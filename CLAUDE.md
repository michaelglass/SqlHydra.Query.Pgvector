# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What This Is

`SqlHydra.Query.Pgvector` is an extension package for [SqlHydra.Query](https://github.com/JordanMarr/SqlHydra) that adds [pgvector](https://github.com/pgvector/pgvector) support:

- **Distance operators** for `select` projections — `cosine_distance` (`<=>`), `l2_distance` (`<->`), `inner_product_distance` (`<#>`).
- **`orderBy*Distance` CE operations** — `orderByCosineDistance` / `orderByL2Distance` / `orderByInnerProductDistance`, with the query vector bound as a parameter.

This is a **runtime-only** package: it adds operators via SqlHydra.Query's public extensibility seam (the `SqlHydraInfixOperator` assembly attribute + `InfixOperators` registry, and the public `tryGetOrderByColumn` helper) — **no access to SqlHydra internals**. It does not ship a code-generation type mapping; mapping the PostgreSQL `vector` column type to `Pgvector.Vector` during `dotnet sqlhydra` generation is a documented copy-paste `IExtendTypeMapping` snippet in the README (see "Dependency on SqlHydra" for why).

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

One source file in `src/SqlHydra.Query.Pgvector/`:

- **`PgvectorExtensions.fs`** — the whole runtime. A `PgvectorFn` type whose `*_distance` members are `sqlFn` marker stubs (the visitor matches the call-site shape and emits the infix operator; the body is never invoked). Assembly-level `[<assembly: SqlHydraInfixOperator(...)>]` attributes register the three operators, auto-discovered on first query compile. A `SelectBuilder` type-extension adds the `orderBy*Distance` CE ops, which resolve the column via the public `tryGetOrderByColumn` helper and append an `OrderByRaw` fragment with the vector as a bound parameter.

There is no shipped code-generation type mapping (see "Dependency on SqlHydra"); the `IExtendTypeMapping` for the `vector` column is a copy-paste snippet in the README instead.

Tests in `tests/SqlHydra.Query.Pgvector.Tests/` use xUnit v3 + Unquote: `Tests.fs` (operator/orderBy SQL emission via `toSql`), `IntegrationTests.fs` (real Postgres + pgvector via Testcontainers — executes the compiled SQL against `pgvector/pgvector:pg17` and asserts actual distance/nearest-neighbour results), `Schema.fs` (a minimal hand-rolled table for `toSql`-based assertions).

## Dependency on SqlHydra

The package depends on a single PackageReference: **`SqlHydra.Query`** (the published NuGet package — `4.1.0-beta.1`+ contains the extensibility seam: `SqlHydraInfixOperator`, `InfixOperators`, parameterized `OrderByRaw`, and the public `tryGetOrderByColumn` helper). No Paket, no fork pin — a plain `dotnet build` works.

The code-generation type mapping is **documented, not shipped**. An `IExtendTypeMapping` for the `vector` column needs the codegen types from **`SqlHydra.Domain`**, which is **not currently published as a standalone NuGet package** (it ships compiled into `SqlHydra.Cli`). Shipping a type that depends on it would force consumers to obtain `SqlHydra.Domain` out of band, so instead the README carries a copy-paste snippet users drop into their own SqlHydra.Query project (which already references `SqlHydra.Domain`). This is the one piece tracking an upstream gap; once `SqlHydra.Domain` is published standalone we can ship the mapping as a real type again.

## Releasing

Version lives in `src/SqlHydra.Query.Pgvector/SqlHydra.Query.Pgvector.fsproj` (`<Version>`); a `v*` tag triggers the release workflow (`fssemantictagger`). Run `mise run ci` green and ensure the `## Unreleased` CHANGELOG section is populated before tagging.
