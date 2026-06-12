module SqlHydra.Query.Pgvector.Tests.Tests

open Xunit
open Swensen.Unquote
open SqlHydra.Query
open SqlHydra.Query.Pgvector.PgvectorExtensions
open type SqlHydra.Query.Pgvector.PgvectorExtensions.PgvectorFn
open SqlHydra.Query.Pgvector.Tests.production

// pgvector operator registration auto-fires when the assembly loads via the
// [<assembly: SqlHydraInfixOperator(...)>] attributes — no per-test setup needed.

// A `select`-projection distance op is column-vs-column only: SqlHydra emits the
// infix operator between the two column references. A *literal* second argument
// (a `Pgvector.Vector` or a raw array) is NOT inlined as a SQL string literal and
// is NOT silently parameter-bound — SqlHydra's emitter fails fast at compile time.
// Only the `orderBy*Distance` path parameter-binds the query vector. These tests
// pin that behaviour so the README's scope claim stays honest.

[<Fact>]
let ``cosine_distance literal Vector in select fails fast (not inlined, not bound)`` () =
    let queryVec = Pgvector.Vector(System.ReadOnlyMemory([| 0.1f; 0.2f; 0.3f |]))

    let ex =
        Assert.ThrowsAny<exn>(fun () ->
            select {
                for p in product do
                    select (cosine_distance (p.standardcost, queryVec))
            }
            |> toSql
            |> ignore)

    // The whole point: it does NOT inline the vector as a literal string.
    ex.Message.Contains "Pgvector.Vector" =! true
    ex.Message.Contains "literal" =! true

[<Fact>]
let ``cosine_distance literal array in select fails fast`` () =
    let arr = [| 0.1f; 0.2f; 0.3f |]

    let ex =
        Assert.ThrowsAny<exn>(fun () ->
            select {
                for p in product do
                    select (cosine_distance (p.standardcost, arr))
            }
            |> toSql
            |> ignore)

    ex.Message.Contains "literal" =! true

[<Fact>]
let ``cosine_distance column-vs-column in select emits infix with no parameters`` () =
    let q =
        select {
            for p in product do
                select (cosine_distance (p.standardcost, p.listprice))
        }

    let emitter = PostgresEmitter() :> ISqlEmitter
    let compiled = q.CompileWith(emitter)
    compiled.Sql.Contains "<=>" =! true
    // Pure column-vs-column projection — no bound parameters.
    compiled.Parameters.Length =! 0

[<Fact>]
let ``cosine_distance emits <=> infix in select`` () =
    let sql =
        select {
            for p in product do
                select (cosine_distance (p.standardcost, p.listprice))
        }
        |> toSql

    sql.Contains "<=>" =! true

[<Fact>]
let ``l2_distance emits <-> infix in select`` () =
    let sql =
        select {
            for p in product do
                select (l2_distance (p.standardcost, p.listprice))
        }
        |> toSql

    sql.Contains "<->" =! true

[<Fact>]
let ``inner_product_distance emits <#> infix in select`` () =
    let sql =
        select {
            for p in product do
                select (inner_product_distance (p.standardcost, p.listprice))
        }
        |> toSql

    sql.Contains "<#>" =! true

[<Fact>]
let ``orderByCosineDistance emits ORDER BY ... <=> ?`` () =
    let vector = [| 0.1f; 0.2f; 0.3f |]

    let sql =
        select {
            for p in product do
                orderByCosineDistance p.standardcost (box vector)
        }
        |> toSql

    sql.Contains "ORDER BY" =! true
    sql.Contains "<=>" =! true

[<Fact>]
let ``orderByCosineDistance binds vector as a parameter`` () =
    let vector = [| 0.1f; 0.2f; 0.3f |]

    let q =
        select {
            for p in product do
                orderByCosineDistance p.standardcost (box vector)
        }

    let emitter = PostgresEmitter() :> ISqlEmitter
    let compiled = q.CompileWith(emitter)
    // SQL should reference a bound parameter instead of a bare ? placeholder.
    compiled.Sql.Contains "<=>" =! true
    compiled.Sql.Contains " ?" =! false
    compiled.Parameters.Length =! 1
    let (_, value) = compiled.Parameters.[0]
    value =! (box vector)

[<Fact>]
let ``orderByL2Distance binds vector as a parameter`` () =
    let vector = [| 0.5f; 0.5f |]

    let q =
        select {
            for p in product do
                orderByL2Distance p.standardcost (box vector)
        }

    let emitter = PostgresEmitter() :> ISqlEmitter
    let compiled = q.CompileWith(emitter)
    compiled.Sql.Contains "<->" =! true
    compiled.Parameters.Length =! 1
    let (_, value) = compiled.Parameters.[0]
    value =! (box vector)

[<Fact>]
let ``orderByCosineDistance + nullsLast retains parameter binding`` () =
    let vector = [| 1.0f; 2.0f |]

    let q =
        select {
            for p in product do
                orderByCosineDistance p.standardcost (box vector)
                nullsLast
        }

    let emitter = PostgresEmitter() :> ISqlEmitter
    let compiled = q.CompileWith(emitter)
    compiled.Sql.Contains "NULLS LAST" =! true
    compiled.Sql.Contains "<=>" =! true
    compiled.Parameters.Length =! 1

[<Fact>]
let ``orderByInnerProductDistance binds vector as a parameter`` () =
    let vector = [| 0.25f; 0.75f |]

    let q =
        select {
            for p in product do
                orderByInnerProductDistance p.standardcost (box vector)
        }

    let emitter = PostgresEmitter() :> ISqlEmitter
    let compiled = q.CompileWith(emitter)
    compiled.Sql.Contains "<#>" =! true
    compiled.Parameters.Length =! 1
    let (_, value) = compiled.Parameters.[0]
    value =! (box vector)

[<Fact>]
let ``orderByCosineDistance rejects a non-column selector`` () =
    // A distance expression (not a simple column) can't be resolved to a qualified
    // column, so the orderBy op must fail loudly rather than emit broken SQL.
    let ex =
        Assert.Throws<System.InvalidOperationException>(fun () ->
            select {
                for p in product do
                    orderByCosineDistance (cosine_distance (p.standardcost, p.listprice)) (box [| 0.1f |])
            }
            |> ignore)

    // The message names the simple-column-reference requirement and echoes the
    // offending selector expression so the caller can find their mistake.
    ex.Message.Contains "simple column reference" =! true
    ex.Message.Contains "cosine_distance" =! true
