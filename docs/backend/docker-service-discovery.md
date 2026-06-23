# Docker 服务自动发现

## 概述

Compose 服务清单由 **`docker ps` 自动发现**，可选 **overlay JSON**（原 `compose-topology*.json`）补充 `HealthUrl`、DisplayName、serviceId 别名。overlay 文件缺失时仍可启动，仅依赖发现结果。

## 流程

```
docker ps -a --format json
    → DockerContainerDiscovery
    → TopologyMerger（合并 overlay）
    → DynamicServiceCatalog（RefreshAsync）
    → GetComposeTopology / DiscoverDockerServices / 状态·日志·HTTP 工具
```

每次诊断流水线开始前会 `RefreshAsync`；调用 `GetComposeTopology` 或 `DiscoverDockerServices` 也会刷新。

## overlay JSON

与旧拓扑格式兼容，但**不要求**列全所有容器。每条 overlay 至少指定 `Id`、`ContainerName`、`ComposeService` 之一用于匹配。

| 字段                          | 用途                          |
| ----------------------------- | ----------------------------- |
| `ComposeProjectName`        | 只发现该项目下的 Compose 容器 |
| `Services[].ContainerName`  | 按容器名匹配（优先）          |
| `Services[].ComposeService` | 按 compose service 名匹配     |
| `Services[].Id`             | 对外 serviceId 别名           |
| `Services[].HealthUrl`      | HTTP 探活（仅 overlay 提供）  |

生产 overlay：`/opt/opsguard/compose-topology.production.json`（不进 Git）。

## 配置

`Agent` 节：

| 项                                     | 默认              | 说明                        |
| -------------------------------------- | ----------------- | --------------------------- |
| `DockerDiscoveryComposeOnly`         | `true`          | 仅发现带 Compose 标签的容器 |
| `DockerDiscoveryExcludeNamePrefixes` | `["opsguard-"]` | 排除容器名前缀              |

`--topology` / `OPSGUARD_TOPOLOGY`：overlay 文件路径；不存在则纯发现模式。

## 安全

LLM 仍只能操作**合并后清单**内的 `serviceId`→`containerName`，不能直接传入任意容器名。

## 测试

`DockerDiscoveryTests`：合并、解析、`TopologyOverlayProvider` 可选加载。
