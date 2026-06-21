# OpsGuard

Linux 主机 + Docker Compose 运维诊断 Agent（.NET / Semantic Kernel / Qwen）。

自然语言提问 → ReAct 只读工具采集 → **Collector → Analyzer → Advisor** 输出 Markdown 诊断报告。

## 快速开始

```bash
cp .env.example .env          # 填入 DASHSCOPE_API_KEY
dotnet test

# Web UI（推荐）
dotnet run --project src/OpsGuard.Web

# 或命令行
dotnet run --project src/OpsGuard.App -- \
  --topology docs/compose-topology.production.json
```

浏览器访问 Web：**http://localhost:5299**

## 文档

| 文档 | 说明 |
| --- | --- |
| [**运行与指令说明**](docs/运行与指令.md) | 环境配置、CLI 参数、本地/服务器部署、示例与排错 |
| [项目方案](docs/project-plan.md) | 功能设计、技术方案、Plugin 契约 |
| [Agent 配置](AGENTS.md) | Cursor Agent 协作约定 |

## 常用命令

```bash
# Web UI
dotnet run --project src/OpsGuard.Web

# 冒烟测试（Plugin + LLM + Tool Calling）
dotnet run --project src/OpsGuard.App -- --topology docs/compose-topology.production.json --smoke

# 单次非交互诊断
dotnet run --project src/OpsGuard.App -- \
  --topology docs/compose-topology.production.json \
  --query "backend 容器运行正常吗？"

# 发布到生产机 111.229.81.45
dotnet publish src/OpsGuard.App -c Release -o /tmp/opsguard-publish
# 详见 docs/运行与指令.md §5
```
