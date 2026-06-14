# ADR-003: Azure and Foundry Deployment Profile

Status: Accepted

Date: 2026-06-14

## Context

The runtime must deploy on Azure and use Microsoft Foundry models without
making Azure the owner of orchestration, state transitions, evaluation, tool
policy, traces, or artifact naming.

## Decision

The Azure profile maps existing runtime ports to Azure services:

| Runtime boundary | Azure service |
| --- | --- |
| `IModelClient` | Microsoft Foundry Responses API |
| `IRunStateStore` | Azure Database for PostgreSQL or Azure SQL Database |
| `IArtifactStore` | Azure Blob Storage |
| `ISemanticIndex` | Azure AI Search |

`AzureFoundryModelClient` calls `POST /openai/v1/responses`. Provider endpoint,
deployment name, API version, and credentials remain model-client
configuration. Foundry returns reasoning content; it does not select patterns,
write artifacts, pass evals, or complete runs.

The current Core implementation includes the Foundry model client, the generic
PostgreSQL/SQL Server run-state adapter, the artifact contract, and the
semantic-index contract. Azure Blob Storage and Azure AI Search adapters are
deployment additions behind those contracts; they are not prerequisites for
credential-free tests.

## Operational Shape

- Use PostgreSQL when cross-cloud database portability is the priority.
- Use Azure SQL Database when existing SQL Server operations are the priority.
- Store JSON, HTML, Markdown, and binary artifacts as Blob objects with media
  type and SHA-256 metadata.
- Project selected artifact text into Azure AI Search. Treat the index as
  disposable and rebuildable.
- Keep the timestamped output directory as the runtime's canonical write
  target and mirror only through runtime-owned adapters.
- Supply provider drivers and Azure SDK adapters at deployment composition
  time, not in orchestration code.

## Consequences

- Azure services can be replaced independently.
- Foundry integration does not turn this project into an Azure agent framework.
- A service outage in an explicitly enabled authoritative mirror fails the run
  visibly instead of silently losing persistence.
- Managed identity support can be added inside individual adapters without
  changing Core contracts or runtime policy.
