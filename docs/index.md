<!-- sync:intro -->
[pgvector](https://github.com/pgvector/pgvector) distance functions for
[SqlHydra.Query](https://github.com/JordanMarr/SqlHydra) — cosine, L2, and inner-product
distance operators for `select` projections and `ORDER BY`, plus a code-generation type
mapping (`PgvectorTypeMapping`) that maps the PostgreSQL `vector` column type to
`Pgvector.Vector` during `dotnet sqlhydra` generation. Register it in your generator TOML
(see below).
<!-- sync:intro:end -->
