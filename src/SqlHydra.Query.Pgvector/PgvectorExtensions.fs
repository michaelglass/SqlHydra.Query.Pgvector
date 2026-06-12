module SqlHydra.Query.Pgvector.PgvectorExtensions

open System
open System.Linq.Expressions
open SqlHydra.Query

// Register pgvector infix operators via assembly-level attributes.
// SqlHydra.Query discovers these the first time a query is compiled — no setup call needed.
[<assembly: SqlHydraInfixOperator("cosine_distance", "<=>")>]
[<assembly: SqlHydraInfixOperator("l2_distance", "<->")>]
[<assembly: SqlHydraInfixOperator("inner_product_distance", "<#>")>]
do ()

/// pgvector distance functions for use in select expressions.
/// Use `open type PgvectorFn` to access functions without qualification.
/// Emits PostgreSQL pgvector infix operators: `<=>` (cosine), `<->` (L2), `<#>` (inner product).
type PgvectorFn =
    /// Cosine distance between two vectors. Emits: lhs <=> rhs
    static member cosine_distance(a: 'T, b: 'U) : float = sqlFn
    /// L2 (Euclidean) distance between two vectors. Emits: lhs <-> rhs
    static member l2_distance(a: 'T, b: 'U) : float = sqlFn
    /// Inner product distance between two vectors. Emits: lhs <#> rhs
    static member inner_product_distance(a: 'T, b: 'U) : float = sqlFn

/// pgvector-specific extensions for the select builder.
type SelectBuilder<'Selected, 'Mapped> with

    member private this.OrderByVectorDistance
        (state: QuerySource<'T, SelectQueryIR>, propertySelector, operator: string, vector: obj)
        =
        match tryGetOrderByColumn<'T, 'Prop> propertySelector with
        | Some(tableAlias, colName) ->
            let fqCol = $"\"{tableAlias}\".\"{colName}\""

            QuerySource<'T, SelectQueryIR>(
                { state.Query with
                    OrderBy = state.Query.OrderBy @ [ OrderByRaw($"{fqCol} {operator} ?", [| vector |]) ] },
                state.TableMappings
            )
        | None ->
            raise (
                InvalidOperationException(
                    $"pgvector distance ordering requires a simple column reference, "
                    + $"but the selector '{propertySelector}' did not resolve to a single column. "
                    + "Order by a plain column property (e.g. `orderByCosineDistance i.embedding queryVector`)."
                )
            )

    /// ORDER BY column <=> @vector (pgvector cosine distance, ascending — closest first).
    [<CustomOperation("orderByCosineDistance", MaintainsVariableSpace = true)>]
    member this.OrderByCosineDistance
        (
            state: QuerySource<'T, SelectQueryIR>,
            [<ProjectionParameter>] propertySelector: Expression<Func<'T, 'Prop>>,
            vector: obj
        ) =
        this.OrderByVectorDistance(state, propertySelector, "<=>", vector)

    /// ORDER BY column <-> @vector (pgvector L2/Euclidean distance, ascending — closest first).
    [<CustomOperation("orderByL2Distance", MaintainsVariableSpace = true)>]
    member this.OrderByL2Distance
        (
            state: QuerySource<'T, SelectQueryIR>,
            [<ProjectionParameter>] propertySelector: Expression<Func<'T, 'Prop>>,
            vector: obj
        ) =
        this.OrderByVectorDistance(state, propertySelector, "<->", vector)

    /// ORDER BY column <#> @vector (pgvector inner product distance, ascending).
    [<CustomOperation("orderByInnerProductDistance", MaintainsVariableSpace = true)>]
    member this.OrderByInnerProductDistance
        (
            state: QuerySource<'T, SelectQueryIR>,
            [<ProjectionParameter>] propertySelector: Expression<Func<'T, 'Prop>>,
            vector: obj
        ) =
        this.OrderByVectorDistance(state, propertySelector, "<#>", vector)
