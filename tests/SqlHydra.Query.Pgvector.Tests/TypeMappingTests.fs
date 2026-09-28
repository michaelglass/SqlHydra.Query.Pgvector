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
    // Base resolver maps nothing: proves the extension supplies `vector` and delegates the rest.
    mapping.Extend(fun _ -> None)

/// The CLR type a provider type name maps to, or None when the extension passes it on.
let private clrTypeOf providerTypeName =
    extended (contextFor providerTypeName) |> Option.map _.ClrType

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
let ``maps halfvec and sparsevec to their Pgvector types`` () =
    clrTypeOf "halfvec" =! Some "Pgvector.HalfVector"
    clrTypeOf "sparsevec" =! Some "Pgvector.SparseVector"

[<Fact>]
let ``maps an array of a pgvector type to an array of its CLR type`` () =
    clrTypeOf "vector[]" =! Some "Pgvector.Vector[]"
    clrTypeOf "halfvec[]" =! Some "Pgvector.HalfVector[]"

// PostgreSQL qualifies the type with its schema when that schema is not on the search path
// (after `CREATE EXTENSION vector SCHEMA extensions`, say), quoting a schema name that needs it.
[<Theory>]
[<InlineData("extensions.vector", "Pgvector.Vector")>]
[<InlineData("\"My Schema\".vector", "Pgvector.Vector")>]
[<InlineData("extensions.sparsevec[]", "Pgvector.SparseVector[]")>]
let ``maps a schema-qualified pgvector type`` (providerTypeName: string, clrType: string) =
    clrTypeOf providerTypeName =! Some clrType

[<Fact>]
let ``matches the vector type name case-insensitively`` () =
    clrTypeOf "VECTOR" =! Some "Pgvector.Vector"

[<Theory>]
[<InlineData("int4")>]
[<InlineData("vectors")>]
[<InlineData("vector_config")>]
let ``delegates other columns to the base resolver`` (providerTypeName: string) = clrTypeOf providerTypeName =! None

/// A typo in a CLR type name would still compile here and only fail in the generated code.
[<Theory>]
[<InlineData("vector")>]
[<InlineData("halfvec")>]
[<InlineData("sparsevec")>]
[<InlineData("vector[]")>]
let ``every mapped CLR type exists in the Pgvector assembly`` (providerTypeName: string) =
    let clrType = (clrTypeOf providerTypeName).Value
    let pgvector = typeof<Pgvector.Vector>.Assembly
    test <@ not (isNull (pgvector.GetType clrType)) @>
