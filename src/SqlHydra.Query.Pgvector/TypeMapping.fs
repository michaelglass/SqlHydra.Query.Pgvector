namespace SqlHydra.Query.Pgvector

open SqlHydra.Domain

// Module-level rather than `static let` in the type: a class's static fields are guarded by a
// static-initialisation check on every access, a branch no caller can take.
module private PgvectorTypes =

    let clrTypes =
        dict
            [ "vector", "Pgvector.Vector"
              "halfvec", "Pgvector.HalfVector"
              "sparsevec", "Pgvector.SparseVector" ]

    /// `format_type` prefixes a type with its schema when that schema is not on the search path,
    /// and quotes a name that needs it. The type name itself is always the last segment.
    let unqualified (providerTypeName: string) =
        providerTypeName.Substring(providerTypeName.LastIndexOf '.' + 1).Trim('"').ToLowerInvariant()

    let tryMap (providerTypeName: string) =
        let name = unqualified providerTypeName

        let element, suffix =
            if name.EndsWith "[]" then
                name.Substring(0, name.Length - 2), "[]"
            else
                name, ""

        match clrTypes.TryGetValue element with
        | true, clrType ->
            Some
                { TypeMapping.ColumnTypeAlias = name
                  TypeMapping.ClrType = clrType + suffix
                  TypeMapping.DbType = System.Data.DbType.Object
                  // Must be None: SqlHydra parses ProviderDbType with Enum.Parse<NpgsqlDbType>,
                  // which has no pgvector member. Pgvector's Npgsql plugin (UseVector()) infers
                  // the handler from the value itself.
                  TypeMapping.ProviderDbType = None }
        | false, _ -> None

/// Code-generation type mapping: maps the pgvector column types to their `Pgvector` CLR types
/// during `dotnet sqlhydra` generation.
///
/// | PostgreSQL  | CLR                     |
/// |-------------|-------------------------|
/// | `vector`    | `Pgvector.Vector`       |
/// | `halfvec`   | `Pgvector.HalfVector`   |
/// | `sparsevec` | `Pgvector.SparseVector` |
///
/// An array of any of them maps to an array of its CLR type. A type is recognised with or without
/// a schema qualifier, which PostgreSQL adds (`extensions.vector`) when the extension's schema is
/// not on the generator's search path.
///
/// Register it in your TOML so the CLI applies it:
///
///     [extensions]
///     type_mappings = ["SqlHydra.Query.Pgvector"]
type PgvectorTypeMapping() =
    interface IExtendTypeMapping with
        member _.Extend(baseTryFind) =
            fun (ctx: TypeMappingContext) ->
                PgvectorTypes.tryMap ctx.Column.ProviderTypeName
                |> Option.orElseWith (fun () -> baseTryFind ctx)
