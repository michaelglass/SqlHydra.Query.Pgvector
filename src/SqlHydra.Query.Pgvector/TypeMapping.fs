namespace SqlHydra.Query.Pgvector

open SqlHydra.Domain

/// Code-generation extension that maps the PostgreSQL `vector` column type
/// (pgvector) to the `Pgvector.Vector` CLR type during `dotnet sqlhydra` generation.
///
/// Register it in your TOML so the CLI applies it:
///
///     [extensions]
///     type_mappings = ["SqlHydra.Query.Pgvector"]
///
/// SqlHydra discovers `IExtendTypeMapping` implementations in the referenced
/// assembly automatically and composes them ahead of the built-in mappings.
type PgvectorTypeMapping() =
    interface IExtendTypeMapping with
        member _.Extend(baseTryFind) =
            fun (ctx: TypeMappingContext) ->
                match ctx.Column.ProviderTypeName.ToLower() with
                | "vector" ->
                    Some
                        { TypeMapping.ColumnTypeAlias = "vector"
                          TypeMapping.ClrType = "Pgvector.Vector"
                          TypeMapping.DbType = System.Data.DbType.Object
                          TypeMapping.ProviderDbType = Some "Vector" }
                | _ -> baseTryFind ctx
