namespace SqlHydra.Query.Pgvector

open SqlHydra.Domain

/// Code-generation type mapping: maps the PostgreSQL `vector` column type (pgvector)
/// to `Pgvector.Vector` during `dotnet sqlhydra` generation.
///
/// Register it in your TOML so the CLI applies it:
///
///     [extensions]
///     type_mappings = ["SqlHydra.Query.Pgvector"]
///
/// The `SqlHydra.Domain` types this implements ship inside the `SqlHydra.Query` package,
/// so referencing `SqlHydra.Query` is enough to author against them.
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
                          // Must be None: SqlHydra applies ProviderDbType via
                          // Enum.Parse<NpgsqlDbType>, and NpgsqlDbType has no Vector member.
                          // pgvector binds through the Pgvector.Npgsql plugin (UseVector()),
                          // which infers the handler from the Pgvector.Vector value itself.
                          TypeMapping.ProviderDbType = None }
                | _ -> baseTryFind ctx
