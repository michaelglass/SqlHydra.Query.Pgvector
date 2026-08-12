// sync:usage-opens:start
open SqlHydra.Query
open SqlHydra.Query.Pgvector.PgvectorExtensions
open type SqlHydra.Query.Pgvector.PgvectorExtensions.PgvectorFn
// sync:usage-opens:end

open SqlHydra

// In a real app `dotnet sqlhydra` generates this record, mapping a `vector` column to
// `Pgvector.Vector` once the type-mapping extension is registered in your TOML. Plain
// numeric stand-ins work here because the emitter only cares about column references.
[<CLIMutable>]
type document =
    { [<ProviderDbType("Integer")>]
      id: int
      [<ProviderDbType("Text")>]
      content: string
      [<ProviderDbType("Money")>]
      embedding: decimal
      [<ProviderDbType("Money")>]
      centroid: decimal } // a second vector column, to compare each embedding against

let documents = table<document>

let emitter = PostgresEmitter() :> ISqlEmitter

let sqlOf (query: SelectQuery) = (query.CompileWith emitter).Sql

// Your search embedding — the `Pgvector.Vector` you want the nearest rows to.
let queryVector = box [| 0.1f; 0.2f; 0.3f |]

// The region below is sourced verbatim into README.md via syncdocs `src=`; edits here
// (comments included) change the README.

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
