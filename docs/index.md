<!-- sync:intro -->
Vector similarity search for [SqlHydra.Query](https://github.com/JordanMarr/SqlHydra),
powered by [pgvector](https://github.com/pgvector/pgvector).

Write `select` and `ORDER BY` queries that compare embeddings by **cosine**, **L2
(Euclidean)**, or **inner-product** distance — all in strongly-typed F#, with the native
pgvector operators (`<=>`, `<->`, `<#>`) generated for you. It also teaches the SqlHydra
code generator about `vector` columns so they come through as `Pgvector.Vector` in your
generated types.
<!-- sync:intro:end -->
