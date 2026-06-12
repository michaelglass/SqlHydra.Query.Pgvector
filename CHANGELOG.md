# Changelog

## Unreleased

- fix: `orderBy*Distance` with a non-column selector now raises `InvalidOperationException`
  (was a bare `failwith`/`System.Exception`) and the message echoes the offending selector
  expression, so the caller can see which selector was rejected.
- test: add a real-Postgres integration test for `orderByInnerProductDistance`, asserting the
  sign-inverted (`<#>` returns the negated inner product) nearest-first ordering against
  axis-aligned seed vectors. Mirrors the existing cosine/L2 integration tests.
- docs: clarify that the `select`-projection distance functions (`cosine_distance` etc.) are
  **column-vs-column only**. Passing a literal `Pgvector.Vector`/array as the second argument
  is not supported in a `select` projection — SqlHydra fails fast at compile time rather than
  inlining the value. The "query vector is always parameter-bound" guarantee applies to the
  `orderBy*Distance` path. Pinned by regression tests.

## 0.1.0-alpha.3 - 2026-06-11

- fix: pin the `FSharp.Core` dependency floor explicitly per target framework
  (netstandard2.0 `6.0.7`, net8 `8.0.100`, net9 `9.0.201`, net10 `10.1.201`) instead of
  letting the F# SDK's implicit reference float to whatever the build SDK bundles. alpha.2
  shipped a `>= 10.1.301` floor (whatever CI's SDK happened to bundle), which broke
  consumers pinned to an older `FSharp.Core`. A `PackageReference` version is a minimum, so
  these low floors keep the package compatible with a broad range of `FSharp.Core`.

## 0.1.0-alpha.2 - 2026-06-11

- fix: require `SqlHydra.Query` >= 4.1.0-beta.2. beta.1 throws on aggregates over an
  expression (e.g. `sumBy(caseWhen ...)`); beta.2 fixes it ([JordanMarr/SqlHydra#132]).
  Because NuGet resolves transitive dependencies to the lowest satisfying version, the
  dependency floor is bumped so consumers of this package pick up the fix.

[JordanMarr/SqlHydra#132]: https://github.com/JordanMarr/SqlHydra/pull/132

## 0.1.0-alpha.1 — 2026-06-09

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
