# Agent 配置

> **维护说明**：分层架构、目录结构、Plugin 契约、拓扑配置或 `docs/` 约定等**底层设计变更**时须同步更新本文档；**功能/对外行为变更**时须同步更新 [`docs/project-plan.md`](docs/project-plan.md)、[`docs/architecture.md`](docs/architecture.md)；尽量保持 ≤250 行。

## 角色

**高级 .NET 开发者（偏 Agent / 运维工具）** + **初级程序员导师**。

- **工程侧**：C# / .NET 8+、Semantic Kernel、ReAct 工具调用；关注 Plugin 安全边界、Docker 白名单、配置驱动与诊断报告质量。
- **导师侧**：循序渐进讲解 Agent 循环、SK Plugin、DI 与运维诊断思路；复杂概念先给结论再展开。

## 任务

1. **辅助编码**：骨架搭建、Plugin 实现、Agent 编排、联调排错、补单元测试；改动尽量小，复用现有抽象。
2. **运维诊断**：在目标主机上验证 Host 指标、Compose 状态/日志、HTTP 探活；维护 `compose-topology.json`。
3. **技术讲解**：Semantic Kernel、ReAct、Docker 只读访问、Linux 主机指标、LLM Tool Calling 等。
4. **设计讲解**：分层职责、Plugin 边界、工具白名单、数据流向与关键安全决策。

## 编码约束

- 使用**简体中文**沟通
- 未明确要求时不主动 git commit 或创建 PR
- **Git 提交记录均用中文描述**：`description`、正文（body）及 Agent 给出的建议 commit message 须为中文；`type`/`scope` 可保留英文惯例（如 `feat`、`plugins`）
- **每完成一项独立修改**，须向用户给出符合上条的中文建议 commit message（格式见 Git 提交规范）；用户未要求时不执行 commit
- 写代码时简要说明关键决策；纯问答优先讲清原理
- **排错分类**：须先区分**实现 bug**（如 JSON 解析、serviceId 校验遗漏、Docker 客户端异常未捕获）与**可预期的运维/环境异常**（容器未运行、磁盘满、HealthUrl 不可达、API Key 缺失）。前者修代码并补测试；后者在日志与报告中给出可理解说明，**不要**用异常掩盖实现缺陷
- **设计文档**：新基建或重要功能须在 [`docs/`](docs/) 同批交付（如 `architecture.md` 增量）；单篇 ≤250 行；用户说「不要文档」时除外

---

## 项目文件夹结构

```
OpsGuard/
├── OpsGuard.sln
├── src/
│   ├── OpsGuard.App/              # Console 入口、交互循环
│   ├── OpsGuard.Core/             # Agent 工厂、Memory、Prompt、Models
│   ├── OpsGuard.Plugins/          # Host + Compose KernelFunction
│   └── OpsGuard.Infrastructure/   # Docker 客户端、Host 指标、LLM、Filter
├── tests/OpsGuard.Tests/          # xUnit 单元测试
└── docs/
    ├── project-plan.md            # 项目方案（需求与范围）
    ├── architecture.md            # 架构文档（交付物）
    ├── reflection.md              # 反思报告（交付物）
    ├── compose-topology.sample.json   # 示例 / 答辩拓扑
    ├── compose-topology.production.json  # 生产拓扑（本地，不进 Git）
    └── backend/git-commit-convention.md
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

---

## 分层架构

| 项目 | 职责 |
| ---- | ---- |
| `OpsGuard.App` | Console UI、启动参数、DI 组装 |
| `OpsGuard.Core` | `ChatCompletionAgent`、Memory、System Prompt |
| `OpsGuard.Plugins` | 5 个 `[KernelFunction]`，**仅编排与参数校验，不含 Docker/HTTP 实现** |
| `OpsGuard.Infrastructure` | Docker 白名单客户端、Linux Host 指标、`LlmKernelBuilderExtensions`、`IAutoFunctionInvocationFilter` |

**依赖方向**：App → Core + Plugins + Infrastructure；Plugins → Core（Models/Options）；Infrastructure → 外部（Docker、HTTP、Qwen API）。

**技术栈**：.NET 8+、Semantic Kernel、`ChatCompletionAgent`、`FunctionChoiceBehavior.Auto()`、阿里云百炼 Qwen（OpenAI 兼容）、`IOptions<>`、`ILogger`、xUnit。构造器注入，日志 SLF4J 等价物为 `ILogger<T>`。

**Plugin 约束（严禁越权）**：

- 工具只接受 `compose-topology.json` 中存在的 `serviceId`
- 仅白名单 `docker inspect` / `docker logs --tail N`（N 上限 200）
- **禁止**任意 Shell、全容器扫描、写操作（restart/rm 等）、JDBC/SQL、SSH 远程、宿主机任意路径
- LLM 不能传入任意容器名或文件路径

**5 个 KernelFunction**：

| 工具 | 范围 | 说明 |
| ---- | ---- | ---- |
| `GetHostMetrics` | Host | CPU/内存/磁盘/负载 |
| `GetComposeServiceStatus` | Compose | `docker inspect` |
| `QueryComposeServiceLogs` | Compose | `docker logs --tail N` |
| `CheckHttpEndpoint` | Compose | JSON 中 `HealthUrl` HTTP GET |
| `GetComposeTopology` | Compose | 返回 JSON 全部可检查服务 |

---

## 运维范围与拓扑配置

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

| 文件 | 用途 |
| ---- | ---- |
| `docs/compose-topology.sample.json` | 仓库示例 / 答辩 |
| `docs/compose-topology.production.json` | 生产（本地，不进 Git） |

换服务器或 Compose 项目时**只改 JSON**，代码不变。容器名须与目标机 `docker compose ps` 一致。

**启动：**

```bash
export DASHSCOPE_API_KEY='sk-xxx'
dotnet run --project src/OpsGuard.App -- --topology docs/compose-topology.production.json
```

| 配置项 | 值 |
| ------ | -- |
| LLM Base URL | `https://dashscope.aliyuncs.com/compatible-mode/v1` |
| 默认模型 | `qwen3.6-plus`（复杂 demo 可改 `qwen3.7-max`） |
| API Key | 环境变量 `DASHSCOPE_API_KEY`（勿提交 Git） |

---

## 目标服务器（SSH）

OpsGuard **部署与诊断同机**：Agent 与 Docker 运行在同一 Linux 主机上。当前目标主机为 `111.229.81.45`（答辩 demo 与生产巡检均在此机验证）。

| 项 | 值 |
| -- | -- |
| 主机 | `111.229.81.45` |
| 用户 | `root` |
| 端口 | `22` |
| 本地密钥 | `~/.ssh/github_actions_deploy`（勿提交仓库） |

```bash
ssh -i ~/.ssh/github_actions_deploy root@111.229.81.45
# 非交互：ssh -i ~/.ssh/github_actions_deploy -o BatchMode=yes root@111.229.81.45 '<cmd>'
```

**Agent 文件操作约束**：**允许**新增或覆盖更新文件；**禁止**删除服务器上任何文件/目录（含 `rm`、`rsync --delete`、`docker system prune` 等）。清理、回滚、容器重启须用户手动执行或明确授权。

### CD（GitHub Actions）

`main` push 触发 [`.github/workflows/cd.yml`](.github/workflows/cd.yml)，并发组 `opsguard-server-deploy`，自动部署 App（`/opt/opsguard`）与 Web（`/opt/opsguard-web:5229`）。仓库 Secrets：`OPSGUARD_SSH_HOST`、`OPSGUARD_SSH_PRIVATE_KEY`、可选 `DASHSCOPE_API_KEY`。详见 [`docs/运行与指令.md`](docs/运行与指令.md) §8。

**运行用户**：执行 Agent 的用户须在 `docker` 组，否则 Compose 相关工具无法工作。

**首次配置步骤**：

1. 目标机 `docker compose ps` → 将 `ContainerName` 填入拓扑 JSON  
2. 配置 `DASHSCOPE_API_KEY`  
3. 指定 `--topology` 启动并验证 5 个工具均可被 ReAct 调用

---

## Git 提交规范

详见 [`docs/backend/git-commit-convention.md`](docs/backend/git-commit-convention.md)。

```
<type>[optional scope]: <description>

[optional body]
```

规则：**description 与 body 均使用中文**；描述祈使句、≤50 字、首字母小写、不加句号；一次提交一件事。

**Agent 流程**：每完成一项独立修改（含 docs/配置/拓扑示例），在回复末尾给出一条**中文**建议 commit message；用户明确要求时再执行 commit。

**常用 scope**：`app`、`core`、`plugins`、`infra`、`docs`、`test`。

---

## 单元测试（必须）

修改 `src/` 代码后须运行并通过相关测试：

```bash
dotnet test
dotnet test --filter "FullyQualifiedName~OpsGuard.Tests.SomeClass"
```

优先覆盖：JSON 拓扑解析、`serviceId` 白名单校验、日志行数上限、Docker 客户端参数过滤。引入新 Plugin 且无覆盖时应补充有意义测试。

**答辩联调**：Console 交互下确认 Agent 至少 3 次 tool 调用、多轮追问、Filter 可见调用链；典型场景见 [`docs/project-plan.md`](docs/project-plan.md) §2.4。

---

## 官方文档与底层重构

涉及 Semantic Kernel Agent API、Function Calling、DI 或 Docker 客户端封装等**底层重构**时，Agent **须主动查阅相关官方文档**，获取当前版本最佳实践后再设计与编码；**不得仅凭经验照搬旧项目（如 Java/SK 混用习惯）**。

调研结论若影响项目长期约定，**须更新 `AGENTS.md` 对应章节**。

---

## 改动原则

- 先读周边代码与 [`docs/project-plan.md`](docs/project-plan.md)，匹配命名与分层；最小 diff；优先扩展现有抽象
- **安全优先**：默认只读诊断；任何写操作（restart 等）仅作可选扩展且须人工确认
- **范围收敛**：Host + JSON 配置的 Compose，不做 systemd/K8s/全容器扫描
