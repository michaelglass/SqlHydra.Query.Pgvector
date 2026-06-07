# SqlHydra.Query.Pgvector

<!-- sync:intro:start -->
[pgvector](https://github.com/pgvector/pgvector) distance functions for
[SqlHydra.Query](https://github.com/JordanMarr/SqlHydra) — cosine, L2, and inner-product
distance operators for `select` projections and `ORDER BY`, plus a code-generation type
mapping for the PostgreSQL `vector` column type.
<!-- sync:intro:end -->

> **Status: depends on the [michaelglass/SqlHydra fork](https://github.com/michaelglass/SqlHydra/tree/feature/postgres-extension-v4) until/unless the remaining pieces land upstream.** The build pins the fork via a Paket git dependency (see `paket.dependencies`); `SqlHydra.Query` needs the infix-operator extensibility seam and parameterized `OrderByRaw` from the [features](https://github.com/michaelglass/SqlHydra/tree/feature/postgres-features-v4) + [extension](https://github.com/michaelglass/SqlHydra/tree/feature/postgres-extension-v4) PRs (split from JordanMarr/SqlHydra#125; the first PR, JordanMarr/SqlHydra#129, is merged). Once those ship on NuGet this flips to a normal `PackageReference` and the package gets published.

## Installation

```bash
dotnet add package SqlHydra.Query.Pgvector
```

## Building

This repo pins `SqlHydra.Query` / `SqlHydra.Domain` to a fork commit via a Paket
git dependency (see `paket.dependencies`), so restore the fork before building:

```bash
dotnet tool restore
dotnet paket restore
dotnet build SqlHydra.Query.Pgvector.slnx
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

## Code generation: mapping the `vector` column type

To have `dotnet sqlhydra` map PostgreSQL `vector` columns to `Pgvector.Vector`, register
the included `IExtendTypeMapping` extension in your TOML:

```toml
[extensions]
type_mappings = ["SqlHydra.Query.Pgvector"]
```

SqlHydra discovers the `PgvectorTypeMapping` implementation in the referenced assembly and
composes it ahead of the built-in mappings.

## License

MIT
