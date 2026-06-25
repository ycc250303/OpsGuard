# OpsGuard

Linux 主机 + Docker Compose **只读运维诊断 Agent**（.NET 10 / Semantic Kernel / 阿里云百炼 Qwen）。

用户用自然语言提问 → Agent 通过 ReAct 调用白名单工具采集证据 → **Collector → Analyzer → Advisor** 三阶段流水线输出 Markdown 诊断报告。提供 **Blazor Server Web UI** 与 **命令行 Console** 两种入口。

---

## 功能概览

| 范围 | 能力 |
| --- | --- |
| **Host（宿主机）** | CPU、内存、磁盘、负载（读 `/proc` 等 Linux 指标） |
| **Compose 服务** | 仅检查 `compose-topology.json` 中声明的容器 |
| **容器状态** | 白名单 `docker inspect` |
| **容器日志** | 白名单 `docker logs --tail N`（N 上限 200） |
| **HTTP 探活** | 对 JSON 中配置了 `HealthUrl` 的服务发起 GET |
| **拓扑查询** | 返回当前 JSON 中全部可检查服务列表 |

**设计原则：** 配置驱动、安全只读、Agent 与 Docker 同机部署；不做 systemd/K8s/全容器扫描/任意 Shell。

**典型提问：**

- 「服务器是不是磁盘满了？」
- 「`backend` 容器挂了，帮我查日志」
- 「检查本机和所有配置的 compose 服务」

---

## 技术栈

| 类别 | 选型 |
| --- | --- |
| 运行时 | .NET 10 |
| Agent 框架 | Semantic Kernel `ChatCompletionAgent`、Function Calling |
| 编排 | Multi-Agent：Collector（ReAct + Plugin）→ Analyzer → Advisor |
| LLM | 阿里云百炼 Qwen（OpenAI 兼容 API） |
| Web UI | Blazor Server |
| 测试 | xUnit |

---

## 环境要求

| 项 | 要求 |
| --- | --- |
| **开发机** | [.NET 10 SDK](https://dotnet.microsoft.com/download)（`dotnet --version` 显示 10.x） |
| **运行主机** | **Linux**，且与 Docker **同机**（Host 指标与 `docker` 命令均在本机执行） |
| **Docker** | 运行用户须在 `docker` 组，可执行 `docker inspect` / `docker logs` |
| **LLM** | 环境变量 `DASHSCOPE_API_KEY`（阿里云百炼 API Key） |
| **网络** | 运行主机可访问 `https://dashscope.aliyuncs.com` |

> **说明：** 在 Windows / macOS 上可 **编译、跑单元测试、启动 Web UI**；但 Host 与 Compose 的真实诊断须在 **Linux 目标机**上执行（答辩/生产环境：`111.229.81.45`）。

---

## 项目结构

```
OpsGuard/
├── OpsGuard.sln
├── src/
│   ├── OpsGuard.App/           # Console 入口、交互循环、诊断会话服务
│   ├── OpsGuard.Web/           # Blazor Server Web UI
│   ├── OpsGuard.Core/          # Agent 工厂、Prompt、Models、编排
│   ├── OpsGuard.Plugins/       # 5 个 KernelFunction（Host + Compose + HTTP）
│   └── OpsGuard.Infrastructure/# Docker 客户端、Host 指标、LLM、Filter
├── tests/OpsGuard.Tests/       # xUnit 单元测试
├── docs/
│   ├── compose-topology.sample.json      # 示例拓扑（仓库内）
│   ├── compose-topology.production.json  # 生产拓扑（本地维护，不进 Git）
│   ├── 运行与指令.md                      # 详细部署与排错
│   └── project-plan.md                   # 功能设计与 Plugin 契约
├── deploy/                     # systemd 与部署脚本
└── .env.example                # API Key 模板
```

**5 个只读 Plugin：**

| 工具 | 说明 |
| --- | --- |
| `GetHostMetrics` | 宿主机 CPU/内存/磁盘/负载 |
| `GetComposeServiceStatus` | 指定 `serviceId` 的容器 inspect |
| `QueryComposeServiceLogs` | 指定 `serviceId` 的 docker logs |
| `CheckHttpEndpoint` | 指定 `serviceId` 的 HealthUrl 探活 |
| `GetComposeTopology` | 返回拓扑 JSON 中全部服务 |

---

## 构建

### 1. 克隆仓库

```bash
git clone https://github.com/ycc250303/OpsGuard.git
cd OpsGuard
```

### 2. 还原依赖并编译

```bash
dotnet restore
dotnet build -c Release
```

### 3. 运行测试（推荐每次改代码后执行）

```bash
dotnet test -c Release
```

期望：**50** 个测试全部通过。

---

## 首次配置

### API Key（`.env`）

```bash
cp .env.example .env
```

编辑 `.env`，填入百炼 API Key：

```env
DASHSCOPE_API_KEY=sk-xxxxxxxx
```

- `.env` 已在 `.gitignore` 中，**勿提交 Git**
- 程序启动时自动加载仓库根目录下的 `.env`
- 若 Shell 已 `export DASHSCOPE_API_KEY`，**以环境变量为准**

### Compose 拓扑 JSON

| 文件 | 用途 |
| --- | --- |
| `docs/compose-topology.sample.json` | 仓库示例，本地开发默认可用 |
| `docs/compose-topology.production.json` | 生产配置，需在目标 Linux 机按实际容器名填写，**不进 Git** |

在目标服务器查看容器名：

```bash
docker ps --format '{{.Names}}'
```

将名称填入 JSON 的 `ContainerName`；`Id` 为 Agent 工具参数 `serviceId`（如 `backend`）。有 HTTP 健康检查的服务填写 `HealthUrl`。

示例片段见 [`docs/compose-topology.sample.json`](docs/compose-topology.sample.json)。

---

## 运行

### Web UI（推荐）

```bash
# 确保已配置 .env
dotnet run --project src/OpsGuard.Web
```

浏览器打开：**http://localhost:5299**

- 多轮对话、Markdown 报告渲染、流式输出
- Enter 发送，Shift+Enter 换行

**指定拓扑（任选其一）：**

```bash
# 方式 A：环境变量
export OPSGUARD_TOPOLOGY=docs/compose-topology.production.json   # Linux/macOS
# $env:OPSGUARD_TOPOLOGY="docs/compose-topology.production.json"  # PowerShell
dotnet run --project src/OpsGuard.Web

# 方式 B：启动参数
dotnet run --project src/OpsGuard.Web -- --topology docs/compose-topology.production.json
```

本地未指定时，默认使用 `docs/compose-topology.sample.json`（见 `launchSettings.json`）。

> Web UI **无登录鉴权**。生产环境访问 `http://111.229.81.45:5229/` 时，建议通过 SSH 隧道：`ssh -L 5229:127.0.0.1:5229 root@111.229.81.45`。

---

### 命令行 Console

**交互模式：**

```bash
dotnet run --project src/OpsGuard.App -- \
  --topology docs/compose-topology.production.json
```

```
ops> 检查本机和所有配置的 compose 服务
ops> backend 容器挂了，帮我查日志
ops> exit
```

**单次非交互诊断（脚本/CI 友好）：**

```bash
dotnet run --project src/OpsGuard.App -- \
  --topology docs/compose-topology.production.json \
  --query "backend 容器运行正常吗？"
```

**冒烟测试（API Key、Host Plugin、LLM、Tool Calling）：**

```bash
dotnet run --project src/OpsGuard.App -- \
  --topology docs/compose-topology.production.json \
  --smoke
```

期望输出含 `[OK] API Key 已加载`、`[OK] GetHostMetrics Plugin`、`[OK] LLM 连通`、`[OK] Collector Agent + Tool`。

#### CLI 参数一览

| 参数 | 说明 |
| --- | --- |
| `--topology <path>` | 拓扑 JSON 路径；省略时使用 `docs/compose-topology.sample.json` |
| `--query "<问题>"` | 单次诊断后退出 |
| `--smoke` | 冒烟测试 |
| （无额外参数） | 进入交互模式，提示符 `ops>` |

---

## 发布与部署（简要）

**Console App** 发布到 Linux 服务器（与 Docker 同机）：

```bash
dotnet publish src/OpsGuard.App/OpsGuard.App.csproj -c Release -o /tmp/opsguard-publish
```

**Web** 一键部署脚本：

```bash
./deploy/deploy-web.sh
```

生产环境：

| 组件 | 路径 / 地址 |
| --- | --- |
| Console App | `/opt/opsguard/` |
| Web UI | `/opt/opsguard-web/`，**http://111.229.81.45:5229/** |
| 生产拓扑 | `/opt/opsguard/compose-topology.production.json` |

`main` 分支 push 会触发 GitHub Actions 自动部署。完整步骤、systemd、排错见 [**运行与指令说明**](docs/运行与指令.md)。

---

## 应用配置

主要配置在 `src/OpsGuard.App/appsettings.json`（Web 继承同一套）：

| 配置节 | 键 | 说明 |
| --- | --- | --- |
| `Llm` | `ModelId` | 百炼模型 ID（默认 `qwen3.6-plus`） |
| `Llm` | `Endpoint` | OpenAI 兼容地址，一般无需修改 |
| `Agent` | `MaxLogTailLines` | 日志行数上限（默认 200） |
| `Agent` | `OrchestrationTimeoutMinutes` | 单次诊断超时（默认 10 分钟） |
| `Agent` | `ConversationHistoryTurns` | 多轮对话带入的历史轮数 |

---

## 文档

| 文档 | 说明 |
| --- | --- |
| [**运行与指令说明**](docs/运行与指令.md) | 环境配置、CLI/Web 参数、本地与服务器部署、示例与排错 |
| [项目方案](docs/project-plan.md) | 功能设计、技术方案、Plugin 契约、答辩场景 |
| [Agent 配置](AGENTS.md) | Cursor Agent 协作约定与分层架构 |

---

## 许可证

.NET 程序设计课程期末项目。
