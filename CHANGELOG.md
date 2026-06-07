# Changelog

## Unreleased

- feat: pgvector distance operators for `select` projections — `cosine_distance` (`<=>`),
  `l2_distance` (`<->`), and `inner_product_distance` (`<#>`), registered as SqlHydra infix
  operators so they emit the native pgvector operators inline.
- feat: `orderByCosineDistance` / `orderByL2Distance` / `orderByInnerProductDistance` custom
  operations for nearest-neighbour `ORDER BY`, with the query vector bound as a real query
  parameter (parameterized `OrderByRaw`) rather than inlined.
- feat: `PgvectorTypeMapping` (`IExtendTypeMapping`) for `dotnet sqlhydra` code generation —
  maps the PostgreSQL `vector` column type to `Pgvector.Vector`. `ProviderDbType` is `None`
  on purpose: `NpgsqlDbType` has no `Vector` member, so binding goes through the
  `Pgvector.Npgsql` plugin (`UseVector()`), which infers the handler from the value itself.
