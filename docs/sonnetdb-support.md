# SonnetDB provider: 5.0 contracts

`Chloe.SonnetDB` uses SonnetDB's native ADO.NET driver. The verified database dependency is **SonnetDB 5.0.0** on .NET 10, with matching Core/Protocol and Server artifacts. Other compiled Chloe target frameworks do not establish an older-runtime SonnetDB installation.

```csharp
var factory = new DbConnectionFactory(() =>
    new SndbConnection("Data Source=./demo-db"));
using var db = new SonnetDBContext(factory);
```

Use `Protocol=rest` or `Protocol=frame-http2` with the Server's matching endpoint for remote connections. The driver uses native Frame for supported requests and HTTP/2 REST for applicable transaction/lifecycle requests; it does not silently fall back to HTTP/1 when HTTP/2 is explicitly selected.

## Capability matrix

| Existing Chloe capability | 5.0 provider mapping |
| --- | --- |
| Entity/expression CRUD, async, identity-only insertion | Native INSERT/SELECT/UPDATE/DELETE and RETURNING; command/context interceptors remain observable |
| Bounded batching and transactions | Existing parameter/batch limits, outer-transaction behavior and complete failed-batch rollback |
| Schema-qualified tables | Structured quoted schema/table identity for DDL, queries and batch writes |
| Sequence mapping and Sql.NextValueForSequence | Native persistent nextval, per-row generation and RETURNING; no client-side counters |
| LockType.UpdLock | FOR UPDATE on the direct source of an eligible single-table query inside a real transaction |
| Isolation | ReadCommitted and Serializable; unsupported levels remain explicit errors |
| Exact Decimal and UInt64 | Exact Decimal values, explicit DECIMAL(p,s) validation, unsigned decimal representation, bit_and_uint64 / bit_or_uint64 / parse_uint64 |
| DateTime | DATETIME2 and typed DateTime2 parameters preserve 100ns ticks; the adapter preserves CLR wall-clock values for Local inputs |
| DateTimeOffset | Native DATETIMEOFFSET and the existing Chloe converter; complete UTC ticks with zero returned offset |
| TimeSpan and date subtraction | Native DURATION supports negative and full signed ticks; native TimeSpan results |
| Guid.NewGuid in SQL expressions | Native uuid() evaluated independently per row/expression |
| Explicit string size | STRING(n) declaration with server-enforced overflow rejection |
| Keyless models | Native writes preserve duplicates and NULL without inventing a business key |
| Aggregate CAST and native date functions | Server-side expressions with native declaration metadata; no client-side filtering |
| XML column descriptions | COMMENT ON COLUMN generated from the existing assembly XML documentation, escaping quotes and honoring overridden physical table names |
| Navigation, joins, grouping, sharding, tracking, concurrency | Existing framework paths; database-specific regression tests exercise their native operations |

## Preserved boundaries

- NoLock remains rejected: an ordinary ReadCommitted query is not a dirty read. Join, DISTINCT, grouping and non-direct query sources with UpdLock remain rejected.
- Sequence names occupy the database-wide sequence namespace. Explicit schema-qualified sequences remain unsupported; rollback may consume sequence numbers as documented by SonnetDB.
- Native legacy DATETIME / DbType.DateTime has a millisecond contract and rejects lossy submillisecond bindings. New default DateTime mappings use DATETIME2; an explicitly customized legacy column must use compatible parameters.
- DateTimeOffset does not preserve an arbitrary input offset; values represent the same UTC instant with offset zero. Mapping support does not imply every CLR member/method has an SQL translation.
- Both Precision and Scale must be supplied for explicit Decimal metadata; unsupported or contradictory declarations are rejected.
- Upstream Chloe 5.57 fixes the dynamic-filter/query-cache defect tracked by Chloe #434. The two framework regression tests require a filter added after an unfiltered cached query to affect the next result, both with SonnetDB and with SQLite. Earlier 5.56 defect reproductions remain historical evidence.
- SonnetDB's storage upgrade/downgrade contract and offline backup requirements remain in force. These provider tests do not establish physical power-loss, arbitrary production-load or clean-machine installation acceptance.

## Verification and delivery

The SonnetDB-specific test project is `tests/Chloe.SonnetDB.Tests`. Its release-contract tests assert the repaired positive behavior, native constraints and explicit unsupported boundaries. Set `CHLOE_SONNETDB_CONNECTION_STRING` for a real remote Server, and run the complete project in embedded, REST and explicit HTTP/2 modes. Inspect TRX counts so a missing test adapter or skipped test cannot count as a passing gate.

The provider uses matching Chloe core/provider source versions of at least 5.57 and depends on the core rollback/session cleanup included by this PR. The core and adapter must be released together with compatible version metadata; the already published core does not acquire those additional rollback changes from the provider alone. The final release version is assigned when those core changes ship. Upstream merge, stable NuGet publication and public-package consumer verification remain distinct release gates.
