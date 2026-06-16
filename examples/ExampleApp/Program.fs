// sync:usage-opens:start
open SqlHydra.Query
open SqlHydra.Query.Pgvector.PgvectorExtensions
open type SqlHydra.Query.Pgvector.PgvectorExtensions.PgvectorFn
// sync:usage-opens:end

open SqlHydra

// In a real app this record + table value are produced by `dotnet sqlhydra`.
// A `vector` column maps to `Pgvector.Vector` when you register the
// `SqlHydra.Query.Pgvector` type-mapping extension in your TOML. Here we use
// plain numeric stand-ins, since the emitter only cares about column references.
[<CLIMutable>]
type document =
    { [<ProviderDbType("Integer")>]
      id: int
      [<ProviderDbType("Text")>]
      content: string
      [<ProviderDbType("Money")>]
      embedding: decimal // stand-in for a `Pgvector.Vector` column
      [<ProviderDbType("Money")>]
      centroid: decimal } // a second `Pgvector.Vector` column to compare against

let documents = table<document>

let emitter = PostgresEmitter() :> ISqlEmitter

let sqlOf (query: SelectQuery) = (query.CompileWith emitter).Sql

// `queryVector` is your search embedding — the `Pgvector.Vector` you want to
// find the nearest rows to. It's bound as a query parameter in the orderBy path.
let queryVector = box [| 0.1f; 0.2f; 0.3f |]

// --- README usage example (column-vs-column select + nearest-neighbour orderBy) --
// This region is sourced verbatim into README.md via syncdocs `src=`, so it can
// never drift from the real pgvector API.

// sync:usage-queries:start
// Distance between two vector columns (e.g. how far each document is from a
// cluster centroid). Both arguments must be column references:
let centroidDistance =
    select {
        for d in documents do
            select (cosine_distance (d.embedding, d.centroid))
    }

// Find the 10 closest documents to your query vector (nearest-neighbour search).
// The query vector is bound as a parameter, so it's safe to pass user input:
let nearest =
    select {
        for d in documents do
            orderByCosineDistance d.embedding queryVector
            take 10
    }
// sync:usage-queries:end

printfn "cosine_distance (column-vs-column) select SQL:\n%s\n" (sqlOf centroidDistance)

let compiledNearest = nearest.CompileWith emitter
printfn "orderByCosineDistance SQL:\n%s" compiledNearest.Sql
printfn "  parameters: %d\n" compiledNearest.Parameters.Length

// --- The remaining distance functions, exercised for completeness ------------

let l2Select =
    select {
        for d in documents do
            select (l2_distance (d.embedding, d.centroid))
    }

printfn "l2_distance select SQL:\n%s\n" (sqlOf l2Select)

let innerProductSelect =
    select {
        for d in documents do
            select (inner_product_distance (d.embedding, d.centroid))
    }

printfn "inner_product_distance select SQL:\n%s\n" (sqlOf innerProductSelect)

let nearestL2 =
    select {
        for d in documents do
            orderByL2Distance d.embedding queryVector
            take 10
    }

let compiledL2 = nearestL2.CompileWith emitter
printfn "orderByL2Distance SQL:\n%s" compiledL2.Sql
printfn "  parameters: %d\n" compiledL2.Parameters.Length

let nearestInnerProduct =
    select {
        for d in documents do
            orderByInnerProductDistance d.embedding queryVector
            take 10
    }

let compiledInnerProduct = nearestInnerProduct.CompileWith emitter
printfn "orderByInnerProductDistance SQL:\n%s" compiledInnerProduct.Sql
printfn "  parameters: %d" compiledInnerProduct.Parameters.Length
