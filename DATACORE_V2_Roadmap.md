Below is a **production-oriented MCP server design** for an AI agent that needs **both data storage and data computation**. I’ll first summarize what the current MCP ecosystem suggests, then give you a concrete architecture, API surface, security model, execution model, and rollout plan.

***

# 1) What existing work tells us

The **Model Context Protocol (MCP)** is a JSON-RPC–based client/server protocol in which servers expose **tools**, **resources**, and optionally **prompts**, with capability negotiation during initialization; the official spec explicitly frames MCP as a standard way to connect LLM applications to external data sources and executable capabilities. [\[modelconte...rotocol.io\]](https://modelcontextprotocol.io/specification/2025-03-26), [\[anthropic.com\]](https://www.anthropic.com/news/model-context-protocol)

The official MCP examples and reference implementations are especially relevant for your use case: the ecosystem already includes reference servers for **filesystem**, **memory**, **git**, **fetch**, **time**, and **sequential thinking**; notably, the official examples highlight **filesystem** as a secure access pattern and **memory** as a persistent knowledge-graph pattern. [\[modelconte...rotocol.io\]](https://modelcontextprotocol.io/examples), [\[github.com\]](https://github.com/modelcontextprotocol/servers)

However, the official servers repository explicitly warns that those reference servers are **educational examples, not production-ready solutions**, and that implementers must add their own security controls, threat-model-driven safeguards, and operational hardening. That warning is crucial for your design: you should treat the reference servers as **interaction patterns**, not as deployable architecture. [\[github.com\]](https://github.com/modelcontextprotocol/servers)

For **remote** MCP servers, the ecosystem has clearly moved beyond local-only stdio. The MCP authorization specification says that for HTTP-based transports, implementations should follow the MCP authorization model based on **OAuth 2.1**, metadata discovery, and token-based access, while stdio deployments should typically retrieve credentials from the local environment instead. [\[modelconte...rotocol.io\]](https://modelcontextprotocol.io/specification/2025-03-26/basic/authorization), [\[modelconte...rotocol.io\]](https://modelcontextprotocol.io/specification/2025-03-26)

The transport direction is also clear: Cloudflare’s remote MCP work shows how teams are operationalizing Internet-accessible MCP servers with OAuth, and OpenAI’s Agents SDK documentation recommends **Streamable HTTP** or **stdio** for new integrations while keeping **SSE** only for legacy compatibility. [\[blog.cloudflare.com\]](https://blog.cloudflare.com/remote-model-context-protocol-servers-mcp/), [\[openai.github.io\]](https://openai.github.io/openai-agents-js/guides/mcp/)

Finally, MCP is no longer niche: OpenAI added support for **remote MCP servers** in the Responses API and provides MCP support in its Agents SDK, which means that if you design your server well, it can be consumed by multiple agent hosts instead of being tied to a single assistant product. [\[openai.com\]](https://openai.com/index/new-tools-and-features-in-the-responses-api/), [\[openai.github.io\]](https://openai.github.io/openai-agents-js/guides/mcp/)

***

# 2) The right product definition for your server

For your use case, I would **not** design the server as “just a database wrapper.” I would design it as an **AI Data Fabric MCP Server** with two responsibilities:

1.  **Data access + storage abstraction**  
    A unified way for the agent to discover datasets, inspect schemas, read slices of data, write approved artifacts, and manage versions.

2.  **Computation orchestration**  
    A controlled execution layer for SQL, aggregation, transformations, statistics, and optionally longer-running jobs, with progress, cancellation, and result materialization.

This aligns with MCP’s division between **resources** (discoverable, inspectable context/data) and **tools** (actions/execution), and it matches how the official protocol expects a server to expose both context and executable functionality. [\[modelconte...rotocol.io\]](https://modelcontextprotocol.io/specification/2025-03-26), [\[modelconte...rotocol.io\]](https://modelcontextprotocol.io/examples)

My core design recommendation is:

*   use **resources** for **metadata, previews, immutable outputs, and job results**;
*   use **tools** for **actions, mutations, and computation requests**;
*   keep **prompts** optional and lightweight, mainly for reusable workflows rather than core business logic. This is consistent with MCP’s intended feature model of resources/prompts/tools. [\[modelconte...rotocol.io\]](https://modelcontextprotocol.io/specification/2025-03-26)

***

# 3) High-level architecture

I recommend a **four-plane architecture**:

```text
┌──────────────────────────────────────────────────────────────┐
│  MCP Interface Plane                                        │
│  - initialize / capabilities                                │
│  - tools/list, tools/call                                   │
│  - resources/list, resources/read                           │
│  - prompts/list, prompts/get (optional)                     │
└──────────────────────────────────────────────────────────────┘
                          │
                          ▼
┌──────────────────────────────────────────────────────────────┐
│  Control Plane                                               │
│  - AuthN/AuthZ                                               │
│  - Session + tenant context                                  │
│  - Policy engine                                             │
│  - Tool routing / planner                                    │
│  - Audit, rate limits, quotas                                │
└──────────────────────────────────────────────────────────────┘
                          │
                          ▼
┌──────────────────────────────────────────────────────────────┐
│  Execution Plane                                             │
│  - Sync executor (fast queries/tools)                        │
│  - Async job manager                                         │
│  - SQL engine / compute kernels                              │
│  - Sandbox / container runner                                │
│  - Caching + result materialization                          │
└──────────────────────────────────────────────────────────────┘
                          │
                          ▼
┌──────────────────────────────────────────────────────────────┐
│  Data Plane                                                  │
│  - Metadata DB (catalog, policies, lineage, jobs)            │
│  - Object store (Parquet/CSV/JSON/blobs/results)             │
│  - OLTP/OLAP connectors (Postgres, DuckDB, Spark, etc.)      │
│  - Vector/graph store (optional)                             │
│  - Secrets manager                                            │
└──────────────────────────────────────────────────────────────┘
```

This separation is important because MCP itself standardizes the host/client/server interaction, but it does **not** prescribe your internal runtime, storage topology, or policy system. The spec is intentionally about the protocol boundary; production concerns remain your responsibility. [\[modelconte...rotocol.io\]](https://modelcontextprotocol.io/specification/2025-03-26), [\[github.com\]](https://github.com/modelcontextprotocol/servers)

***

# 4) Recommended MCP surface: what should be a Resource vs a Tool

## 4.1 Resources (read-oriented, inspectable, cacheable)

Use resources for anything that the model or user may want to **inspect as context**.

### Recommended resource URIs

```text
catalog://datasets
catalog://datasets/{dataset_id}
catalog://datasets/{dataset_id}/schema
catalog://datasets/{dataset_id}/sample?rows=50
catalog://datasets/{dataset_id}/partitions
catalog://jobs/{job_id}
catalog://jobs/{job_id}/result
catalog://jobs/{job_id}/logs
catalog://artifacts/{artifact_id}
catalog://lineage/{dataset_id}
catalog://policies/{tenant_id}
```

### Why this is the right mapping

MCP resources are intended to expose “context and data, for the user or the AI model to use,” so catalogs, schemas, samples, job outputs, and lineage graphs are a natural fit. [\[modelconte...rotocol.io\]](https://modelcontextprotocol.io/specification/2025-03-26)

### Resource design rules

*   **Immutable by default**: resource reads should not mutate state.
*   **Stable URIs**: make every meaningful object addressable.
*   **Pagination + truncation**: large resources should return summaries plus links.
*   **Typed metadata**: return MIME type, size, freshness, checksum, schema version.
*   **Authorization-aware**: resources should be filtered by tenant, role, dataset policy.

***

## 4.2 Tools (action-oriented, side-effectful, or computational)

Use tools for anything that **does work**.

### Core tool groups

#### A. Discovery / planning

*   `datasets.search`
*   `datasets.resolve`
*   `schemas.compare`
*   `query.explain`

#### B. Computation

*   `sql.run`
*   `sql.validate`
*   `aggregate.run`
*   `transform.run`
*   `stats.compute`
*   `vector.search` (if you include vector store)
*   `graph.traverse` (if you include graph/memory features)

#### C. Storage / writing

*   `artifacts.create`
*   `dataset.write_append`
*   `dataset.write_replace`
*   `table.create_from_result`
*   `file.put`
*   `file.get_signed_url`

#### D. Job control

*   `jobs.submit`
*   `jobs.cancel`
*   `jobs.retry`

#### E. Governance / debugging

*   `lineage.lookup`
*   `audit.lookup`
*   `policy.simulate_access`

This split mirrors what MCP servers already do conceptually in the ecosystem: the filesystem server exposes controlled file operations, the memory server exposes structured persistent state, and compute-like tool servers expose executable actions. [\[modelconte...rotocol.io\]](https://modelcontextprotocol.io/examples), [\[github.com\]](https://github.com/modelcontextprotocol/servers)

***

## 4.3 Prompts (optional but useful)

Prompts should be **workflow templates**, not hard-coded business logic.

Examples:

*   `analysis.plan_from_dataset`
*   `safe_sql_rewrite`
*   `root_cause_investigation`
*   `data_quality_triage`

MCP explicitly supports prompt templates as a first-class feature, but in your design they should help the host orchestrate better interactions rather than becoming the only way to access functionality. [\[modelconte...rotocol.io\]](https://modelcontextprotocol.io/specification/2025-03-26)

***

# 5) Internal data model

Your MCP server needs a strong internal control schema. I recommend these core entities:

## 5.1 Metadata entities

*   **Tenant**
*   **User / principal**
*   **Project / workspace**
*   **Connector**
*   **Dataset**
*   **Logical view**
*   **Schema version**
*   **Artifact**
*   **Job**
*   **Execution**
*   **Policy**
*   **Lineage edge**
*   **Audit event**

## 5.2 Storage classes

*   **Hot metadata** → relational DB (Postgres)
*   **Large tabular outputs** → object storage (Parquet preferred)
*   **Operational cache** → Redis or in-memory cache
*   **Vector/semantic index** → optional, only if the agent truly needs semantic search
*   **Graph/memory state** → optional graph store or relational edge tables

This is partly inspired by the official “memory” reference pattern, which treats persistent agent memory as structured state rather than ad hoc text blobs. [\[modelconte...rotocol.io\]](https://modelcontextprotocol.io/examples), [\[github.com\]](https://github.com/modelcontextprotocol/servers)

***

# 6) Execution model: synchronous vs asynchronous

This is one of the most important design decisions.

## 6.1 Synchronous path (sub-second to a few seconds)

Use for:

*   metadata lookup
*   schema inspection
*   row samples
*   small SQL queries
*   stats over bounded data
*   policy simulation
*   lineage lookup

These should return immediately through `tools/call`.

## 6.2 Asynchronous path (long-running)

Use for:

*   full-table scans
*   heavy joins
*   transformations
*   feature generation
*   report builds
*   large exports
*   multi-step workflows

These should create a **Job** and return:

*   `job_id`
*   status
*   ETA / progress handle
*   result resource URI

This recommendation fits MCP’s broader utility model, which includes progress tracking and cancellation as part of the protocol family. [\[modelconte...rotocol.io\]](https://modelcontextprotocol.io/specification/2025-03-26)

### Recommended async pattern

1.  `jobs.submit` tool call
2.  return `job_id` + `catalog://jobs/{job_id}`
3.  agent polls `resources/read` on the job resource
4.  when ready, result is exposed at `catalog://jobs/{job_id}/result`

This gives the model an inspectable, context-friendly path to results.

***

# 7) Query and computation planner

A strong MCP server should **not** let the LLM directly decide raw engine execution without mediation. Instead, add a **planner/router** in the control plane.

## 7.1 Planner responsibilities

*   validate tool arguments against schema
*   resolve dataset aliases
*   estimate cost
*   choose sync vs async
*   choose execution backend
*   apply row/column/tenant filters
*   inject safety guards
*   determine whether write operations require extra approval

## 7.2 Backend selection examples

*   **DuckDB** for local Parquet / interactive analytics
*   **Postgres** for OLTP-backed structured queries
*   **Spark / Flink / distributed SQL** for large transforms
*   **Python sandbox** for custom analytics only when necessary
*   **Vector engine** for embedding-based similarity

## 7.3 Safe SQL policy

For `sql.run`, always support:

*   parse + lint
*   read-only mode by default
*   explicit `mode = read_only | approved_write`
*   max rows
*   max scan size
*   timeout
*   query fingerprinting
*   result truncation + materialization fallback

This is analogous to how the filesystem reference server is careful about access control boundaries; the lesson is that tool power must be bounded by explicit policy, not by trust in the model. [\[github.com\]](https://github.com/modelcontextprotocol/servers), [\[npmjs.com\]](https://www.npmjs.com/package/@modelcontextprotocol/server-filesystem)

***

# 8) Security model (this is not optional)

Because your server offers **data access** and **computation**, it is security-sensitive by design.

## 8.1 Local vs remote deployment

### Local / desktop / same-machine

Use **stdio** where possible for development or tightly controlled local workflows. The MCP authorization guidance explicitly says stdio implementations should typically use environment-provided credentials rather than the HTTP authorization flow. [\[modelconte...rotocol.io\]](https://modelcontextprotocol.io/specification/2025-03-26/basic/authorization)

### Remote / shared / SaaS / enterprise

Use **HTTPS + OAuth 2.1**. The MCP authorization spec says HTTP-based transports should conform to its OAuth-based authorization mechanism, including metadata discovery and secure token handling. [\[modelconte...rotocol.io\]](https://modelcontextprotocol.io/specification/2025-03-26/basic/authorization)

***

## 8.2 Token strategy

Do **not** pass raw upstream database or SaaS tokens straight through to the MCP client. Instead:

*   authenticate the user/client
*   issue your **own server-scoped token**
*   use your server to access downstream systems with narrowly scoped credentials or token exchange
*   enforce permissions at the **tool + dataset + operation** level

This pattern is strongly supported by Cloudflare’s remote MCP guidance, which explains why the MCP server should issue its own token rather than exposing upstream provider tokens directly. [\[blog.cloudflare.com\]](https://blog.cloudflare.com/remote-model-context-protocol-servers-mcp/)

***

## 8.3 Authorization layers

You need **four layers**:

1.  **Server access**  
    Can this principal connect at all?

2.  **Tool access**  
    Which tools can they see/call?

3.  **Data access**  
    Which datasets/columns/rows can they inspect?

4.  **Execution access**  
    Which compute backends, write actions, or long-running jobs can they trigger?

The MCP spec emphasizes user consent, data privacy, and tool safety, but it leaves implementers responsible for building robust consent and access controls. [\[modelconte...rotocol.io\]](https://modelcontextprotocol.io/specification/2025-03-26)

***

## 8.4 Remote authorization standards to implement

At minimum for remote HTTP:

*   OAuth 2.1
*   PKCE
*   metadata discovery
*   secure redirect URI validation
*   token expiration / rotation
*   HTTPS-only transport

These are all explicitly discussed in the MCP authorization specification. [\[modelconte...rotocol.io\]](https://modelcontextprotocol.io/specification/2025-03-26/basic/authorization)

If you expect open interoperability with many clients, support the discovery and registration flow the MCP docs describe. If your deployment is enterprise-controlled, you can also pre-register clients and tighten trust boundaries. [\[modelconte...rotocol.io\]](https://modelcontextprotocol.io/specification/2025-03-26/basic/authorization)

***

## 8.5 Prompt-injection and tool abuse defenses

Because MCP tools expose real actions, your server should defend against:

*   prompt-injected SQL or filters
*   exfiltration via broad result sets
*   indirect file/path traversal in storage tools
*   “confused deputy” behavior across tenants or connectors
*   unsafe write operations triggered by ambiguous model instructions

The official spec explicitly calls out the security and trust/safety implications of arbitrary data access and tool execution, and the official server repository warns that implementers must add proper safeguards for their own threat model. [\[modelconte...rotocol.io\]](https://modelcontextprotocol.io/specification/2025-03-26), [\[github.com\]](https://github.com/modelcontextprotocol/servers)

### Concrete defenses

*   argument schema validation
*   allowlisted connectors
*   row/column ACL injection
*   max-result enforcement
*   output redaction
*   user approval gates for writes
*   audit every tool call
*   tool descriptions should never be blindly trusted as policy

The official spec directly warns that tool descriptions/annotations should be considered untrusted unless from a trusted server. [\[modelconte...rotocol.io\]](https://modelcontextprotocol.io/specification/2025-03-26)

***

# 9) Multi-tenancy model

If there is any chance you will serve more than one user/team/project, design for multi-tenancy from day one.

## 9.1 Tenant isolation strategies

*   tenant ID on all metadata entities
*   separate storage prefixes / buckets per tenant
*   row-level security in metadata DB
*   isolated connector credentials
*   per-tenant cache namespaces
*   per-tenant job quotas

## 9.2 Session context

Every tool execution should carry:

*   tenant ID
*   user/principal ID
*   project/workspace ID
*   auth scopes
*   request approval mode
*   trace ID
*   policy snapshot version

Remote MCP deployments in practice are explicitly about authenticated, user-consented access across network boundaries, which makes principal/tenant-aware execution essential rather than optional. [\[blog.cloudflare.com\]](https://blog.cloudflare.com/remote-model-context-protocol-servers-mcp/), [\[modelconte...rotocol.io\]](https://modelcontextprotocol.io/specification/2025-03-26/basic/authorization)

***

# 10) Observability and operability

A production MCP server needs better observability than a normal REST API, because the caller is an agent and failures may be semantic rather than purely technical.

## 10.1 What to log

*   session start / initialize
*   tools/list and resources/list visibility
*   every tools/call request + normalized arguments
*   backend query ID
*   rows scanned / rows returned
*   policy decisions
*   approval decisions
*   job lifecycle events
*   token / auth events
*   resource reads of generated outputs

## 10.2 What to measure

*   p50/p95 per tool
*   success vs semantic-failure rate
*   retries
*   async job queue depth
*   cache hit rate
*   bytes scanned
*   result truncation frequency
*   approval friction rate
*   top blocked operations

## 10.3 What to surface to the agent

*   clear machine-readable errors
*   `retryable: true/false`
*   `policy_blocked: true/false`
*   `suggested_next_steps`
*   `resource_uri` for any result that is too large inline

MCP includes error reporting, logging, progress, and cancellation utilities precisely because real deployments need lifecycle visibility and control. [\[modelconte...rotocol.io\]](https://modelcontextprotocol.io/specification/2025-03-26)

***

# 11) Compatibility strategy: build once, work across hosts

Because OpenAI supports remote MCP in the Responses API and the Agents SDK supports hosted MCP tools, Streamable HTTP, and stdio, you should design your server to be **host-agnostic** rather than tied to a single assistant UI. [\[openai.com\]](https://openai.com/index/new-tools-and-features-in-the-responses-api/), [\[openai.github.io\]](https://openai.github.io/openai-agents-js/guides/mcp/)

## Recommended compatibility posture

*   **Primary remote transport**: Streamable HTTP
*   **Secondary local transport**: stdio
*   **Legacy compatibility**: SSE only if you must support older clients
*   **Capability negotiation**: expose only stable features by default
*   **Versioning**: track MCP protocol version independently from your server feature version

This direction also matches the ecosystem shift seen in Cloudflare’s remote MCP work and OpenAI’s guidance around legacy SSE support. [\[blog.cloudflare.com\]](https://blog.cloudflare.com/remote-model-context-protocol-servers-mcp/), [\[openai.github.io\]](https://openai.github.io/openai-agents-js/guides/mcp/)

***

# 12) A concrete API shape I would recommend

## 12.1 Resources

```json
[
  {"uri": "catalog://datasets", "name": "Datasets Catalog"},
  {"uri": "catalog://datasets/sales/schema", "name": "Schema: sales"},
  {"uri": "catalog://datasets/sales/sample?rows=50", "name": "Sample: sales"},
  {"uri": "catalog://jobs/job_123", "name": "Job Status"},
  {"uri": "catalog://jobs/job_123/result", "name": "Job Result"}
]
```

## 12.2 Tools

### `datasets.search`

**Purpose:** find candidate datasets by semantic + keyword matching

**Input**

```json
{
  "query": "customer churn features",
  "limit": 10,
  "project_id": "proj_1"
}
```

### `sql.run`

**Purpose:** validated SQL over approved logical datasets

**Input**

```json
{
  "query": "SELECT region, SUM(revenue) AS rev FROM sales GROUP BY region",
  "dataset_bindings": ["sales"],
  "mode": "read_only",
  "max_rows": 500,
  "timeout_sec": 10
}
```

### `jobs.submit`

**Purpose:** launch long-running computation

**Input**

```json
{
  "task_type": "transform",
  "spec": {
    "source_dataset": "sales_raw",
    "transform": "daily_revenue_by_region"
  },
  "materialize_to": "artifact",
  "priority": "normal"
}
```

### `dataset.write_append`

**Purpose:** append governed records to a writable dataset

**Input**

```json
{
  "dataset_id": "annotations",
  "records": [{ "doc_id": "d1", "label": "approved" }],
  "idempotency_key": "req-123"
}
```

***

# 13) Suggested implementation stack

This is my practical recommendation, not a protocol requirement.

## Option A: pragmatic single-node / team-scale

*   MCP SDK (TypeScript or Python)
*   FastAPI / Node server
*   Postgres for metadata + policies + jobs
*   DuckDB for interactive analytical compute
*   S3-compatible object store for artifacts/results
*   Redis for caching / queue state
*   background workers (Celery / rq / Temporal / simple queue)
*   OAuth via your IdP for remote mode

## Option B: enterprise / scale-out

*   MCP front-end service
*   separate policy service
*   query planner service
*   job service
*   execution backends:
    *   Postgres / Trino / Spark / warehouse
*   object store + catalog
*   OpenTelemetry + SIEM integration
*   centralized secrets manager
*   approval service for human-in-the-loop writes

This layered design is consistent with the fact that MCP standardizes the outer protocol, while production deployments still need their own security, execution, and storage stack. [\[modelconte...rotocol.io\]](https://modelcontextprotocol.io/specification/2025-03-26), [\[github.com\]](https://github.com/modelcontextprotocol/servers)

***

# 14) Rollout plan I would use

## Phase 1 — Local, read-only, narrow scope

*   stdio transport
*   only resources + read-only tools
*   dataset catalog, schema, sample, `sql.run(read_only)`
*   no writes
*   no arbitrary Python
*   single tenant

This lets you validate your tool schemas and agent behavior with minimal blast radius. The official ecosystem began with many local stdio integrations, which remain the simplest path for controlled development. [\[modelconte...rotocol.io\]](https://modelcontextprotocol.io/examples), [\[openai.github.io\]](https://openai.github.io/openai-agents-js/guides/mcp/)

## Phase 2 — Remote, authenticated, policy-aware

*   Streamable HTTP
*   OAuth 2.1
*   tenant-aware authorization
*   async jobs
*   audit logging
*   controlled writes to artifacts only

This matches the direction of remote MCP adoption and the HTTP authorization expectations in the MCP spec. [\[blog.cloudflare.com\]](https://blog.cloudflare.com/remote-model-context-protocol-servers-mcp/), [\[modelconte...rotocol.io\]](https://modelcontextprotocol.io/specification/2025-03-26/basic/authorization)

## Phase 3 — Full data platform

*   connector framework
*   lineage
*   vector/graph options
*   human approval for writes
*   usage quotas
*   external host compatibility testing (OpenAI/others)

This is where your server becomes a reusable platform instead of a point integration. OpenAI’s remote MCP support makes this worthwhile because a well-designed server can serve multiple agent runtimes. [\[openai.com\]](https://openai.com/index/new-tools-and-features-in-the-responses-api/), [\[openai.github.io\]](https://openai.github.io/openai-agents-js/guides/mcp/)

***

# 15) My strongest design recommendations (short version)

If you only take ten things away, take these:

1.  **Model your server as a data fabric, not a thin DB wrapper.**
2.  **Use resources for inspectable data and results; tools for actions and compute.**
3.  **Make long-running compute asynchronous and resource-addressable.**
4.  **Put a planner/policy layer between the LLM and the execution engine.**
5.  **Default everything to read-only.**
6.  **Treat write access as an approval-gated capability.**
7.  **Use stdio locally; use Streamable HTTP + OAuth 2.1 remotely.** [\[modelconte...rotocol.io\]](https://modelcontextprotocol.io/specification/2025-03-26/basic/authorization), [\[openai.github.io\]](https://openai.github.io/openai-agents-js/guides/mcp/)
8.  **Never expose upstream provider tokens directly to clients.** [\[blog.cloudflare.com\]](https://blog.cloudflare.com/remote-model-context-protocol-servers-mcp/)
9.  **Design for multi-tenancy and auditability from day one.**
10. **Keep the MCP surface small, stable, and semantically rich.**

***

# 16) Final suggested blueprint

If I were building this for real, I would ship the following **v1 MCP contract**:

### Resources

*   dataset catalog
*   dataset metadata/schema/sample
*   job status/result/logs
*   artifact metadata/content
*   lineage summaries

### Tools

*   `datasets.search`
*   `datasets.resolve`
*   `sql.validate`
*   `sql.run`
*   `stats.compute`
*   `jobs.submit`
*   `jobs.cancel`
*   `artifacts.create`
*   `dataset.write_append` (approval-gated)
*   `policy.simulate_access`

### Security

*   local stdio for development
*   remote Streamable HTTP for production
*   OAuth 2.1 + PKCE + token rotation
*   server-issued scoped tokens
*   role + dataset + column + row policy
*   audit all tool calls

### Runtime

*   metadata DB + object store + interactive SQL engine + async worker pool

That design is well aligned with the current MCP protocol model, the official example patterns, and the industry trend toward remote authenticated MCP servers consumable by multiple agent hosts. [\[modelconte...rotocol.io\]](https://modelcontextprotocol.io/specification/2025-03-26), [\[modelconte...rotocol.io\]](https://modelcontextprotocol.io/examples), [\[blog.cloudflare.com\]](https://blog.cloudflare.com/remote-model-context-protocol-servers-mcp/), [\[openai.com\]](https://openai.com/index/new-tools-and-features-in-the-responses-api/), [\[openai.github.io\]](https://openai.github.io/openai-agents-js/guides/mcp/)

***

If you want, I can do **one of three next steps**:

1.  turn this into a **concrete MCP interface spec** (`tools/list`, schemas, resource URIs, error model),
2.  design a **TypeScript/Python implementation architecture** with modules/classes, or
3.  tailor the design for a specific workload such as **SQL analytics**, **knowledge memory**, **document processing**, or **multi-tenant enterprise SaaS**.
