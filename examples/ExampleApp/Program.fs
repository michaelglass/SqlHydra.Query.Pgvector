open SqlHydra
open SqlHydra.Query
open SqlHydra.Query.Pgvector.PgvectorExtensions
open type SqlHydra.Query.Pgvector.PgvectorExtensions.PgvectorFn

// In a real app this record + table value are produced by `dotnet sqlhydra`.
// A `vector` column maps to `Pgvector.Vector` when you register the
// `SqlHydra.Query.Pgvector` type-mapping extension in your TOML.
[<CLIMutable>]
type document =
    { [<ProviderDbType("Integer")>]
      id: int
      [<ProviderDbType("Text")>]
      content: string
      [<ProviderDbType("Money")>]
      embedding: decimal } // stand-in for a `Pgvector.Vector` column

let documents = table<document>

let emitter = PostgresEmitter() :> ISqlEmitter

let sqlOf (query: SelectQuery) = (query.CompileWith emitter).Sql

// --- Distance functions as projected columns -------------------------------

let cosineSelect =
    select {
        for d in documents do
            select (cosine_distance (d.embedding, d.embedding))
    }

printfn "cosine_distance select SQL:\n%s\n" (sqlOf cosineSelect)

let l2Select =
    select {
        for d in documents do
            select (l2_distance (d.embedding, d.embedding))
    }

printfn "l2_distance select SQL:\n%s\n" (sqlOf l2Select)

let innerProductSelect =
    select {
        for d in documents do
            select (inner_product_distance (d.embedding, d.embedding))
    }

printfn "inner_product_distance select SQL:\n%s\n" (sqlOf innerProductSelect)

// --- ORDER BY nearest-neighbour with the query vector bound as a parameter --

let queryVector = box [| 0.1f; 0.2f; 0.3f |]

let nearestCosine =
    select {
        for d in documents do
            orderByCosineDistance d.embedding queryVector
            take 10
    }

let compiledCosine = nearestCosine.CompileWith emitter
printfn "orderByCosineDistance SQL:\n%s" compiledCosine.Sql
printfn "  parameters: %d\n" compiledCosine.Parameters.Length

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
