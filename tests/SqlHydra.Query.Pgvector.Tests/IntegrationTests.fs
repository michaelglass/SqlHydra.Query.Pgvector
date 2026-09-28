module SqlHydra.Query.Pgvector.Tests.IntegrationTests

open System
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

// Schema standing in for SqlHydra-generated table types, in the shapes `PgvectorTypeMapping` and
// the generator emit (an `Option` per nullable column, a left-view per table).
module ``public`` =

    [<CLIMutable>]
    type items =
        { [<ProviderDbType("Integer")>]
          id: int
          embedding: Vector }

    let items = table<items>

    [<CLIMutable>]
    type shapes =
        { [<ProviderDbType("Integer")>]
          id: int
          half: Option<HalfVector>
          sparse: Option<SparseVector>
          many: Option<Vector[]> }

    let shapes = table<shapes>

    [<CLIMutable>]
    type labels =
        { [<ProviderDbType("Integer")>]
          id: int
          embedding: Vector }

    let labels = table<labels>

    module LeftJoined =
        type private ``labels (base)`` = labels

        [<CLIMutable; NoEquality; NoComparison>]
        type labels =
            { [<ProviderDbType("Integer")>]
              id: Option<int>
              embedding: Option<Vector> }

            interface ILeftViewOf<``labels (base)``>

        let labels = leftTable<``labels (base)``, labels>

/// Real Postgres + pgvector, seeded with known embeddings.
///
/// `PGVECTOR_TEST_SERVER`, when set, is a connection string to a running server with the pgvector
/// extension available: the fixture creates a scratch database on it and drops it afterwards.
/// Otherwise it starts a Testcontainers container, which needs Docker.
type PgvectorFixture() =
    let server = Environment.GetEnvironmentVariable "PGVECTOR_TEST_SERVER"

    let container =
        if String.IsNullOrEmpty server then
            Some(PostgreSqlBuilder("pgvector/pgvector:pg17").Build())
        else
            None

    let scratchDatabase = $"pgvector_test_{Guid.NewGuid():N}"

    let onServer (sql: string) =
        task {
            use conn = new NpgsqlConnection(server)
            do! conn.OpenAsync()
            use cmd = new NpgsqlCommand(sql, conn)
            let! _ = cmd.ExecuteNonQueryAsync()
            return ()
        }

    let mutable dataSource: NpgsqlDataSource = null

    member _.DataSource = dataSource

    /// Runs a select through SqlHydra's own reader, so the rows are hydrated as generated code would be.
    member _.Context =
        ContextType.CreateTask(fun () ->
            task {
                let! conn = dataSource.OpenConnectionAsync()
                return new QueryContext(conn, PostgresEmitter())
            })

    interface IAsyncLifetime with
        member _.InitializeAsync() : ValueTask =
            ValueTask(
                task {
                    let! connectionString =
                        match container with
                        | Some container ->
                            task {
                                do! container.StartAsync()
                                return container.GetConnectionString()
                            }
                        | None ->
                            task {
                                do! onServer $"CREATE DATABASE {scratchDatabase}"

                                return
                                    NpgsqlConnectionStringBuilder(server, Database = scratchDatabase).ConnectionString
                            }

                    let builder = NpgsqlDataSourceBuilder(connectionString)
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

                    do!
                        exec
                            "CREATE TABLE labels (id int primary key, embedding vector(3));
                             INSERT INTO labels VALUES (1, '[1,0,0]'), (3, '[0,0,1]');"

                    do!
                        exec
                            "CREATE TABLE shapes (id int primary key, half halfvec(3), sparse sparsevec(5), many vector(3)[]);
                             INSERT INTO shapes VALUES
                                 (1, '[1,0,0]', '{1:1,4:2}/5', ARRAY['[1,0,0]', '[0,1,0]']::vector[]),
                                 (2, NULL, NULL, NULL);"
                }
            )

        member _.DisposeAsync() : ValueTask =
            ValueTask(
                task {
                    if not (isNull dataSource) then
                        do! dataSource.DisposeAsync()

                    match container with
                    | Some container -> do! container.DisposeAsync().AsTask()
                    | None -> do! onServer $"DROP DATABASE IF EXISTS {scratchDatabase} WITH (FORCE)"
                }
            )

[<Trait("Category", "Integration")>]
type IntegrationTests(fixture: PgvectorFixture) =

    let emitter = PostgresEmitter() :> ISqlEmitter

    /// Execute SqlHydra-compiled SQL + parameters against the database.
    /// SqlHydra emits positional `?` placeholders; Npgsql needs named ones, so they are
    /// rewritten to @pN and bound in order.
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
            let q =
                select {
                    for i in ``public``.items do
                        select (cosine_distance (i.embedding, i.embedding))
                }

            let sql, ps = compile q
            let! distances = executeReader sql ps (fun r -> r.GetDouble(0))

            distances.Length =! 3

            for d in distances do
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
            ids.Head =! 2
            ids =! [ 2; 3; 1 ]
        }

    [<Fact>]
    [<Trait("Category", "Integration")>]
    member _.``orderByCosineDistance orders by a left-view column, unmatched rows last``() =
        task {
            let queryVec = Vector(System.ReadOnlyMemory([| 0.0f; 0.1f; 0.9f |]))

            let! rows =
                selectTask fixture.Context {
                    for i in ``public``.items do
                        leftJoin' l in ``public``.LeftJoined.labels
                        on' (Some i.id = l.id)
                        orderByCosineDistance l.embedding (box queryVec)
                        select (i.id, l.embedding)
                }

            (rows |> Seq.map (fun (id, e) -> id, e |> Option.map string) |> List.ofSeq)
            =! [ 3, Some "[0,0,1]"; 1, Some "[1,0,0]"; 2, None ]
        }

    [<Fact>]
    [<Trait("Category", "Integration")>]
    member _.``a distance projected from a left-view column reads once unmatched rows are filtered out``() =
        task {
            // A distance is a `float`: an unmatched row's NULL distance could not be read into one.
            let! rows =
                selectTask fixture.Context {
                    for i in ``public``.items do
                        leftJoin' l in ``public``.LeftJoined.labels
                        on' (Some i.id = l.id)
                        where (l.embedding <> None)
                        orderBy i.id
                        select (i.id, cosine_distance (i.embedding, l.embedding))
                }

            (rows |> Seq.map (fun (id, d) -> id, round d) |> List.ofSeq)
            =! [ 1, 0.0; 3, 0.0 ]
        }

    [<Fact>]
    [<Trait("Category", "Integration")>]
    member _.``halfvec, sparsevec and vector[] columns hydrate as their mapped CLR types``() =
        task {
            let! rows =
                selectTask fixture.Context {
                    for s in ``public``.shapes do
                        orderBy s.id
                }

            let show (row: ``public``.shapes) =
                row.id,
                row.half |> Option.map string,
                row.sparse |> Option.map string,
                row.many |> Option.map (Array.map string >> List.ofArray)

            (rows |> Seq.map show |> List.ofSeq)
            =! [ 1, Some "[1,0,0]", Some "{1:1,4:2}/5", Some [ "[1,0,0]"; "[0,1,0]" ]
                 2, None, None, None ]
        }
