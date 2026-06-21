# OpsGuard — 服务器运维诊断 Agent 项目方案

> .NET 程序设计课程 · 期末项目  
> 技术栈：C# / .NET 8+ · Semantic Kernel · 阿里云百炼 Qwen

---

## 目录

1. [项目概述](#一项目概述)
2. [功能设计](#二功能设计)
3. [技术方案](#三技术方案)
4. [作业要求对照](#四作业要求对照)
5. [加分项策略](#五加分项策略)
6. [实施计划](#六实施计划)
7. [安全与答辩](#七安全与答辩)
8. [要求总表](#八要求总表)
- [附录 A：ReAct 与 Semantic Kernel](#附录-a-react-与-semantic-kernel)
- [附录 B：环境变量与启动](#附录-b-环境变量与启动)

---

## 一、项目概述

### 1.1 项目定位

**项目名称：** OpsGuard — Linux 主机 + Docker Compose 运维诊断 Agent

**一句话：** 检查**服务器本身**（CPU/内存/磁盘/负载）与**人工 JSON 配置的 Docker Compose 服务**（容器状态、`docker logs`、可选 HTTP 探活）；用户自然语言提问，Agent 通过 ReAct 调用只读工具并输出诊断报告。

**设计原则：**

- **范围收敛** — 只做 Host + Compose，不做 systemd/K8s/全容器扫描
- **配置驱动** — Compose 服务仅来自 `compose-topology.json`
- **安全只读** — 白名单 Docker 命令，LLM 不能传入任意路径或 shell

### 1.2 运维范围

```
┌─────────────────────────────────────────────────┐
│  ① 服务器本身（Host）                            │
│     CPU / 内存 / 磁盘 / 负载                     │
├─────────────────────────────────────────────────┤
│  ② Docker Compose 服务（人工 JSON 配置）         │
│     容器状态 · docker logs · 可选 HealthUrl      │
│     仅检查 JSON 中声明的服务                     │
└─────────────────────────────────────────────────┘
```

| 在范围内 | 不在范围内 |
|----------|------------|
| 主机资源与负载 | systemd / 裸机进程 |
| JSON 中列出的 Compose 容器 | 自动扫描全部 Docker 容器 |
| `docker inspect` / `docker logs`（白名单） | 任意 Shell、JDBC/SQL 探库 |
| 配置了 `HealthUrl` 的 HTTP GET | K8s、多机、SSH 远程、Windows |
| Agent 与 Docker 同机部署 | 7×24 后台监听（后续扩展） |

### 1.3 典型场景

| 场景 | 用户输入示例 |
|------|--------------|
| 主机资源 | 「服务器是不是磁盘满了？」 |
| Compose 故障 | 「`backend` 容器挂了，帮我查日志」 |
| HTTP 探活 | 「`web-gateway` 健康检查失败」 |
| 全量巡检 | 「检查本机和所有配置的 compose 服务」 |

### 1.4 配置与换环境

| 项 | 说明 |
|----|------|
| Compose 服务清单 | **必须人工维护** `compose-topology.json` |
| 容器名 | 在服务器执行 `docker compose ps`，填入 `ContainerName` |
| 答辩 demo | 使用 `docs/compose-topology.sample.json` |
| 生产环境 | 使用 `compose-topology.production.json`（不提交 Git） |
| 换服务器 | 只换 JSON，代码不变 |

---

## 二、功能设计

### 2.1 核心功能

| 功能 | 范围 | 行为 |
|------|------|------|
| 自然语言诊断 | 全局 | 多轮 tool 调用 → Markdown 报告 |
| 主机巡检 | ① Host | CPU、内存、磁盘、负载 |
| Compose 状态 | ② JSON 服务 | running/exited、重启次数 |
| Compose 日志 | ② JSON 服务 | `docker logs` 尾部 N 行 |
| HTTP 探活 | ② 有 HealthUrl 的服务 | 状态码、响应时间 |
| 多轮对话 | 全局 | 「再查 backend 日志」「磁盘呢？」 |
| 推理可见 | 全局 | Console 展示 tool 调用链 |

### 2.2 诊断工具（Plugin）

共 **5 个** `[KernelFunction]`（满足作业 ≥3 要求）：

| 工具 | 范围 | 输入 | 说明 |
|------|------|------|------|
| `GetHostMetrics` | Host | 无 | CPU/内存/磁盘/负载 |
| `GetComposeServiceStatus` | Compose | `serviceId` | `docker inspect` |
| `QueryComposeServiceLogs` | Compose | `serviceId`、行数 | `docker logs --tail N` |
| `CheckHttpEndpoint` | Compose | `serviceId` | 读取 JSON 中 `HealthUrl` |
| `GetComposeTopology` | Compose | 无 | 返回 JSON 中全部可检查服务 |

**安全约束：** 工具只接受 JSON 里存在的 `serviceId`，不接受 LLM 传入的任意路径或容器名。

### 2.3 功能边界

**做：** 主机只读指标 · Compose 仅 JSON 清单 · 白名单 `docker inspect`/`docker logs` · 单 Linux 主机

**不做：** 全容器扫描 · systemd · SQL 探库 · 宿主机 LogPath · 任意 Shell · 多机/K8s · 实时 tail -f 监听

**可选扩展（非 V1）：** Blazor UI · 人工确认后 `docker compose restart` · 定时巡检

### 2.4 答辩演示场景

| 场景 | Agent 行为 |
|------|------------|
| 磁盘满 | `GetHostMetrics` → 建议清理 |
| 容器退出 | `GetComposeServiceStatus` → `QueryComposeServiceLogs` |
| 网关 unhealthy | `CheckHttpEndpoint` 非 200 → 查 compose 日志 |
| 全量巡检 | `GetHostMetrics` + 遍历 JSON 中每个 `serviceId` |
| 多轮追问 | 「backend 呢？」→ 仅查 JSON 中的 `backend` |

### 2.5 Compose 拓扑配置

**文件位置：**

| 文件 | 用途 |
|------|------|
| `docs/compose-topology.sample.json` | 仓库示例 / 答辩 |
| `docs/compose-topology.production.json` | 生产（本地，不进 Git） |

**完整示例：**

```json
{
  "ComposeProjectName": "myapp",
  "Host": {
    "DisplayName": "生产服务器"
  },
  "Services": [
    {
      "Id": "backend",
      "DisplayName": "后端 API",
      "ContainerName": "myapp-backend-1",
      "ComposeService": "backend",
      "HealthUrl": "http://127.0.0.1:5000/health",
      "Description": "ASP.NET Core API"
    },
    {
      "Id": "web-gateway",
      "DisplayName": "前端 Nginx",
      "ContainerName": "myapp-nginx-1",
      "HealthUrl": "http://127.0.0.1:80"
    },
    {
      "Id": "postgres",
      "DisplayName": "数据库",
      "ContainerName": "myapp-postgres-1",
      "Description": "仅查容器状态与 logs"
    }
  ]
}
```

**字段说明：**

| 字段 | 必填 | 说明 |
|------|------|------|
| `Id` | ✅ | 工具参数 `serviceId` |
| `ContainerName` | ✅ | 与 `docker ps` 一致 |
| `DisplayName` | 建议 | 报告展示名 |
| `ComposeService` | 可选 | compose 中的 service 名 |
| `HealthUrl` | 可选 | 有 HTTP 入口时填写 |
| `Description` | 可选 | 写入 System Prompt |

**ReAct 示例：**

```
用户: 「backend 启动失败」
  → GetComposeTopology()
  → GetComposeServiceStatus("backend")
  → QueryComposeServiceLogs("backend", 100)
  → GetHostMetrics()          // 若怀疑资源问题
  → 输出诊断报告
```

**Plugin 实现要点：**

1. 启动加载 JSON → `IOptions<ComposeTopologyOptions>`
2. 日志最多 200 行，防止撑爆 LLM 上下文
3. 运行用户需在 `docker` 组

---

## 三、技术方案

### 3.1 项目结构

```
End_Assignment/OpsGuard/
├── OpsGuard.sln
├── src/
│   ├── OpsGuard.App/                 # Console 入口
│   ├── OpsGuard.Core/                # Agent、Memory、Prompt、Models
│   ├── OpsGuard.Plugins/             # Host + Compose Plugins
│   └── OpsGuard.Infrastructure/      # Docker 客户端、Host 指标、LLM、Filter
├── tests/OpsGuard.Tests/
└── docs/
    ├── project-plan.md               # 本文档
    ├── architecture.md               # 交付物
    ├── reflection.md                 # 交付物
    ├── compose-topology.sample.json
    └── compose-topology.schema.json  # 可选
```

**Plugins 目录：**

```
OpsGuard.Plugins/
├── Host/HostMetricsPlugin.cs
├── Compose/
│   ├── ComposeStatusPlugin.cs
│   ├── ComposeLogsPlugin.cs
│   └── ComposeTopologyPlugin.cs
└── Network/HttpCheckPlugin.cs
```

**Infrastructure 目录：**

```
OpsGuard.Infrastructure/
├── Configuration/ComposeTopologyOptions.cs, LlmOptions.cs
├── Llm/LlmKernelBuilderExtensions.cs
├── Docker/IComposeDockerClient.cs, ComposeDockerClient.cs
├── Host/IHostMetricsReader.cs, LinuxHostMetricsReader.cs
└── Logging/AgentInvocationFilter.cs
```

### 3.2 分层架构

```
OpsGuard.App              Console UI
OpsGuard.Core             Agent 工厂 · Memory · Prompt
OpsGuard.Plugins          5 个 KernelFunction
OpsGuard.Infrastructure   Docker 白名单 · Host 指标 · 日志 Filter
外部                      Qwen API · Linux · Docker
```

### 3.3 技术栈

| 层级 | 选型 |
|------|------|
| 运行时 | .NET 8 或 10 |
| AI 框架 | Semantic Kernel + `ChatCompletionAgent` |
| LLM | 阿里云百炼 Qwen（OpenAI 兼容接口） |
| 配置 | `IOptions<>` + JSON 拓扑文件 |
| 日志 | `ILogger` + Serilog（可选） |
| 测试 | xUnit |

### 3.4 LLM（Qwen）

| 模型 | 用途 |
|------|------|
| `qwen3.6-plus` | **默认** — 日常诊断，免费额度 |
| `qwen3.7-max` | 复杂 demo — 改 `ModelId` 即可 |

| 配置项 | 值 |
|--------|-----|
| Base URL | `https://dashscope.aliyuncs.com/compatible-mode/v1` |
| API Key | 环境变量 `DASHSCOPE_API_KEY`（不进 Git） |
| Tool Calling | `FunctionChoiceBehavior.Auto()` |

```csharp
builder.AddOpenAIChatCompletion(
    modelId: options.ModelId,
    apiKey: options.ApiKey,
    endpoint: new Uri("https://dashscope.aliyuncs.com/compatible-mode/v1"));
```

### 3.5 应用配置

**`appsettings.json`：**

```json
{
  "Llm": {
    "ModelId": "qwen3.6-plus",
    "Endpoint": "https://dashscope.aliyuncs.com/compatible-mode/v1"
  },
  "ComposeTopology": {
    "TopologyFile": "docs/compose-topology.sample.json"
  }
}
```

**启动：**

```bash
export DASHSCOPE_API_KEY='sk-xxx'
dotnet run --project src/OpsGuard.App -- --topology docs/compose-topology.production.json
```

服务清单写在 `compose-topology.json`，不内联到 `appsettings.json`。

---

## 四、作业要求对照

### 4.1 核心要素

| 必须项 | 实现 |
|--------|------|
| LLM 集成 | Qwen + 百炼 OpenAI 兼容接口 |
| Agent Loop | `ChatCompletionAgent` + `FunctionChoiceBehavior.Auto()` |
| ≥3 工具 | 5 个 Plugin（Host + Compose） |
| 记忆 | `ChatHistoryAgentThread` + `ServerContextMemory` |
| UI | Console 交互循环 |

### 4.2 技术要求

| 必须项 | 实现 |
|--------|------|
| C# / .NET 8+ | 全项目 net8.0（或 net10.0） |
| Semantic Kernel | 是 |
| 自定义 Plugin | `[KernelFunction]` + `[Description]` |
| async/await | Plugin、`HttpClient`、`InvokeAsync` 全 async |
| 错误处理与日志 | try/catch + `ILogger` + `IAutoFunctionInvocationFilter` |

### 4.3 交付物

| 交付物 | 文件 |
|--------|------|
| 源码 + README | 仓库根目录 |
| 架构文档 | `docs/architecture.md` |
| 反思报告 | `docs/reflection.md`（含核心循环逐行解读、AI 使用说明） |
| 演示答辩 | 2–3 场景，10+5 分钟 |

### 4.4 评分策略（100 分）

| 维度 | 分值 | 策略 |
|------|------|------|
| Agent 核心功能 | 30 | Host + Compose 场景跑通，≥3 次 tool |
| 技术实现 | 20 | 分层清晰，Plugin 单一职责 |
| .NET 深度 | 15 | DI、`IOptions`、Filter |
| 架构文档 | 15 | 架构图 + 工具表 + ReAct 流程 |
| 反思与答辩 | 20 | 原理解释 + demo |

---

## 五、加分项策略

### 5.1 进阶项

| 项 | 状态 |
|----|------|
| Multi-Agent | ⚠️ 可选（Collector → Analyzer → Advisor） |
| RAG | ⚠️ 可选（索引 `docs/runbook/`） |
| MCP | ❌ 不做 |
| Streaming | ⚠️ 可选（demo 效果） |
| 可观测性 | ✅ `IAutoFunctionInvocationFilter` |

### 5.2 正式加分（最多 +10）

| 加分项 | 分值 | 建议 |
|--------|------|------|
| Multi-Agent | +3 | 与 RAG 二选一 |
| RAG | +3 | 与 Multi-Agent 二选一 |
| MCP | +2 | 不做 |
| 单元测试 | +2 | ✅ 建议做（JSON 解析、容器名校验） |
| 创新性 | +2 | ✅ Host + Compose 双域诊断 |

**最少精力路线：** 必做 + 单元测试(+2) + 创新(+2) + RAG 或 Multi-Agent(+3) ≈ **+7 分**

---

## 六、实施计划

| 阶段 | 时间 | 内容 |
|------|------|------|
| P0 | 2 天 | 骨架 + Qwen + 5 Plugin + Console + README |
| P1 | 1 天 | Memory + Filter + `architecture.md` |
| P2 | 1 天 | 2 个 demo + `reflection.md` + PPT |
| P3 | 1 天 | 单元测试 + RAG 或 Multi-Agent（二选一） |
| P4 | 可选 | Streaming / Blazor |

**分工建议：**

| 成员 | 负责 |
|------|------|
| A | Host/Compose Plugins + Docker 客户端 |
| B | Agent 工厂 + Qwen + Memory + Console + Filter |
| C | 文档 + 演示脚本 + PPT |

---

## 七、安全与答辩

| 要点 | 答辩话术 |
|------|----------|
| 只读诊断 | 默认不重启/不删容器 |
| 白名单 Docker | LLM 无法执行任意命令 |
| serviceId 白名单 | 未配置的服务不可查，范围可控 |
| ReAct 在 SK 内 | Plugin=Action，Filter=Observation 记录 |
| Key 安全 | `DASHSCOPE_API_KEY` 仅环境变量 |

---

## 八、要求总表

| 类别 | 条目 | 状态 |
|------|------|------|
| 必做 | LLM / Agent Loop / ≥3 工具 / 记忆 / UI | ✅ |
| 必做 | .NET 8+ / SK / Plugin / async / 日志 | ✅ |
| 必做 | 源码 / 架构文档 / 反思报告 / 答辩 | ✅ |
| 加分 | 单元测试 +2 / 创新 +2 | ✅ 建议 |
| 加分 | Multi-Agent +3 / RAG +3 | ⚠️ 二选一 |
| 加分 | MCP +2 | ❌ |

---

## 附录 A：ReAct 与 Semantic Kernel

```
用户输入
  → Thought   LLM 分析（SK 内部）
  → Action    调用 [KernelFunction]（FunctionChoiceBehavior.Auto）
  → Observation  工具结果写入 ChatHistory（Filter 可记录）
  → 重复直至完成或达到 MaxSteps
```

答辩需能解释上述循环，即使代码中没有显式 `for` 循环。

---

## 附录 B：环境变量与启动

```bash
# 必需
export DASHSCOPE_API_KEY='sk-xxx'

# 启动（指定生产拓扑）
dotnet run --project src/OpsGuard.App -- \
  --topology docs/compose-topology.production.json
```

**首次配置步骤：**

1. [阿里云百炼](https://bailian.console.aliyun.com/) 创建 API Key  
2. `docker compose ps` → 填写 `ContainerName` 到 JSON  
3. 默认模型 `qwen3.6-plus`；复杂 demo 改为 `qwen3.7-max`
