module SqlHydra.Query.Pgvector.Tests.Tests

open Xunit
open Swensen.Unquote
open SqlHydra.Query
open SqlHydra.Query.Pgvector.PgvectorExtensions
open type SqlHydra.Query.Pgvector.PgvectorExtensions.PgvectorFn
open SqlHydra.Query.Pgvector.Tests.production

// pgvector operator registration auto-fires when the assembly loads via the
// [<assembly: SqlHydraInfixOperator(...)>] attributes — no per-test setup needed.

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
