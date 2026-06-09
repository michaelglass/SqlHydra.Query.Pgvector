# SqlHydra.Query.Pgvector

<!-- sync:intro:start -->
[pgvector](https://github.com/pgvector/pgvector) distance functions for
[SqlHydra.Query](https://github.com/JordanMarr/SqlHydra) — cosine, L2, and inner-product
distance operators for `select` projections and `ORDER BY`, plus a code-generation type
mapping (`PgvectorTypeMapping`) that maps the PostgreSQL `vector` column type to
`Pgvector.Vector` during `dotnet sqlhydra` generation. Register it in your generator TOML
(see below).
<!-- sync:intro:end -->

> **Status: depends on the published [`SqlHydra.Query`](https://www.nuget.org/packages/SqlHydra.Query) (≥ `4.1.0-beta.1`, currently a prerelease).** That release ships the runtime extensibility seam this package needs — the `SqlHydraInfixOperator` assembly attribute + `InfixOperators` registry, `sqlFn`, parameterized `OrderByRaw`, and the public `tryGetOrderByColumn` helper — and bundles the `SqlHydra.Domain` codegen types this package's `PgvectorTypeMapping` implements against. No fork or Paket pin: a plain `dotnet build` works.

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

This package **ships** a code-generation type mapping — `PgvectorTypeMapping`, in the
`SqlHydra.Query.Pgvector` namespace — that maps the PostgreSQL `vector` column type to
`Pgvector.Vector` during `dotnet sqlhydra` generation. You don't copy any code; just
reference the package and register it in your generator TOML:

```toml
[extensions]
type_mappings = ["SqlHydra.Query.Pgvector"]
```

SqlHydra discovers the `IExtendTypeMapping` implementation in the referenced assembly
automatically and composes it ahead of the built-in mappings, so a `vector` column then
generates a `Pgvector.Vector` property.

The mapping sets `ProviderDbType = None` on purpose: SqlHydra applies `ProviderDbType` via
`Enum.Parse<NpgsqlDbType>`, and `NpgsqlDbType` has no `Vector` member. pgvector binds through
the `Pgvector.Npgsql` plugin (`UseVector()`), which infers the handler from the
`Pgvector.Vector` value itself.

## License

MIT
