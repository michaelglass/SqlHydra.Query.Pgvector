module SqlHydra.Query.Pgvector.Tests.IntegrationTests

open System.Text.RegularExpressions
open System.Threading.Tasks
open Npgsql
open Pgvector
open Xunit
open Swensen.Unquote
open SqlHydra
open SqlHydra.Query
open SqlHydra.Query.Pgvector.PgvectorExtensions
open type SqlHydra.Query.Pgvector.PgvectorExtensions.PgvectorFn
open Testcontainers.PostgreSql

// ---------------------------------------------------------------------------
// Schema standing in for SqlHydra-generated table types. The emitter only needs
// the column NAMES to build SQL, so the CLR field types are incidental — we use
// `Pgvector.Vector` for the embedding so the shape mirrors a real generated table.
// We execute the compiled SQL via raw Npgsql (no generated HydraReader needed).
// ---------------------------------------------------------------------------
module ``public`` =

    [<CLIMutable>]
    type items =
        { [<ProviderDbType("Integer")>]
          id: int
          embedding: Vector }

    let items = table<items>

/// Real Postgres + pgvector via Testcontainers. Builds an NpgsqlDataSource with
/// UseVector(), creates the `items` table, and seeds known embeddings.
type PgvectorFixture() =
    let container = PostgreSqlBuilder("pgvector/pgvector:pg17").Build()

    let mutable dataSource: NpgsqlDataSource = null

    member _.DataSource = dataSource

    interface IAsyncLifetime with
        member _.InitializeAsync() : ValueTask =
            ValueTask(
                task {
                    do! container.StartAsync()

                    let builder = NpgsqlDataSourceBuilder(container.GetConnectionString())
                    builder.UseVector() |> ignore
                    dataSource <- builder.Build()

                    use! conn = dataSource.OpenConnectionAsync()

                    let exec (sql: string) =
                        task {
                            use cmd = new NpgsqlCommand(sql, conn)
                            let! _ = cmd.ExecuteNonQueryAsync()
                            return ()
                        }

                    do! exec "CREATE EXTENSION IF NOT EXISTS vector;"
                    // The data source cached its type catalogue before `vector` existed; reload
                    // so Npgsql/Pgvector can resolve the `vector` OID for parameter binding.
                    do! conn.ReloadTypesAsync()
                    do! exec "CREATE TABLE items (id int primary key, embedding vector(3));"

                    // Seed three rows with known embeddings along the axes.
                    let seed (id: int) (v: float32[]) =
                        task {
                            use cmd =
                                new NpgsqlCommand("INSERT INTO items (id, embedding) VALUES (@id, @e)", conn)

                            cmd.Parameters.AddWithValue("id", id) |> ignore
                            cmd.Parameters.AddWithValue("e", Vector(System.ReadOnlyMemory(v))) |> ignore
                            let! _ = cmd.ExecuteNonQueryAsync()
                            return ()
                        }

                    do! seed 1 [| 1.0f; 0.0f; 0.0f |]
                    do! seed 2 [| 0.0f; 1.0f; 0.0f |]
                    do! seed 3 [| 0.0f; 0.0f; 1.0f |]
                }
            )

        member _.DisposeAsync() : ValueTask =
            ValueTask(
                task {
                    if not (isNull dataSource) then
                        do! dataSource.DisposeAsync()

                    do! container.DisposeAsync().AsTask()
                }
            )

[<Trait("Category", "Integration")>]
type IntegrationTests(fixture: PgvectorFixture) =

    let emitter = PostgresEmitter() :> ISqlEmitter

    /// Execute SqlHydra-compiled SQL + parameters against the container.
    /// SqlHydra emits positional `?` placeholders; rewrite them to @pN, bind in order,
    /// then project each row via `read`.
    let executeReader (sql: string) (parameters: (string * obj) list) (read: NpgsqlDataReader -> 'T) =
        task {
            use! conn = fixture.DataSource.OpenConnectionAsync()
            use cmd = new NpgsqlCommand(sql, conn)

            parameters
            |> List.iteri (fun i (_, value) -> cmd.Parameters.AddWithValue(sprintf "p%d" i, value) |> ignore)

            let mutable idx = -1

            cmd.CommandText <-
                Regex.Replace(
                    sql,
                    @"\?",
                    (fun _ ->
                        idx <- idx + 1
                        sprintf "@p%d" idx)
                )

            use! reader = cmd.ExecuteReaderAsync()
            let npgReader = reader :?> NpgsqlDataReader
            let results = System.Collections.Generic.List<'T>()
            let mutable more = true

            while more do
                let! hasNext = reader.ReadAsync()

                if hasNext then
                    results.Add(read npgReader)
                else
                    more <- false

            return List.ofSeq results
        }

    let compile (q: SelectQuery) =
        let c = q.CompileWith(emitter)
        c.Sql, c.Parameters

    interface IClassFixture<PgvectorFixture>

    [<Fact>]
    [<Trait("Category", "Integration")>]
    member _.``cosine_distance to self is approximately zero``() =
        task {
            // SELECT cosine_distance(embedding, embedding) FROM items  →  embedding <=> embedding
            let q =
                select {
                    for i in ``public``.items do
                        select (cosine_distance (i.embedding, i.embedding))
                }

            let sql, ps = compile q
            let! distances = executeReader sql ps (fun r -> r.GetDouble(0))

            distances.Length =! 3

            for d in distances do
                // Distance of any vector to itself must be ~0.
                test <@ abs d < 1e-5 @>
        }

    [<Fact>]
    [<Trait("Category", "Integration")>]
    member _.``orderByCosineDistance returns the nearest row first``() =
        task {
            // Query vector closest (cosine) to row 2's embedding [0,1,0].
            let queryVec = Vector(System.ReadOnlyMemory([| 0.0f; 0.9f; 0.1f |]))

            let q =
                select {
                    for i in ``public``.items do
                        orderByCosineDistance i.embedding (box queryVec)
                        select i.id
                        take 3
                }

            let sql, ps = compile q
            let! ids = executeReader sql ps (fun r -> r.GetInt32(0))

            ids.Length =! 3
            // Row 2 ([0,1,0]) is nearest in cosine distance to [0,0.9,0.1].
            ids.Head =! 2
        }

    [<Fact>]
    [<Trait("Category", "Integration")>]
    member _.``orderByL2Distance returns the nearest row first``() =
        task {
            // Query vector closest (L2) to row 3's embedding [0,0,1].
            let queryVec = Vector(System.ReadOnlyMemory([| 0.1f; 0.1f; 0.95f |]))

            let q =
                select {
                    for i in ``public``.items do
                        orderByL2Distance i.embedding (box queryVec)
                        select i.id
                        take 3
                }

            let sql, ps = compile q
            let! ids = executeReader sql ps (fun r -> r.GetInt32(0))

            ids.Length =! 3
            // Row 3 ([0,0,1]) is nearest in L2 distance to [0.1,0.1,0.95].
            ids.Head =! 3
        }

    [<Fact>]
    [<Trait("Category", "Integration")>]
    member _.``orderByInnerProductDistance returns the highest-inner-product row first``() =
        task {
            // pgvector's `<#>` returns the NEGATED inner product, so an ascending sort by it
            // ranks the row with the LARGEST inner product (greatest similarity) first.
            // Inner products with the axis-aligned rows just pick out each component:
            //   row 1 [1,0,0] -> 0.2,  row 2 [0,1,0] -> 0.9,  row 3 [0,0,1] -> 0.3.
            // Largest is row 2, so it must come back first.
            let queryVec = Vector(System.ReadOnlyMemory([| 0.2f; 0.9f; 0.3f |]))

            let q =
                select {
                    for i in ``public``.items do
                        orderByInnerProductDistance i.embedding (box queryVec)
                        select i.id
                        take 3
                }

            let sql, ps = compile q
            let! ids = executeReader sql ps (fun r -> r.GetInt32(0))

            ids.Length =! 3
            // Row 2 has the largest inner product with the query vector.
            ids.Head =! 2
            // Full sign-inverted ordering: row 2 (0.9) > row 3 (0.3) > row 1 (0.2).
            ids =! [ 2; 3; 1 ]
        }
