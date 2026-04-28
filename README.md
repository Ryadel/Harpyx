# Harpyx

<p align="center">
  <img src="src/Harpyx.WebApp/wwwroot/img/logo/Logo-Grayscale.svg" alt="Harpyx logo" width="240" />
</p>

**Harpyx is a multi-tenant, self-hostable platform for retrieval-augmented generation (RAG) over private document corpora.** It lets organizations upload their own documents, index them into searchable vector embeddings, and chat with them through the LLM provider of their choice.

Harpyx is built in .NET 10 and ships as two cooperating services — a web-facing `Harpyx.WebApp` and a background `Harpyx.Worker` — orchestrated with Docker Compose on top of SQL Server, MinIO, RabbitMQ, Redis, OpenSearch, and ClamAV.

## What it does

- **Document ingestion** — uploads are virus-scanned, stored in object storage, and queued for asynchronous parsing. Containers (ZIP, RAR, 7z, tar.gz, .msg, .eml) are expanded; PDFs, Office documents, RTF, EPUB, HTML, plain-text, images (with OCR), and structured files (CSV, JSON, XML, YAML) are extracted, chunked, and embedded.
- **Multi-provider RAG** — per-user BYO API keys for OpenAI, Anthropic Claude, and Google Gemini, with AES-256-GCM encryption at rest. Chat, embedding, and OCR models can be overridden independently at workspace or project scope.
- **Project-scoped chat** — tenants group users into workspaces; workspaces contain projects; projects contain documents, prompts, and chat sessions grounded on the documents they own.
- **Multi-tenancy with roles** — platform access is split between Admin and Standard users; tenant memberships provide the operational role model. User access is allowlist-controlled and audited.
- **Self-hosted usage controls** — instance-wide limits gate tenant, workspace, project, document, storage, API, OCR, and RAG usage without commercial tiers.
- **Production-grade infrastructure** — health checks for every backing service, OpenTelemetry traces/metrics, Serilog structured logging, forwarded-headers and rate-limiting policies, CSRF protection, ClamAV upload scanning, and auditable security events.

## Why it was built

Off-the-shelf RAG products either lock you into a specific LLM vendor, require you to ship your documents to a third party, or are one-tenant toys that don't survive contact with a real organization. Harpyx was built to fill the gap:

- **Sovereignty**: every byte stays inside your infrastructure. The only external network calls are the LLM API calls the user explicitly configured, using the user's own API keys.
- **Pluggability**: LLM providers, storage backends, and auth are decoupled from business logic via an explicit Clean Architecture split (`Domain` → `Application` → `Infrastructure` / `WebApp` / `Worker`), so swapping any of them does not leak into the rest of the codebase.
- **Operational realism**: the default deployment assumes reverse proxies, rate limits, malware scanning, key rotation, structured audit logs, and separate web / worker processes from day one — because that's what it takes to host documents for real teams.

## Quick start

```bash
cp .env.example .env          # then fill in the secrets
docker compose up --build
```

The WebApp is exposed at `http://localhost:8080`. Liveness is at `/health/live`, readiness at `/health/ready`.

For local development against Docker-hosted dependencies (SQL, MinIO, Redis, RabbitMQ, OpenSearch, ClamAV), run the WebApp/Worker from your IDE with the repository-root `.env` in place.

## Tech stack

ASP.NET Core 10 · EF Core 9 (SQL Server) · Razor Pages + Tailwind/DaisyUI · Microsoft.Identity.Web (Entra ID) · Google OAuth · MinIO · RabbitMQ · Redis · OpenSearch · ClamAV · OpenTelemetry · Serilog · Testcontainers.

## Documentation

Extensive developer & operator documentation lives in the project's [Wiki](wiki/):

- **[Home](https://github.com/Ryadel/Harpyx/wiki/Home)** — table of contents
- **[Architecture](https://github.com/Ryadel/Harpyx/wiki/Architecture)** — clean-architecture layers, service boundaries, data flow
- **[Configuration](https://github.com/Ryadel/Harpyx/wiki/Configuration)** — the override chain, env-var convention, Azure Key Vault
- **[Authentication and Authorization](https://github.com/Ryadel/Harpyx/wiki/Authentication-and-Authorization)** — Entra ID, Google OAuth, role model, allowlist
- **[RAG Pipeline](https://github.com/Ryadel/Harpyx/wiki/RAG-Pipeline)** — ingestion, extraction, chunking, embeddings, retrieval
- **[Jobs and Messaging](https://github.com/Ryadel/Harpyx/wiki/Jobs-and-Messaging)** — RabbitMQ queues, worker lifecycle, retries
- **[Storage and Persistence](https://github.com/Ryadel/Harpyx/wiki/Storage-and-Persistence)** — SQL Server, MinIO, Redis, OpenSearch
- **[Security](https://github.com/Ryadel/Harpyx/wiki/Security)** — malware scanning, encryption at rest, upload policy, audit
- **[Observability](https://github.com/Ryadel/Harpyx/wiki/Observability)** — OpenTelemetry, Serilog, health checks
- **[Development](https://github.com/Ryadel/Harpyx/wiki/Development)** — prerequisites, build, tests, migrations
- **[Deployment](https://github.com/Ryadel/Harpyx/wiki/Deployment)** — Docker Compose topology, container images, scaling notes
