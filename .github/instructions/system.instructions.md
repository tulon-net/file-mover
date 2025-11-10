---
applyTo: '**'
---
File Mover – System Instructions (AI System Prompt)

These instructions define how the AI should behave when assisting with the File Mover project. They consolidate architecture, conventions, security & reliability practices, and expected workflows. Treat this as the canonical baseline whenever broader context is lost. When the user refers to "notatki" / "notes" they mean the local (gitignored) path `.github/instructions/notes`.

## 1. Project High-Level Summary
File Mover is a distributed .NET 9 application following **Schematic Driven Development (SDD)** paradigm that schedules, generates, and transfers files (FTP, future SFTP) using an event-driven architecture. Components:
- **API** (`FileMover.Api`) – schedule management, triggers jobs, authentication/authorization, exposes endpoints for GUI.
- **GUI** (`FileMover.Gui`) – separate frontend application connecting to API for managing users, FTP servers, schedules, viewing job statistics/logs/system health.
- **Workers** (`FileMover.Worker.GenerateFile`, `FileMover.Worker.SendFile`) – generate files then transfer them to multiple FTP servers concurrently.
- **Domain** (`FileMover.Domain`) – core entities (`Schedule`, `FileTransferJob`, `FtpServer`, `User`), business logic.
- **Infrastructure** (`FileMover.Infrastructure`) – DB (MariaDB/EF Core), Redis, RabbitMQ, FTP client, dual secret providers.
- **Contracts** (`FileMover.Contracts`) – message contracts (`GenerateFileMessage`, `SendFileMessage`).

**Deployment Modes**:
1. **Azure Deployment**: Azure AD authentication (Service Principal), FTP credentials in Azure Key Vault (accessed via Service Principal).
2. **Local Deployment**: Local user database with authentication, FTP credentials encrypted in local database.

**Key Constraints**:
- Max file size: 1GB (streaming required for transfer).
- Schedules can have multiple FTP servers; transfers occur concurrently with RabbitMQ rate limiting.
- Observability: OpenTelemetry, Prometheus, Grafana (plus optional Elastic Stack, Zabbix). Deployment: Docker Compose (dev), Swarm (prod), optional Kubernetes.

## 2. Core Architectural Principles
1. Event-driven decoupling via RabbitMQ (Separate queues for generation and transfer).
2. Horizontal scalability: workers stateless; scale independently.
3. Separation of concerns: domain logic isolated from infrastructure.
4. Strict secret indirection: configs store vault references, never raw credentials.
5. Observability-first: every message handling step instrumented (traces + metrics + logs).
6. Resilience: retries with backoff for transient FTP/queue/network failures; dead-letter queues for poison messages.

## 3. Coding & Style Guidelines
Follow C# 12 + .NET 9 best practices.
- Naming: PascalCase public members; private fields `_camelCase`.
- Keep methods small, single responsibility; prefer early returns over deep nesting.
- Use async/await throughout for I/O (FTP, DB, message broker).
- Add XML docs for public APIs & complex logic.
- Enforce immutability for value objects; prefer records where suitable.
- Validation: guard domain invariants at construction (e.g., cron expression validity with timezone awareness).
- Unit tests follow AAA pattern; descriptive method names using `Should_` pattern or natural language.
- Commit messages follow Conventional Commits.
- Do not reformat unrelated code when patching.
- **Cron Scheduling**: Use Cronos + NodaTime for timezone-aware scheduling. Store cron expression + IANA timezone; validate and calculate NextRunUtc. See `docs/CRON_SCHEDULING_STRATEGY.md` for implementation details.

## 4. Testing Strategy
Layers:
- Unit: Domain, message handlers logic, utility/services (fast, isolated).
- Integration: DB, Redis, RabbitMQ interactions; can use testcontainers/fake infra if added later.
- Contract: Ensure message DTOs backward compatibility (versioning strategy TBD – ask if adding breaking fields).
- Performance / Load: For high throughput schedule and transfer scenarios (future addition).
Edge cases to consider in implementations:
1. Empty queues / idle workers.
2. Large file sizes; streaming vs buffering (currently unspecified – clarify before large-file feature work).
3. Redis or RabbitMQ temporary outage.
4. Secret provider latency / throttling.
5. Duplicate schedule entries or overlapping cron expressions.

## 5. Security & Compliance
- Never hard-code secrets, passwords, connection strings, tokens.
- Assume TLS for external endpoints when feasible (FTP currently plaintext – plan for FTPS/SFTP migration; note gaps).
- **Dual Secret Management**:
  - **Azure Mode**: Fetch FTP credentials at runtime from Azure Key Vault via Service Principal. Cache securely in memory with short lifetime; avoid persistent storage.
  - **Local Mode**: Store FTP credentials encrypted in local database (use AES-256 or similar; encryption key from environment/config, never in repo). Decrypt only in-memory at runtime.
- Validate all external inputs (schedule definitions, FTP hostnames, paths) to prevent path traversal or injection.
- Log only non-sensitive metadata; scrub credentials and file contents.
- Provide audit trail: message IDs, correlation IDs (trace + log alignment). If missing, propose adding correlation ID field.
- Principle of least privilege: DB user limited to required schema; vault/key access limited to secrets scope.
- **Authentication & Authorization**:
  - **Azure Mode**: Azure AD (Entra ID) authentication; API validates bearer tokens issued by Azure AD; role-based claims for user/admin roles.
  - **Local Mode**: Local user table with hashed passwords (bcrypt/PBKDF2); JWT tokens issued by API; role-based authorization.
  - GUI authenticates users and stores token for subsequent API calls.

## 6. Reliability & Resilience Practices
- Implement retries with exponential backoff + jitter for FTP connect/upload; cap attempt count; send to DLQ after exhaustion.
- Use message acknowledgment patterns: acknowledge only after successful commit of job status.
- Idempotency: generation and send handlers should tolerate reprocessing (design pending – ask if deterministic file content). Consider hashing produced file and storing in Redis to avoid duplicate uploads.
- Health checks: API exposes readiness/liveness endpoints; workers should optionally expose their health if converted to sidecar or metrics endpoint.
- Graceful shutdown: drain in-flight messages before exiting (ensure cancellation tokens wired through).

## 7. Observability Standards
- Trace each message lifecycle: receive → process steps → publish next message.
- Minimum metrics: message throughput, processing duration, failure counts, retry counts, queue length (if polled), file transfer size histogram.
- Structured logging (JSON preferred) with fields: component, operation, messageId, scheduleId, jobId, correlationId, attempt, durationMs, outcome.
- Expose Prometheus metrics endpoint for API and workers; ensure counters/gauges naming: `filemover_` prefix.

## 8. Deployment & Configuration Conventions
- Use `.env` for local dev only; production uses Swarm/K8s secrets & configs.
- Version images with semantic version tags and immutable digests in production manifests.
- Separate infrastructure concerns (`infrastructure/`) from application code; no production credentials inside repo.
- Keep Kubernetes manifests minimal; resource requests/limits to be defined (currently missing – highlight gap).

## 9. Gaps / Missing Documentation (Current Assessment)
1. ~~Authentication & Authorization for API~~ **RESOLVED**: Azure AD (Service Principal) for Azure deployment; local user DB + JWT for local deployment.
2. ~~Detailed secret provider integration~~ **RESOLVED**: Azure Key Vault (Azure mode) or encrypted DB storage (local mode).
3. FTP abstraction (interface, retry policies, FTPS/SFTP roadmap).
4. Message contract versioning / backward compatibility strategy.
5. Error taxonomy & standardized application error codes.
6. Operational runbooks (incident response, scaling playbooks, backup restore procedures). Partial backup in deployment docs, lacks restore steps.
7. Data model schema (MariaDB tables for schedules, FTP servers, users, job history; migrations strategy & tooling – EF Core migrations not shown).
8. ~~Redis data structures layout & TTL policies~~ **RESOLVED**: See section 20 (Redis Strategy).
9. Security hardening (TLS termination, network segmentation, container security scanning, SBOM generation).
10. Performance targets / SLOs (latency, throughput, MTTR, error budget).
11. ~~File content handling~~ **RESOLVED**: Max 1GB; streaming required for transfer. Temp storage path & cleanup policy TBD.
12. Correlation ID strategy (generation & propagation).
13. Infrastructure IaC (Terraform, Bicep, Helm charts) not present.
14. Observability dashboards definitions (only path references; no metrics taxonomy document).
15. **GUI specifications**: Technology stack (React/Angular/Blazor?), authentication flow, API contract, deployment model.
16. **Concurrent job limits**: RabbitMQ rate limiting / prefetch configuration for controlling parallel FTP transfers.
17. **Multi-server schedule handling**: Data model for Schedule → FtpServer many-to-many relationship; orchestration logic for parallel transfers.

## 10. When Implementing New Features
1. Clarify requirements & constraints; record questions in `.github/instructions/notes`.
2. Define contract (inputs, outputs, side effects, error modes) before coding.
3. Add/adjust tests first (happy path + at least 1 failure or edge case).
4. Keep patches focused: only necessary files.
5. Update docs if public behavior or operation changes.
6. Re-run `dotnet build` and `dotnet test` after changes.
7. Validate backward compatibility of message contracts (avoid removing fields; add optional with defaults).

## 11. Interaction Pattern (AI Behavior)
- Always gather context first (search/read) before large edits.
- Maintain and update a todo list for multi-step tasks.
- Prefer patches via precise diffs; avoid unrelated formatting.
- Provide reasoning transparently but concise unless user requests detail.
- Automatically suggest small, low-risk adjacent improvements (e.g., add missing XML docs, simple unit test) after primary task.
- Store clarifying questions or decision logs in `.github/instructions/notes` when they matter long-term.
- If blocked by missing info, state assumption(s), proceed, and log assumption in notes.

## 12. Performance & Resource Considerations
- **Max file size: 1GB**. Use streaming for file generation and FTP upload; avoid loading entire file into memory.
- Use async streaming APIs for file upload/download (e.g., `Stream.CopyToAsync`, FTP client streaming methods).
- Consider batching for high-volume schedules to reduce per-message overhead.
- **Concurrent transfer limits**: Configure RabbitMQ prefetch count to control max parallel FTP transfers per worker; prevents resource exhaustion.
- For schedules with multiple FTP servers, fan out to separate messages per server; coordinate completion via Redis or DB status tracking.

## 13. Security Enhancements To Propose (If Opportunity Arises)
- Transition FTP → SFTP/FTPS with configurable protocol and enforced encryption.
- Add secret rotation hooks / schedule.
- Implement centralized authorization (JWT/OAuth2) for API endpoints; document required scopes.
- Add automated dependency vulnerability scanning (GitHub Dependabot, Trivy, etc.).
- Provide SBOM (e.g., `dotnet sbom`) and sign container images.

## 14. Error Handling Standard
Every handler should return a structured result (Success flag, ErrorCode, Retryable boolean, Message). Log at WARN for retryable, ERROR for terminal failures. Map exceptions to known error codes (e.g., `FTP_TIMEOUT`, `FTP_AUTH_FAILED`, `SCHEDULE_INVALID`, `SECRET_NOT_FOUND`).

## 15. Notes Usage (`.github/instructions/notes`)
- Gitignored private workspace for ephemeral planning & questions.
- Record: unanswered questions, assumptions made, follow-up tasks, design decisions. Keep entries dated and terse.
- Never store secrets or credentials. Plain text only.

## 16. Assumptions (Until Clarified)
- Files are generated deterministically from schedule + timestamp.
- ~~Single tenant context; multi-tenant not yet required.~~ **UPDATED**: Single tenant for file processing; GUI supports multiple users with role-based access (admin vs regular user).
- Cron expressions validated before persistence (validation implementation to be confirmed).
- **Max file size: 1GB**; streaming is mandatory for generation and transfer.
- GUI is a separate SPA (Single Page Application) connecting to API; shares no backend process with API/workers.

## 17. Do Not
- Expose secrets in logs or patches.
- Introduce breaking API changes without version bump & migration doc.
- Commit `.github/instructions/notes` (already gitignored) or any local environment secrets.

## 18. Checklist Before Merging Feature
- Tests added/updated & passing.
- Build succeeds (`dotnet build`).
- Message contracts backward compatible.
- Documentation updated where relevant.
- No sensitive info leaked.
- Observability: metrics/traces added if new operation.
- Performance impact considered (no obvious N+1 DB queries).

## 19. Redis Strategy
Redis serves as an ephemeral, high-performance cache and coordination layer for job state tracking, rate limiting, and distributed locking. Use StackExchange.Redis client library.

**Key Naming Convention**: Use namespaced keys with prefixes for clarity and TTL management.
- `job:{jobId}:status` – job execution status (e.g., "Queued", "InProgress", "Completed", "Failed").
- `job:{jobId}:progress` – optional progress percentage or step info (0-100 integer).
- `job:{jobId}:metadata` – JSON serialized metadata (schedule ID, FTP servers list, file size, start time).
- `schedule:{scheduleId}:lastrun` – timestamp of last successful execution.
- `lock:{resource}` – distributed lock for preventing concurrent execution of same schedule.
- `ratelimit:{workerId}:{minute}` – sliding window counter for rate limiting transfers per worker.

**Data Structures**:
- **Strings**: Job status, progress, timestamps.
- **Hashes**: Job metadata when multiple fields need atomic updates (e.g., `HSET job:{jobId}:meta startTime endTime fileSize`).
- **Lists**: Optional audit trail or recent job history per schedule (capped with `LTRIM`).
- **Sets**: Track active jobs or FTP server connection pools if needed.
- **Sorted Sets**: Priority queues or delayed retry schedules (score = Unix timestamp).

**TTL Policies**:
- Job status keys: 24 hours after job completion (allows GUI to fetch recent history; purge older entries).
- In-progress job keys: No expiry while active; set TTL to 1 hour on transition to terminal state (Completed/Failed) to allow debugging.
- Locks: 5-10 minutes max TTL with renewal/heartbeat if long operation.
- Rate limit counters: 1-5 minute TTL depending on window size.
- `schedule:lastrun`: 7 days or indefinite (small footprint).

**Concurrency & Atomicity**:
- Use `SETNX` or `SET key value NX EX seconds` for distributed locks.
- Use Lua scripts for atomic multi-key operations (e.g., check-and-set status transition, decrement quota).
- Worker acknowledges RabbitMQ message only after Redis status update committed.

**Idempotency Support**:
- Before processing, check `job:{jobId}:status`; if already "Completed", skip re-execution.
- Store file hash/checksum in Redis or DB to detect duplicates.

**Fallback & Resilience**:
- If Redis unavailable, log warning and proceed (degraded mode: no caching, status fetched from DB).
- Implement circuit breaker for Redis client; retry with exponential backoff.
- Persist critical job state in MariaDB for durability; Redis is ephemeral cache layer only.

**Service Interface** (example, adapt to project conventions):
```csharp
public interface ICacheService
{
    Task SetAsync(string key, string value, TimeSpan? expiry = null);
    Task<string?> GetAsync(string key);
    Task<bool> SetIfNotExistsAsync(string key, string value, TimeSpan expiry);
    Task DeleteAsync(string key);
    Task<long> IncrementAsync(string key);
}
```

**Configuration Settings** (Azure/local modes):
- `Redis:ConnectionString` (e.g., `localhost:6379` or Azure Redis Cache endpoint).
- `Redis:DefaultTtlMinutes` – fallback TTL when not specified.
- `Redis:EnableSsl` – true for Azure/production.
- `Redis:Password` – fetch from Key Vault (Azure) or encrypted config (local).

**Monitoring**:
- Expose metrics: Redis connection pool status, cache hit/miss ratio, key count, memory usage.
- Alert on connection failures or high memory consumption.

## 20. Language Policy
- All documentation, code comments, XML docs, commit messages, PR descriptions, and user-facing descriptions MUST be written in English.
- Day-to-day conversation with Copilot/AI assistants or in issues/discussions may be in any language; the resulting artifacts (docs/code/comments) should still be produced in English.

## 21. Versioning Policy (Semantic Versioning)
- Use Semantic Versioning 2.0.0 across the project: application, libraries/packages, Docker images, and deployment artifacts.
- Version format: `MAJOR.MINOR.PATCH[-PRERELEASE][+BUILD]`.

Bump rules:
- MAJOR: Backward-incompatible changes to public APIs or message contracts; destructive DB schema changes; behavioral changes that require consumer updates.
- MINOR: Backward-compatible features; additive message fields with safe defaults; new endpoints or optional parameters.
- PATCH: Backward-compatible bug fixes; non-breaking refactors; performance improvements without behavior change; docs.

Pre-releases:
- Use `-alpha.N`, `-beta.N`, `-rc.N` suffixes for pre-release channels.
- Only promote `-rc` to stable when all checks pass (build, tests, security scans).

Tagging & releases:
- Tag git releases as `vX.Y.Z`.
- Generate release notes from Conventional Commits; map `feat`→MINOR, `fix`→PATCH, any `BREAKING CHANGE` footer or `!` in type/scope → MAJOR.
- Docker images MUST be tagged with `vX.Y.Z` and pushed with immutable digests; `latest` may point only to the latest stable on `main`.

.NET packages & assemblies:
- Keep versions consistent via a central version source (e.g., `Directory.Build.props`) or synchronized per `csproj`.
- Ensure assembly/file versions match package version for published artifacts.

API and message contracts:
- Within the same MAJOR, maintain backward compatibility; additive-only fields preferred.
- Removing fields, changing types/semantics, or altering required fields requires a MAJOR bump and a migration note.

Database migrations:
- Prefer backward-compatible migrations within the same MAJOR; guard destructive changes behind MAJOR or provide a clear upgrade path.

CI enforcement:
- CI should validate SemVer tag format, enforce version consistency across artifacts, and fail on non-compliant release tags.

Refer back to this system prompt whenever context is lost. Update it if architecture or standards evolve (changes should be purposeful and documented in notes).
