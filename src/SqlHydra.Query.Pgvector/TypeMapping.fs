namespace SqlHydra.Query.Pgvector

open SqlHydra.Domain

/// Code-generation type mapping: maps the PostgreSQL `vector` column type (pgvector)
/// to `Pgvector.Vector` during `dotnet sqlhydra` generation.
///
/// Register it in your TOML so the CLI applies it:
///
///     [extensions]
///     type_mappings = ["SqlHydra.Query.Pgvector"]
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
                          // Must be None: SqlHydra parses ProviderDbType with
                          // Enum.Parse<NpgsqlDbType>, which has no Vector member. Pgvector.Npgsql
                          // (UseVector()) infers the handler from the Vector value itself.
                          TypeMapping.ProviderDbType = None }
                | _ -> baseTryFind ctx
