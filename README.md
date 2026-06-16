# SqlHydra.Query.Pgvector

<!-- sync:intro:start -->
Vector similarity search for [SqlHydra.Query](https://github.com/JordanMarr/SqlHydra),
powered by [pgvector](https://github.com/pgvector/pgvector).

The goal is to let you write `select` and `ORDER BY` queries that compare embeddings by
**cosine**, **L2 (Euclidean)**, or **inner-product** distance — all in strongly-typed F#,
with the native pgvector operators (`<=>`, `<->`, `<#>`) generated for you. It also aims to
teach the SqlHydra code generator about `vector` columns so they come through as
`Pgvector.Vector` in your generated types.
<!-- sync:intro:end -->

> **Status:** early alpha, and substantially AI-written. Behavior and APIs may shift
> between versions, so your mileage may vary. Issues and PRs are very welcome.

## Before you start

You'll need:

- A PostgreSQL database with the [pgvector](https://github.com/pgvector/pgvector)
  extension enabled (`CREATE EXTENSION vector;`) and a table with a `vector` column.
- [SqlHydra.Query](https://github.com/JordanMarr/SqlHydra) set up for that database. If
  you're new to SqlHydra, start with its docs — this package just adds vector search on
  top of the queries you already write.

## Installation

```bash
dotnet add package SqlHydra.Query.Pgvector
```

## Searching by similarity

Open the package alongside `SqlHydra.Query`, then use the distance functions in a query.
`queryVector` below is your search embedding — the `Pgvector.Vector` you want to find the
nearest rows to.

```fsharp
open SqlHydra.Query
open SqlHydra.Query.Pgvector.PgvectorExtensions
open type SqlHydra.Query.Pgvector.PgvectorExtensions.PgvectorFn

// Return each document together with how far it is from your query vector:
select {
    for d in documents do
        select (cosine_distance (d.embedding, queryVector))
}

// Find the 10 closest documents (nearest-neighbour search):
select {
    for d in documents do
        orderByCosineDistance d.embedding (box queryVector)
        take 10
}
```

That should be it — no setup or registration call needed.

### Available distance functions

Use these inside `select` to get a distance back as a column:

| Function | Distance |
|---|---|
| `cosine_distance(a, b)` | Cosine |
| `l2_distance(a, b)` | L2 / Euclidean |
| `inner_product_distance(a, b)` | Inner product |

These emit the infix operator between **two columns** (e.g. `embedding <=> other_embedding`).
Both arguments must be column references. Passing a literal `Pgvector.Vector` (or array) as
the second argument is **not** supported in a `select` projection — SqlHydra fails fast at
compile time rather than inlining the value. To rank rows against a query vector, use the
`orderBy*Distance` operations below, which bind the vector as a parameter.

Use these to order results from closest to farthest:

| Operation | Distance |
|---|---|
| `orderByCosineDistance col vec` | Cosine |
| `orderByL2Distance col vec` | L2 / Euclidean |
| `orderByInnerProductDistance col vec` | Inner product |

In the `orderBy*Distance` path the query vector is always sent as a query parameter, so it's
safe to pass user input.

## Generating types for `vector` columns

So that SqlHydra generates a `Pgvector.Vector` property for each `vector` column, add this
package to the `[extensions]` section of your SqlHydra generator TOML:

```toml
[extensions]
type_mappings = ["SqlHydra.Query.Pgvector"]
```

Re-run `dotnet sqlhydra` and your `vector` columns should come through as `Pgvector.Vector`.

## Building this project

Tasks are driven by [`mise`](https://mise.jdx.dev/):

```bash
mise run build    # build the solution
mise run test     # run all tests (the integration tests need Docker)
mise run ci       # the full gate: format check + build + lint + test
mise run format   # format with Fantomas
```

The integration tests spin up a real PostgreSQL + pgvector container via
[Testcontainers](https://testcontainers.com/), so they need a running Docker daemon. To
run only the in-process unit tests without Docker:

```bash
dotnet test --solution SqlHydra.Query.Pgvector.slnx \
    --filter-not-trait "Category=Integration"
```

## License

MIT
