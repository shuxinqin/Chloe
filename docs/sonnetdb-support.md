# SonnetDB 适配与能力验收

基线：Chloe `5.56.0`、SonnetDB 正式 NuGet 包 `4.0.0`，核查日期为 2026-10-09。范围是 Chloe 已提供的关系型 ORM 能力；不为 SonnetDB 的时序、向量、文档等额外模型扩展 Chloe 接口。

主跟踪：[Chloe #433](https://github.com/shuxinqin/Chloe/issues/433)。数据库缺口解决前保持草稿 PR，适配包尚未发布。

## 使用

```csharp
using Chloe.SonnetDB;
using Chloe.SonnetDB.DDL;
using SonnetDB.Data;

using var context = new SonnetDBContext(
    () => new SndbConnection("Data Source=./chloe-data"));
new SonnetDBTableGenerator(context).CreateTable(typeof(User));
var users = context.Query<User>().Where(user => user.Id > 0).ToList();
```

`Data Source` 是数据库目录。远程连接由 `SndbConnection` 的远程连接字符串提供。适配只引用 Chloe，和现有 PostgreSQL/SQLite 项目一样由应用提供 ADO.NET 连接工厂。

适配程序集保留 `net10.0/net8.0/net6.0/netstandard2.0/net46` 目标；**SonnetDB 4.0.0 驱动只有 net10.0 资产，当前实际使用要求 .NET 10**。编译通过其他目标不能作为 SonnetDB 驱动在那些运行时可用的证据。

## 状态规则

- ✅ 已完成：真实发布包端到端验收通过。
- 🧪 待验证：有适配实现，但尚未取得对应环境的端到端证据。
- 🚧 未完成：适配或数据库能力仍需实现，关联 issue 记录验收要求。
- ⏳ 等待 SonnetDB 发布：数据库修复已有源码与测试，正式包尚未包含；必须列出对应修复与版本依据，不能将未实现能力标成等待发布。
- 📦 等待 Chloe 发布：适配尚未合并或发布为 NuGet 包。

不把已有但语义不同的 SQL 当作等价支持。不通过客户端筛选、伪造序列、忽略 schema 或锁请求掩盖缺口。因上游缺口暂时拒绝的功能仍算未完成，不能作为完整适配的证明。

## 能力清单

下面按现有 provider 与核心 ORM 的实际接口核对。✅ 行仅表示所列范围通过，不能用这些结果宣称 🚧 行已经完成。数据库缺口解决前 PR 保持 draft。

| 能力 | 状态 | 验收或缺口 |
| --- | --- | --- |
| 实体/表达式增删改查、基础原始 SQL、参数绑定 | ✅ | 有主键模型；原始 DbParam 的精度/远程类型缺陷见下表 |
| 自增回填、仅自增列的 DEFAULT VALUES | ✅ | INSERT SELECT RETURNING，不依赖多语句或 LAST_INSERT_ROWID |
| 异步查询与写入 | ✅ | 使用真实 ADO.NET 异步路径 |
| 批量写入、分批、事务原子性 | ✅ | 参数化 VALUES；失败提交后的事务清理有回归测试 |
| WHERE、NULL、布尔、CASE、IN/EXISTS/子查询 | ✅ | 真实结果和 NULL 语义 |
| 排序、Skip/Take、Distinct、分页 | ✅ | LIMIT/OFFSET，含 Skip-only；分页界限为 SonnetDB 的 Int32 范围 |
| INNER/LEFT/RIGHT/FULL JOIN | ✅ | 四种连接的匹配与未匹配行均验收 |
| GROUP BY/HAVING、Count/LongCount/Sum/Min/Max/Average | ✅ | 包括空集合；显式外层聚合转换另列缺口 |
| 字符串长度/大小写/Substring/Trim/Replace/Contains | ✅ | 原生函数，拼接保留 NULL 与通配字符的 CLR 语义 |
| 日期成员、Now/UtcNow/Today、AddXX/DiffXX | ✅ | 毫秒精度输入；原生函数，无 SQLite JULIANDAY 或日期修饰符 |
| 基础 CAST、Parse、ToString、算术、按位与/或 | ✅ | 整数除法保持截断语义与 Int64 精度；不是全部 SQL 表达式承诺 |
| 有符号/无符号整数、浮点、Decimal、Bool、Guid、BLOB、枚举、Nullable | ✅ | UInt64 使用原生 DECIMAL，高精度 Decimal 使用字符串参数+CAST；远程小整数无损扩大为 Int64 |
| DateTime 写入与回读 | ✅ | DATETIME 毫秒精度的钟面值；UTC/Local/Unspecified 参数保持 ticks；亚毫秒参数提前拒绝 |
| 建表、主键、复合主键、NOT NULL、自定义数据类型 | ✅ | 原生 DDL；JSON 注解/fluent 配置与文本回读；长度/精度约束、无主键写入和注释另列缺口 |
| 实体跟踪、RowVersion 乐观并发 | ✅ | 复用核心；不能当作悲观行锁替代 |
| 导航 Include、SplitQuery、级联保存、分片读写 | ✅ | 复用 Chloe 核心并运行真实数据库验收 |
| 自定义方法/属性处理器、DbFunction、Options.Clone | ✅ | 保持 provider 扩展入口 |
| DbContext/DbCommand 拦截器 | ✅ | 真实插入、表达式插入、批量、查询、更新/删除及 executing/executed 回调验收 |
| 显式 schema | 🚧 | [#232](https://github.com/IoTSharp/SonnetDB/issues/232)；生成 SQL/建表时明确拒绝 |
| Sequence/Sql.NextValueForSequence | 🚧 | [#231](https://github.com/IoTSharp/SonnetDB/issues/231)；不伪造共享序列 |
| LockType.UpdLock / NoLock | 🚧 | [#233](https://github.com/IoTSharp/SonnetDB/issues/233)；查询锁提示明确拒绝；已关闭 #190 完成的是拒绝合同，行锁未实现 |
| 高于 ReadCommitted 的隔离级别 | 🚧 | [#225](https://github.com/IoTSharp/SonnetDB/issues/225)；适配提前拒绝，不返回虚假的 Serializable 保证 |
| DateTimeOffset 映射 | 🚧 | [#251](https://github.com/IoTSharp/SonnetDB/issues/251)；通用 reader 原生值类型不兼容 Chloe 转换器 |
| TimeSpan 时长映射 | 🚧 | [#252](https://github.com/IoTSharp/SonnetDB/issues/252)；TIME/TimeOnly 不等价于负数或超过 24 小时的时长 |
| DateTime 亚毫秒精度 | 🚧 | [#236](https://github.com/IoTSharp/SonnetDB/issues/236)；绑定与持久化 codec 均截断，适配拒绝有精度损失的输入 |
| 无主键关系表的写入 | 🚧 | [#253](https://github.com/IoTSharp/SonnetDB/issues/253)；明确拒绝，已有 keyless 只读映射不限制 |
| 显式 Column.Size / 有界字符串 DDL | 🚧 | [#240](https://github.com/IoTSharp/SonnetDB/issues/240)；不默默忽略显式长度约束，默认 STRING 无长度界限 |
| 显式 Decimal Precision/Scale 约束 | 🚧 | [#264](https://github.com/IoTSharp/SonnetDB/issues/264)；DDL 虽接受但不执行约束，建表时明确拒绝，默认 DECIMAL 无显式限制 |
| 显式 CAST(SUM/AVG/...) 等外层聚合转换 | 🚧 | [#254](https://github.com/IoTSharp/SonnetDB/issues/254)；普通聚合可用，显式转换不能伪装为已完成 |
| 表达式内逐行 Guid.NewGuid() | 🚧 | [#258](https://github.com/IoTSharp/SonnetDB/issues/258)；明确拒绝，避免客户端求值一次后各行相同；表达式树外生成后插入可用 |
| UInt64 按位与/或 | 🚧 | [#259](https://github.com/IoTSharp/SonnetDB/issues/259)；原生 DECIMAL 操作数不被整数位运算接受；有符号 Int64 位运算已验收 |
| 服务端 UInt64.Parse(string) | 🚧 | [#260](https://github.com/IoTSharp/SonnetDB/issues/260)；不能以 Decimal 解析接受非法小数文本；数值转 UInt64 的截断和表达式树外 CLR 解析已验收 |
| PostgreSQL DDL 的列注释能力 | 🚧 | [#237](https://github.com/IoTSharp/SonnetDB/issues/237)；没有 COMMENT ON COLUMN 合同 |
| 原始 ADO Decimal 参数精度 | 🚧 | [#235](https://github.com/IoTSharp/SonnetDB/issues/235)；ORM 翻译路径已保真，Session/SqlQuery 的原始 DbParam 仍走驱动缺陷路径 |
| 原始 ADO 远程 SByte/UInt16/UInt32 参数 | 🚧 | [#263](https://github.com/IoTSharp/SonnetDB/issues/263)；ORM 参数路径已无损扩大为 Int64，直接驱动参数绑定仍待修复 |
| 原生远程日期函数的 reader 类型/Kind 一致性 | 🚧 | [#262](https://github.com/IoTSharp/SonnetDB/issues/262)；ORM 用已有原生 TO_DATETIME 保持 SQL DATETIME 钟面值，不代表原始 ADO 元数据/Kind 已修复 |
| 原生 INSERT VALUES 标量表达式 | 🚧 | [#244](https://github.com/IoTSharp/SonnetDB/issues/244)；ORM 单条插入已通过 INSERT SELECT 等价实现，不阻塞该插入功能 |
| 核心动态查询过滤器与已有查询缓存 | 🚧 | [Chloe #434](https://github.com/shuxinqin/Chloe/issues/434)；现有 SQLite 也能重现，作为独立框架问题跟踪 |
| 远程 SonnetDB 的上述 ORM 能力 | ✅ | 正式 Server 4.0.0 的 REST 与显式 frame-http2 分别验收；原始驱动缺口仍单独列出 |
| Chloe.SonnetDB NuGet | 📦 | 待合并/发布；必须依赖包含回滚清理修复的新 Chloe 核心版本 |

**⏳ SonnetDB 发布等待项：目前上表数据库缺口尚待实现，没有已完成而仅等发布的项。** 将来必须确认修复提交、测试和正式包版本后更新状态；不能因 issue 关闭或 main 合入就标记正式包已经支持。

动态过滤器核心缺陷保持独立跟踪。本 PR 对 Chloe 核心只增加必要的回滚异常清理：提交失败已经终止事务时，回滚再次失败也释放内部状态与连接，后续命令与新事务可以继续使用；批量写入保留原提交异常及回滚异常。

**📦 发布门槛**：已发布的 Chloe `5.56.0` 不包含上述核心修复。正式发布时必须为包含修复的 Chloe 核心分配新版本，并将 `Chloe.SonnetDB` 的最低依赖版本同步到该版本。当前项目与 nuspec 的 `5.56.0` 是仓库开发基线，真实验收使用本 PR 的项目引用；不能把新适配包与已发布旧核心的组合当作已验收组合。

**日期合同**：映射的 DATETIME 与 SQL Server 常见 datetime 列一样使用钟面时间。Local 参数先保留 ticks、去掉时区标签，UTC/Unspecified 参数不改；日期函数通过原生 `TO_DATETIME` 使用同一合同。返回值的 `DateTime.Kind` 由驱动/协议决定，当前不承诺原标签往返。需要表示 UTC instant 的应用应明确传 UTC；需要完整 DateTimeOffset 合同的模型仍等待 #251。毫秒以外的精度由 #236 跟踪，适配不自动截断输入。原始 `Session` / `SqlQuery` 参数直接走驱动，其差异必须依照上表处理。

调用方可按自己的业务精度明确对齐输入，例如 `var now = DateTime.UtcNow; var milliseconds = new DateTime(now.Ticks - now.Ticks % TimeSpan.TicksPerMillisecond, now.Kind);`。这是调用方选择毫秒精度，不代表数据库具备 tick 精度。

Chloe 本身当前没有公开 UNION/UPSERT/迁移器等接口，不为对接添加这些接口；SonnetDB 的对应功能超出本次适配范围。

## 验收

```powershell
dotnet build src/Chloe.SonnetDB/Chloe.SonnetDB.csproj -c Release
dotnet test tests/Chloe.SonnetDB.Tests/Chloe.SonnetDB.Tests.csproj -c Release
dotnet pack src/Chloe.SonnetDB/Chloe.SonnetDB.csproj -c Release --no-build
```

测试引用正式 `SonnetDB` 包，禁止以本地源码引用或 mock 结果替代正式版本能力。数据库缺口测试必须明确断言拒绝行为、链接未完成 issue；它们验证诊断，不能标记对应能力为 ✅。

2026-10-09 的真实发布包验收结果：

| 环境 | ORM 能力验收 | 原生 SQL 探测 | 数据库缺口验证 | Chloe 既有缺陷验证 | 总计 / 失败 / 跳过 |
| --- | --- | --- | --- | --- | --- |
| 嵌入式 SonnetDB 4.0.0 | 72 | 7 | 19 | 2 | 100 / 0 / 0 |
| 正式 Server 4.0.0，HTTP/REST | 72 | 7 | 19 | 2 | 100 / 0 / 0 |
| 正式 Server 4.0.0，显式 frame-http2 | 72 | 7 | 19 | 2 | 100 / 0 / 0 |

远程使用官方 `sonnetdb-full-4.0.0-win-x64.zip`，校验 SHA256 为 `98d9c9f278a3a50d77bc9aafe778dd7af2ce6d34507305c59c3759f4b42c909c`。`frame-http2` 依照 4.0.0 驱动的实际路径验收：查询使用 Frame，写入使用 HTTP/2 上的 REST 路径；没有把自动回退计作 HTTP/2，也不宣称全部命令走原生 Frame 写协议。

TRX 记录在本机 `tests/Chloe.SonnetDB.Tests/TestResults/`，构建产物不提交；CI 保存嵌入式验收的 TRX。

随后补充同一拦截器测试的实体/表达式更新与删除，各回调、实际值、条件参数和影响行数均断言；修改后的测试在上述三模式分别复验 1/1 通过。最终五目标 Release build 通过（0 错误，202 个现有核心警告），pack 成功并检查五组 lib 资产与仅 Chloe 依赖；未发布包。

成功能力验收可以单独运行：

```powershell
dotnet test tests/Chloe.SonnetDB.Tests/Chloe.SonnetDB.Tests.csproj -c Release --filter "Category!=GapContract&Category!=FrameworkGapContract"
```

远程验收通过环境变量 `CHLOE_SONNETDB_CONNECTION_STRING` 使用发布版 Server。测试会重建测试表，只能指向专用测试数据库；凭据由运行环境提供，不能写进仓库。默认 HTTP/REST 连接与显式 `Protocol=frame-http2` 连接分别运行并记录结果，不能把自动协议回退算作 HTTP/2 证据。
