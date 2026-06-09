# Changelog

## Unreleased

- feat: pgvector distance operators for `select` projections — `cosine_distance` (`<=>`),
  `l2_distance` (`<->`), and `inner_product_distance` (`<#>`), registered as SqlHydra infix
  operators so they emit the native pgvector operators inline.
- feat: `orderByCosineDistance` / `orderByL2Distance` / `orderByInnerProductDistance` custom
  operations for nearest-neighbour `ORDER BY`, with the query vector bound as a real query
  parameter (parameterized `OrderByRaw`) rather than inlined.
- feat: ship `PgvectorTypeMapping`, a code-generation `IExtendTypeMapping` that maps the
  PostgreSQL `vector` column type to `Pgvector.Vector` during `dotnet sqlhydra` generation.
  Register it via TOML — `[extensions] type_mappings = ["SqlHydra.Query.Pgvector"]`. It's
  authored against the `SqlHydra.Domain` codegen types bundled inside the published
  `SqlHydra.Query` package, so no separate `SqlHydra.Domain` reference is needed.
  `ProviderDbType` is `None` on purpose: `NpgsqlDbType` has no `Vector` member, so binding
  goes through the `Pgvector.Npgsql` plugin (`UseVector()`), which infers the handler from the
  value itself.
- docs: document building and testing in the README (`mise run build`/`test`/`ci`/`format`),
  noting that the integration tests use Testcontainers and require a running Docker daemon
  while the unit tests do not.
