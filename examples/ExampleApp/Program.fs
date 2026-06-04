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

// SELECT cosine distance as a projected column.
let distanceQuery =
    select {
        for d in documents do
            select (cosine_distance (d.embedding, d.embedding))
    }

printfn "distance SQL:\n%s\n" ((distanceQuery.CompileWith emitter).Sql)

// ORDER BY nearest-neighbour with the query vector bound as a parameter.
let queryVector = box [| 0.1f; 0.2f; 0.3f |]

let nearestQuery =
    select {
        for d in documents do
            orderByCosineDistance d.embedding queryVector
            take 10
    }

let compiled = nearestQuery.CompileWith emitter
printfn "nearest SQL:\n%s" compiled.Sql
printfn "parameters: %d" compiled.Parameters.Length
