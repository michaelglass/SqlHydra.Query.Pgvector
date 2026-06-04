namespace SqlHydra.Query.Pgvector.Tests

open SqlHydra
open SqlHydra.Query

/// Minimal self-contained schema standing in for SqlHydra-generated table types.
/// Mirrors the generated shape: a `[<CLIMutable>]` record with `[<ProviderDbType>]`
/// columns plus a `table<_>` value. We reuse plain numeric columns as distance-op
/// operands — the emitter only cares about the column references, not the CLR types.
module production =

    [<CLIMutable>]
    type product =
        { [<ProviderDbType("Integer")>]
          productid: int
          [<ProviderDbType("Varchar")>]
          name: string
          [<ProviderDbType("Money")>]
          standardcost: decimal
          [<ProviderDbType("Money")>]
          listprice: decimal }

    let product = table<product>

[<AutoOpen>]
module Helpers =

    let private emitter = PostgresEmitter() :> ISqlEmitter

    /// Compile a select query to its PostgreSQL SQL string.
    let toSql (query: SelectQuery) = (query.CompileWith(emitter)).Sql
