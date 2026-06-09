# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What This Is

`SqlHydra.Query.Pgvector` is an extension package for [SqlHydra.Query](https://github.com/JordanMarr/SqlHydra) that adds [pgvector](https://github.com/pgvector/pgvector) support:

- **Distance operators** for `select` projections — `cosine_distance` (`<=>`), `l2_distance` (`<->`), `inner_product_distance` (`<#>`).
- **`orderBy*Distance` CE operations** — `orderByCosineDistance` / `orderByL2Distance` / `orderByInnerProductDistance`, with the query vector bound as a parameter.

- **Code-generation type mapping** — `PgvectorTypeMapping`, an `IExtendTypeMapping` that maps the PostgreSQL `vector` column type to `Pgvector.Vector` during `dotnet sqlhydra` generation. Register it via TOML (`[extensions] type_mappings = ["SqlHydra.Query.Pgvector"]`).

The package adds operators via SqlHydra.Query's public extensibility seam (the `SqlHydraInfixOperator` assembly attribute + `InfixOperators` registry, and the public `tryGetOrderByColumn` helper) — **no access to SqlHydra internals**. The code-generation type mapping ships in this package too, implemented against the `SqlHydra.Domain` codegen types that ride along inside the published `SqlHydra.Query` package (see "Dependency on SqlHydra").

## Build & Development Commands

Uses `mise` as the task runner (`mise.toml`). All tasks also work via `dotnet` directly.

```bash
mise run build        # Build the solution
mise run test         # Run tests
mise run format       # Fantomas (src/ tests/ examples/)
mise run lint         # FSharpLint
mise run docs         # Generate API docs via fsdocs
mise run check        # All checks with auto-fix
mise run ci           # All CI checks, no auto-fix (mirrors GitHub Actions)
mise run pack         # Create the NuGet package
```

CI runs via the reusable `michaelglass/MichaelsWackyFsPackageTools` workflow (`.github/workflows/ci.yml`): format-check → syncdocs check → build `--warnaserror` → fsharplint → fsdocs → per-test-project coverage → coverageratchet.

## Version Control

This repo is **colocated jj + git** (`jj git init --colocate`), like the other `~/Developer/opensource` F# packages. Use `jj` for day-to-day work; `git` still works for pushes/CI.

- Describe work: `jj describe -m "..."`; new change: `jj new`.
- The `main` bookmark tracks `origin/main` (`jj bookmark track main --remote=origin`).
- Push: `jj git push` (or `git push`).

## Architecture

Two source files in `src/SqlHydra.Query.Pgvector/`:

- **`PgvectorExtensions.fs`** — the runtime operators. A `PgvectorFn` type whose `*_distance` members are `sqlFn` marker stubs (the visitor matches the call-site shape and emits the infix operator; the body is never invoked). Assembly-level `[<assembly: SqlHydraInfixOperator(...)>]` attributes register the three operators, auto-discovered on first query compile. A `SelectBuilder` type-extension adds the `orderBy*Distance` CE ops, which resolve the column via the public `tryGetOrderByColumn` helper and append an `OrderByRaw` fragment with the vector as a bound parameter.
- **`TypeMapping.fs`** — `PgvectorTypeMapping`, an `IExtendTypeMapping` (from the `SqlHydra.Domain` types bundled in `SqlHydra.Query`) that maps the `vector` column type to `Pgvector.Vector` during code generation, with `ProviderDbType = None` (see "Dependency on SqlHydra").

Tests in `tests/SqlHydra.Query.Pgvector.Tests/` use xUnit v3 + Unquote: `Tests.fs` (operator/orderBy SQL emission via `toSql`), `TypeMappingTests.fs` (`PgvectorTypeMapping` codegen mapping — `vector` → `Pgvector.Vector`, case-insensitivity, base-resolver delegation), `IntegrationTests.fs` (real Postgres + pgvector via Testcontainers — executes the compiled SQL against `pgvector/pgvector:pg17` and asserts actual distance/nearest-neighbour results), `Schema.fs` (a minimal hand-rolled table for `toSql`-based assertions).

## Dependency on SqlHydra

The package depends on a single PackageReference: **`SqlHydra.Query`** (the published NuGet package — `4.1.0-beta.1`+ contains the extensibility seam: `SqlHydraInfixOperator`, `InfixOperators`, parameterized `OrderByRaw`, and the public `tryGetOrderByColumn` helper). No Paket, no fork pin — a plain `dotnet build` works.

The code-generation type mapping **ships** in this package (`TypeMapping.fs` → `PgvectorTypeMapping`). It's an `IExtendTypeMapping`, which needs the codegen types from **`SqlHydra.Domain`** — and those types ride along bundled inside the published `SqlHydra.Query` package (`lib/<tfm>/SqlHydra.Domain.dll`). So referencing `SqlHydra.Query` is enough to author and ship the mapping; there is no separate `SqlHydra.Domain` package to obtain out of band. Consumers register it via TOML (`[extensions] type_mappings = ["SqlHydra.Query.Pgvector"]`).

## Releasing

Version lives in `src/SqlHydra.Query.Pgvector/SqlHydra.Query.Pgvector.fsproj` (`<Version>`); a `v*` tag triggers the release workflow (`fssemantictagger`). Run `mise run ci` green and ensure the `## Unreleased` CHANGELOG section is populated before tagging.
