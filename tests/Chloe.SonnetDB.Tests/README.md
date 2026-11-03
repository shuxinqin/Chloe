# SonnetDB provider 验证

当前测试项目引用 **SonnetDB 5.0.0**，通过真实 `SndbConnection` 验证适配；能力映射及保留的限制以 [5.0 能力清单](../../docs/sonnetdb-support.md) 为准。

```powershell
dotnet test tests/Chloe.SonnetDB.Tests/Chloe.SonnetDB.Tests.csproj -c Release
```

默认使用独立嵌入式目录；设置 `CHLOE_SONNETDB_CONNECTION_STRING` 可运行相同完整套件，分别指定 `Protocol=rest` 和 `Protocol=frame-http2` 及匹配的服务端口。远程数据库应专用于验收，测试会创建、删除固定名称的表。本机服务设置 `NO_PROXY=127.0.0.1,localhost`。

2026-11-03 的 5.0 候选验证使用候选包及匹配 Server，未使用本地 SonnetDB 源码引用或 mock。Chloe 5.56 基线的三个模式均为 **101/101 通过、0 失败、0 跳过**；该结果不替代当前 5.57 合并源码的重新验证。必须检查实际 TRX 计数、加载的包与 Server 身份；测试适配器未发现测试或仅有进程退出 0 都不能算通过。最终公开 5.0 包发布后仍需独立缓存重新编译及三模式验证。

2026-11-04 已合并上游 Chloe 5.57 源码（`e52d633f`），保留本 PR 的事务失败回滚清理；完整三模式重新取得 **101/101、0 失败、0 跳过**，两个查询过滤器回归现为正向断言。该轮仍绑定 SonnetDB `455bf3b5` 候选及匹配 Server，后续最终发行提交和公开包需要独立复验。

上游 Chloe 5.57 已修复 #434。当前两个 `Category=FrameworkRegression` 测试分别在 SonnetDB 和真实 SQLite 上要求已缓存的查询立即遵守新过滤器；旧 5.56 的两个缺陷现状断言已由正向回归取代。其余测试覆盖原生正向行为与明确不支持的边界，不能把全部计数宣传为正向功能数。`GapContractTests.cs` 沿用已有文件名，当前内容已经验证 5.0 修复后的正向行为；旧 4.0 的拒绝/缺陷断言只作为下面的历史证据。

新 DateTime 映射使用 DATETIME2 并保留 100ns ticks；显式 legacy DATETIME 继续采用毫秒合同，拒绝丢失精度的绑定。Decimal、schema、sequence、持续时长、注释等当前能力见 5.0 清单。NoLock、复杂来源 UpdLock、部分隔离级别及 schema-qualified sequence 仍有明确限制。

候选验证、上游合入、Chloe 核心/适配包发布及公开包消费者验收是不同状态。核心回滚清理修复须随包含它的新版本一同发布；旧的已发布 Chloe 核心不会自动获得这些修复。

## 历史：2026-10-09 SonnetDB 4.0.0

以下保留原 4.0 包/Server 的负控制与缺口证据，所有“当前”“尚未完成”均仅指该历史版本，不是 5.0 能力结论；旧 TRX 及后补单测也不能替代 5.0 验证。

这些历史测试使用发布的 `SonnetDB` NuGet 包 **4.0.0**，通过真实 `SndbConnection` 验证 Chloe provider，不使用模拟数据库或本地 SonnetDB 源码引用。

```powershell
dotnet test tests/Chloe.SonnetDB.Tests/Chloe.SonnetDB.Tests.csproj
```

默认每个测试创建独立临时数据库目录，结束时释放连接并清理目录。设置 `CHLOE_SONNETDB_CONNECTION_STRING` 可在相同测试上验证远程连接；远程数据库应专用于验收，因为测试会创建、删除固定名称的测试表。

`Category=RawSqlProbe` 直接运行数据库 SQL，用于区分数据库缺口和 adapter 翻译错误。`Category=GapContract` 验证已登记的数据库缺口和 adapter 的显式拒绝契约；这类测试通过不表示相应能力已实现。每个缺口测试包含对应 SonnetDB issue 的 `Issue` trait。所有测试均不使用 `Skip`。

```powershell
dotnet test tests/Chloe.SonnetDB.Tests/Chloe.SonnetDB.Tests.csproj --filter 'Category=GapContract'
dotnet test tests/Chloe.SonnetDB.Tests/Chloe.SonnetDB.Tests.csproj --filter 'Category=RawSqlProbe'
```

✅ 完整验收应同时查看成功能力的结果与缺口契约。🚧 DATETIME 亚毫秒精度仍受 [SonnetDB #236](https://github.com/IoTSharp/SonnetDB/issues/236) 限制；毫秒值验证与精度缺陷证实分开进行。🚧 原生 DECIMAL 参数精度由 [#235](https://github.com/IoTSharp/SonnetDB/issues/235) 跟踪，ORM 精确 decimal 回读仍以值完全相等为验收标准。

`Category=FrameworkGapContract` 独立复现 Chloe 缓存查询计划未包含动态过滤器的 [Chloe #434](https://github.com/shuxinqin/Chloe/issues/434)，并在真实 SQLite 上验证相同缺陷，避免将框架继承问题误归因给 SonnetDB。该基线使用 `Microsoft.Data.Sqlite`；SonnetDB 验收继续使用正式 `SonnetDB` 4.0.0 包。

所有尚未完成的数据库能力在 `GapContractTests.cs` 关联具体 issue；🚧 表示缺口仍未实现，⏳ 仅适用于已有修复、等待发布的状态。

SQL `DATETIME` 的验收合同是保留毫秒精度的钟面值（`DateTime.Ticks`），不承诺 reader 返回的 `DateTime.Kind`。Local、Utc 和 Unspecified 参数、日期 SQL 函数、嵌套日期函数、条件、更新和批量写入都按该合同验证。✅ provider 使用原生 `TO_DATETIME` 适配函数结果，并统一保留 Local 参数的钟面值；🚧 原生函数返回的类型/Kind/metadata 差异仍由 [#262](https://github.com/IoTSharp/SonnetDB/issues/262) 跟踪。🚧 亚毫秒参数明确拒绝，避免 #236 导致静默截断。

✅ 默认 decimal 使用无损参数适配，UInt64 全范围和 REST 的 sbyte/ushort/uint 也有实际读写验收；🚧 原生参数缺陷 [#235](https://github.com/IoTSharp/SonnetDB/issues/235)、[#263](https://github.com/IoTSharp/SonnetDB/issues/263) 尚未完成。🚧 [#264](https://github.com/IoTSharp/SonnetDB/issues/264) 证明原生 `DECIMAL(5,2)` 接受超出 precision/scale 的原值，provider 在 DDL 中拒绝显式 `Column.Precision`、`Column.Scale` 和自定义 `DECIMAL(p,s)`/`NUMERIC(p,s)` 声明，避免忽略用户约束。

拦截器验收实际修改实体插入、表达式插入、批量插入、查询及实体/表达式更新和删除，并检查各路 callback 次数、真实值、影响行数及 ADO 的 executing/executed 回调与参数。原生类型配置验收分别通过 `[DataType("JSON")]` 和 `HasSonnetDBDataType("JSON")` 创建真实 JSON 列、写入及精确回读文本。

2026-10-09 最终验收使用正式发布的 SonnetDB 客户端 **4.0.0**；远程模式连接同版本的真实 SonnetDB server。三种连接方式均运行同一套测试，没有 mock、`Skip` 或本地 SonnetDB 源码依赖。

| 连接方式 | ORM 正向 | 原始 SQL 正向 | 数据库缺口契约 | 框架缺口基线 | 通过/总数 | 失败 | 跳过 |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| ✅ Embedded | 72 | 7 | 19 | 2 | 100/100 | 0 | 0 |
| ✅ REST | 72 | 7 | 19 | 2 | 100/100 | 0 | 0 |
| ✅ HTTP2 frame | 72 | 7 | 19 | 2 | 100/100 | 0 | 0 |

TRX 证据分别为 `TestResults/embedded.trx`、`TestResults/remote-rest.trx` 和 `TestResults/remote-http2.trx`，这些运行产物由 `.gitignore` 排除。表中的 19 项数据库缺口契约和 2 项框架缺口基线用于证实及追踪缺陷，通过数量不表示这些能力已经补齐。

全量运行后，在同一个拦截器测试中补齐实体/表达式更新和删除验收；修改后的单条测试又分别在 Embedded、REST 和 HTTP2 frame 上通过 **1/1**，失败/跳过均为 **0**。补验 TRX 为 `TestResults/interceptors-embedded.trx`、`TestResults/interceptors-rest.trx` 和 `TestResults/interceptors-http2.trx`，测试总数保持 100。

远程连接按相应服务端端口设置 `CHLOE_SONNETDB_CONNECTION_STRING` 中的 `Protocol=rest` 或 `Protocol=frame-http2`；HTTP2 服务端端口需要显式使用 `frame-http2`。本机 server 验收设置 `NO_PROXY=127.0.0.1,localhost`，避免系统代理影响本地连接。

HTTP2 项选择正式 driver 的 `frame-http2` 模式。SonnetDB 4.0.0 实际使用 HTTP2 Frame 查询，以及同一 HTTP2 服务端口上的 REST 写入；这是该版本 driver 的读写路径，验收使用显式协议选择，没有 Auto 或向其他协议端口回退。
