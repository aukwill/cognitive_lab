# ADR-002: Separate Portable Persistence Ports

Status: Accepted

Date: 2026-06-14

## Context

The runtime needs optional persistence without making orchestration, run
state, artifacts, semantic retrieval, or model selection depend on one cloud
or database. Run output includes Markdown, JSON traces, HTML inspection views,
and pipeline stage artifacts. These have different storage and migration needs
from queryable lifecycle state and rebuildable semantic projections.

The accepted recovery design still treats durable checkpoints as future work.
Database persistence in this decision must not imply that interrupted runs can
be resumed safely.

## Decision

Core depends on three independent ports:

- `IRunStateStore` for versioned run catalog upsert, lookup, and listing
- `IArtifactStore` for opaque artifact put, lookup, and listing
- `ISemanticIndex` for optional document projection and search

None exposes SQL, connection strings, SDK clients, or provider-specific
values.

`AdoNetRunStateStore` is the relational state adapter. Provider-specific
behavior is
limited to:

- the configured `DbProviderFactory`
- table-creation SQL selected by `RelationalDatabaseDialect`

PostgreSQL and SQL Server use the same parameterized insert, update, select,
and transfer behavior.

`DirectoryArtifactStore` is the credential-free artifact adapter. Cloud object
stores such as Azure Blob Storage and S3 implement `IArtifactStore` without
changing the runtime or relational schema.

`NullSemanticIndex` makes semantic retrieval opt-in. Azure AI Search, pgvector,
or another vector-capable service can implement `ISemanticIndex`. The index is
a rebuildable projection and never decides completion, evaluation, or phase
execution.

## Portable Data Shape

`runtime_runs` keeps bounded query columns for run identity, lifecycle,
pattern, mode, provider, output directory, generation, and timestamps. Less
stable structured data is stored in a schema-versioned JSON payload using a
plain text column. The payload does not contain credentials or
provider-specific request bodies.

Each stored artifact carries:

- run ID
- normalized forward-slash relative path
- media type
- opaque bytes
- byte length
- SHA-256
- schema version and update timestamp

JSON and HTML therefore use `application/json` and `text/html` media types but
do not depend on vendor JSON, XML, or large-text behavior. Object-store
adapters persist these bytes directly and retain integrity metadata.

The filesystem remains the canonical runtime artifact location. The database
and artifact store are queryable mirrors and portability boundaries, not a
second authority for evals, completion, or recovery. When a store is
explicitly enabled, write failure fails the run rather than silently leaving
an incomplete mirror.

## Lift And Shift

`PortableStorageTransfer.CopyAsync` copies versioned run records through
`IRunStateStore` and artifacts through `IArtifactStore`. It does not inspect
SQL or translate provider types. Integrity metadata moves with each artifact
and is revalidated by the destination.

Switching a deployment requires:

1. configuring the destination run-state adapter
2. configuring the destination artifact adapter
3. copying authoritative history through `PortableStorageTransfer`
4. rebuilding the semantic index from stored artifacts when needed

Core runtime, modes, traces, evals, artifact writers, and orchestration code do
not change.

## Consequences

- PostgreSQL and SQL Server require their ADO.NET driver at deployment time.
- Directory artifact storage requires no external credentials.
- Cross-store transactions are not claimed; transfer is record-by-record and
  can be rerun because writes are idempotent.
- Semantic indexes are eventually consistent projections, not authorities.
- Schema evolution requires explicit schema-version handling and future
  migrations. Automatic destructive migrations are not allowed.
- This store is not a run checkpoint and does not enable automatic resume.
