module SqlHydra.Query.Pgvector.Tests.Tests

open Xunit
open Swensen.Unquote
open SqlHydra.Query
open SqlHydra.Query.Pgvector.PgvectorExtensions
open type SqlHydra.Query.Pgvector.PgvectorExtensions.PgvectorFn
open SqlHydra.Query.Pgvector.Tests.production

// A `select`-projection distance op is column-vs-column only. A literal second
// argument (a `Pgvector.Vector` or a raw array) is neither inlined as a SQL literal
// nor silently parameter-bound — SqlHydra's emitter fails fast. Only the
// `orderBy*Distance` path binds the query vector as a parameter.

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
    compiled.Sql.Contains "<=>" =! true
    // No bare `?` left behind: the placeholder must have become a bound parameter.
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
    // A distance expression has no qualified column to order by, so the op must fail
    // rather than emit broken SQL.
    let ex =
        Assert.Throws<System.InvalidOperationException>(fun () ->
            select {
                for p in product do
                    orderByCosineDistance (cosine_distance (p.standardcost, p.listprice)) (box [| 0.1f |])
            }
            |> ignore)

    ex.Message.Contains "simple column reference" =! true
    ex.Message.Contains "cosine_distance" =! true
