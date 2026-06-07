module SqlHydra.Query.Pgvector.Tests.TypeMappingTests

open Xunit
open Swensen.Unquote
open SqlHydra.Domain
open SqlHydra.Query.Pgvector

let private columnSchema (providerTypeName: string) : ColumnSchema =
    { Catalog = "db"
      Schema = "public"
      Table = "documents"
      Name = "embedding"
      ProviderTypeName = providerTypeName
      IsNullable = false
      Ordinal = 0
      Precision = None
      Scale = None
      IsPrimaryKey = false
      IsComputed = false
      DefaultValue = None }

let private contextFor (providerTypeName: string) : TypeMappingContext =
    let col = columnSchema providerTypeName

    { Table =
        { Catalog = "db"
          Schema = "public"
          Name = "documents"
          Type = TableType.Table
          Columns = [ col ] }
      Column = col }

let private extended =
    let mapping = PgvectorTypeMapping() :> IExtendTypeMapping
    // Base resolver that maps nothing — proves the extension supplies `vector` itself
    // and delegates everything else.
    mapping.Extend(fun _ -> None)

[<Fact>]
let ``maps vector column to Pgvector.Vector`` () =
    let result = extended (contextFor "vector")

    match result with
    | Some m ->
        m.ClrType =! "Pgvector.Vector"
        m.ColumnTypeAlias =! "vector"
        m.DbType =! System.Data.DbType.Object
        m.ProviderDbType =! None
    | None -> failwith "expected a mapping for the vector column type"

[<Fact>]
let ``matches the vector type name case-insensitively`` () =
    let result = extended (contextFor "VECTOR")
    result.IsSome =! true

[<Fact>]
let ``delegates non-vector columns to the base resolver`` () =
    let result = extended (contextFor "int4")
    result =! None
