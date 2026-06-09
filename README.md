# SqlHydra.Query.Pgvector

<!-- sync:intro:start -->
[pgvector](https://github.com/pgvector/pgvector) distance functions for
[SqlHydra.Query](https://github.com/JordanMarr/SqlHydra) — cosine, L2, and inner-product
distance operators for `select` projections and `ORDER BY`. A runtime-only package;
mapping the PostgreSQL `vector` column type during code generation is a documented
copy-paste snippet (see below) until SqlHydra's codegen types ship standalone.
<!-- sync:intro:end -->

> **Status: depends on the published [`SqlHydra.Query`](https://www.nuget.org/packages/SqlHydra.Query) (≥ `4.1.0-beta.1`, currently a prerelease).** That release ships the runtime extensibility seam this package needs — the `SqlHydraInfixOperator` assembly attribute + `InfixOperators` registry, `sqlFn`, parameterized `OrderByRaw`, and the public `tryGetOrderByColumn` helper. No fork or Paket pin: a plain `dotnet build` works.

## Installation

```bash
dotnet add package SqlHydra.Query.Pgvector
```

## Query usage

```fsharp
open SqlHydra.Query
open SqlHydra.Query.Pgvector.PgvectorExtensions
open type SqlHydra.Query.Pgvector.PgvectorExtensions.PgvectorFn

// Distance as a projected column — emits `embedding <=> @p0`:
select {
    for d in documents do
        select (cosine_distance (d.embedding, queryVector))
}

// Nearest-neighbour ordering — emits `ORDER BY "d"."embedding" <=> ?`,
// with the query vector bound as a parameter:
select {
    for d in documents do
        orderByCosineDistance d.embedding (box queryVector)
        take 10
}
```

| Function / operation | Operator | Meaning |
|---|---|---|
| `cosine_distance(a, b)` | `<=>` | cosine distance (select projection) |
| `l2_distance(a, b)` | `<->` | L2 / Euclidean distance (select projection) |
| `inner_product_distance(a, b)` | `<#>` | inner-product distance (select projection) |
| `orderByCosineDistance col vec` | `<=>` | `ORDER BY col <=> @vec` (ascending — closest first) |
| `orderByL2Distance col vec` | `<->` | `ORDER BY col <-> @vec` |
| `orderByInnerProductDistance col vec` | `<#>` | `ORDER BY col <#> @vec` |

The operators self-register the first time a query is compiled, via assembly-level
`[<assembly: SqlHydraInfixOperator(...)>]` attributes — there is no `ensureRegistered()`
startup call.

## Code generation — mapping the `vector` column

This package is **runtime-only** — it does not ship a code-generation type mapping, because
the codegen extensibility types (`SqlHydra.Domain.IExtendTypeMapping`) are not yet published
as a standalone NuGet package (they ship compiled into `SqlHydra.Cli`, not as a referenceable
package). Until that gap closes, add the `IExtendTypeMapping` to **your own** SqlHydra.Query
project and register it in your generator TOML.

1. Drop this type into your SqlHydra.Query project (it compiles against the `SqlHydra.Domain`
   types bundled with `SqlHydra.Query`):

```fsharp
namespace MyApp.SqlHydra

open System.Data
open SqlHydra.Domain

/// Maps the PostgreSQL `vector` column type (pgvector) to `Pgvector.Vector`
/// during `dotnet sqlhydra` generation.
type PgvectorTypeMapping() =
    interface IExtendTypeMapping with
        member _.Extend(baseTryFind) =
            fun (ctx: TypeMappingContext) ->
                match ctx.Column.ProviderTypeName.ToLower() with
                | "vector" ->
                    Some
                        { TypeMapping.ColumnTypeAlias = "vector"
                          TypeMapping.ClrType = "Pgvector.Vector"
                          TypeMapping.DbType = DbType.Object
                          // Must be None: SqlHydra applies ProviderDbType via
                          // Enum.Parse<NpgsqlDbType>, and NpgsqlDbType has no Vector member.
                          // pgvector binds through the Pgvector.Npgsql plugin (UseVector()),
                          // which infers the handler from the Pgvector.Vector value itself.
                          TypeMapping.ProviderDbType = None }
                | _ -> baseTryFind ctx
```

2. Register it in your generator TOML so `dotnet sqlhydra` applies it (use the assembly /
   namespace that contains your type):

```toml
[extensions]
type_mappings = ["MyApp.SqlHydra"]
```

SqlHydra discovers `IExtendTypeMapping` implementations in the referenced assembly
automatically and composes them ahead of the built-in mappings, so a `vector` column then
generates a `Pgvector.Vector` property.

## License

MIT
