# Rhyno Enterprise Automation & ERP Platform

<p align="center">
  <img src="https://img.shields.io/badge/.NET-10.0-512BD4?style=for-the-badge&logo=dotnet&logoColor=white" alt=".NET 10" />
  <img src="https://img.shields.io/badge/Architecture-Multi--Tenant-00ADD8?style=for-the-badge&logo=googledocs&logoColor=white" alt="Multi-Tenant" />
  <img src="https://img.shields.io/badge/Docker-Ready-2496ED?style=for-the-badge&logo=docker&logoColor=white" alt="Docker Ready" />
  <img src="https://img.shields.io/badge/License-Proprietary-808080?style=for-the-badge&logo=openaccess&logoColor=white" alt="License" />
  <img src="https://img.shields.io/badge/Security-Audited-006600?style=for-the-badge&logo=trustpilot&logoColor=white" alt="Security Audited" />
</p>

**Rhyno** is an enterprise-grade modular ERP and office automation platform built on **.NET 10**, engineered for organizations that demand strict multi-tenancy isolation, regulatory compliance, and process automation at scale. It unifies financial accounting, inventory management, human capital, workflow orchestration, document management, and AI-assisted productivity into a single, containerized deployable artifact.

---

## Architecture Overview

Rhyno follows a **Modular Monolith** architectural style — each business capability is an independent module with its own persistence, domain logic, and inter-module communication through an asynchronous outbox pattern. Tenants are fully isolated at the database, queue, cache, and storage layers.

```
┌──────────────────────────────────────────────────────────────────────────────┐
│                          Nginx Reverse Proxy (:80/:443)                       │
│                   HSTS / CSP / Rate Limiting / TLS Termination                 │
└──────────────────────────────┬───────────────────────────────────────────────┘
                               │
┌──────────────────────────────▼───────────────────────────────────────────────┐
│                      Rhyno ASP.NET Core Application (:8080)                   │
│                                                                               │
│  ┌──────────┐ ┌──────────┐ ┌──────────┐ ┌──────────┐ ┌───────────────────┐  │
│  │ Finance  │ │Inventory │ │  Office  │ │  Human   │ │   AI Assistant    │  │
│  │ Ledger   │ │  Stock   │ │ Letters  │ │ Capital  │ │    (OpenAI)       │  │
│  │ Invoices │ │ Counting │ │ Inbox    │ │ Payroll  │ └───────────────────┘  │
│  │ TB/BS    │ │ Transfer │ │ Archive  │ │ Leaves   │                        │
│  └────┬─────┘ └────┬─────┘ └────┬─────┘ └────┬─────┘ ┌───────────────────┐  │
│       │             │            │            │       │  Workflow Engine  │  │
│       │    ┌────────┴────────────┴────────────┘       │ React Flow BPMN   │  │
│       │    │                                          │ Process Mining    │  │
│  ┌────▼────▼────┬──────────────────┬──────────────────┤ SLA & Escalation  │  │
│  │  RBAC + ABAC │  Audit Trail     │  SignalR Hubs    └───────────────────┘  │
│  │  Field Mask  │  Immutable Logs  │  Real-Time Notif.                       │
│  └──────────────┴──────────────────┴─────────────────────────────────────────┘
│                                                                               │
│  ┌───────────────────────────────────────────────────────────────────────┐   │
│  │                   Outbox Pattern → RabbitMQ → Worker                   │   │
│  │    Workflow Events / Notifications / SLA Processing / Escalations     │   │
│  └───────────────────────────────────────────────────────────────────────┘   │
└───────────────────────────────────────────────────────────────────────────────┘
                               │
        ┌──────────────────────┼──────────────────────┐
        │                      │                      │
┌───────▼──────┐  ┌────────────▼────────┐  ┌─────────▼─────────┐
│  SQL Server  │  │  Redis 7 (Alpine)    │  │  Seq Structured   │
│    2022      │  │  Cache & Sessions    │  │  Logging          │
└──────────────┘  └─────────────────────┘  └───────────────────┘
```

### Key Architectural Patterns

| Pattern | Implementation |
|---------|---------------|
| **Multi-Tenancy** | `DatabasePerTenant` and `SchemaPerTenant` isolation via `ITenantIsolationService`. Each tenant resolves its own connection string, schema, queue namespace, cache prefix, storage root, and log scope. |
| **Outbox Pattern** | Transactional outbox with RabbitMQ. Events are persisted atomically with domain changes via `AuditSaveChangesInterceptor`, then published asynchronously by the background worker. |
| **Row-Level Security (RLS)** | `RowLevelSecuritySaveChangesInterceptor` enforces intra-tenant data scoping at the EF Core level — used for department-level partitioning, not tenant isolation. |
| **Authorization** | Dual-layer: **RBAC** via `PermissionAccessService` + custom `PermissionAuthorizationPolicyProvider`, and **ABAC** via `AbacAuthorizationService` with segregation-of-duties (`ISegregationOfDutiesService`). |
| **CQRS-lite** | Module-level application services (`FinancialAppService`, `WorkflowExecutionService`, etc.) encapsulate write logic; read-optimized queries use dedicated DTOs and saved views. |

---

## Module Matrix

### Finance & Accounting
- General ledger with double-entry booking
- Trial balance, balance sheet, and income statement generation
- Voucher renumbering and period closing
- Invoice creation and financial invoice service

### Inventory & Warehouse
- Stock ledger with opening balance tracking
- Inventory counting and cycle count
- Warehouse transfer requests and inter-warehouse transfers
- Issuance and receipt workflows
- Product master data with extended fields

### Office Automation
- Letter management with reply chaining and copy recipients
- Digital signatures (`IDigitalSignatureService`)
- Document archive with versioning (`IDocumentVersioningService`)
- Work inbox with workflow-aware task routing

### Human Capital Management
- Employee lifecycle tracking with status history
- Salary history and payroll processing
- Leave management with workflow-integrated approval
- Insurance (Bimeh) module

### Workflow Engine
- **React Flow BPMN** visual workflow designer (`WorkflowUI/`)
- Adaptive case management with dynamic connectors
- **Process Mining** engine (`ProcessMiningService`) for bottleneck analysis
- SLA monitoring and automated escalation (`WorkflowSlaScheduler`)
- Decision engine with rule-based routing (`IDecisionEngine`)
- Workflow governance with versioned deployment metadata

### AI Assistant
- OpenAI integration (`AiService`) with configurable model (default: `gpt-4o-mini`)
- SQL safety layer (`AiSqlSafetyService`) for sanitized data queries
- Per-tenant rate limiting on AI endpoints

### Real-Time Collaboration
- **SignalR** hub (`/automationHub`) for live notifications
- Persistent notification store with read/unread tracking
- Kanban board views for task visualization

### Platform Services
- Global search across all modules
- Saved views and table preferences per user
- Organization calendar with events
- Scheduled report generation (`ScheduledReportBackgroundService`)
- Document retention background service
- Management dashboard with aggregated KPIs

---

## Tech Stack & Infrastructure

### Backend

| Component | Technology | Version |
|-----------|-----------|---------|
| Runtime | .NET (ASP.NET Core MVC + Razor Pages) | 10.0 |
| ORM | Entity Framework Core | 10.0.8 |
| Identity | ASP.NET Core Identity + JWT + API Key | 10.0.8 |
| State Machine | Stateless | 5.20.1 |
| Excel I/O | ClosedXML / EPPlus | 0.105.0 / 7.6.1 |
| AI SDK | OpenAI (official) | 2.10.0 |
| Resilience | Polly | 7.2.4 |

### Frontend

| Component | Technology |
|-----------|-----------|
| UI Framework | Razor Views + Tailwind CSS |
| Workflow Designer | React + React Flow (BPMN) |
| Real-Time | SignalR (WebSocket) |
| Charts | Chart.js |

### Infrastructure & DevOps

| Component | Technology | Version |
|-----------|-----------|---------|
| Database | SQL Server | 2022-latest |
| Message Broker | RabbitMQ (Alpine) | 3-management |
| Cache & Sessions | Redis (Alpine) | 7 |
| Reverse Proxy | Nginx (Alpine) | latest |
| Structured Logging | Seq (datalust/seq) | latest |
| Container Runtime | Docker + Docker Compose | 3.8+ |
| Health Checks | ASP.NET Core Health Checks | – |
| Swagger | Swashbuckle | 7.2.0 |

---

## Quickstart: Docker Deployment

### Prerequisites
- [Docker](https://docs.docker.com/get-docker/) 20.10+ and Docker Compose v2+
- Git (optional, for cloning)

### 1. Clone & Configure

```bash
git clone https://github.com/soheil-asgari/rhyno-automation.git
cd rhyno-automation
cp .env.example .env
```

**Edit `.env`** and replace all default passwords with strong, unique credentials:

```ini
DB_PASSWORD=YourStrongSqlPassword123!
RABBITMQ_PASS=YourStrongRabbitPassword123!
REDIS_PASSWORD=YourStrongRedisPassword123!
SEQ_PASSWORD=YourStrongSeqPassword123!
API_KEY=YourGeneratedApiKey
```

### 2. Launch the Stack

```bash
docker-compose up -d
```

Docker Compose will orchestrate the following services:

| Service | Container Name | Port(s) | Purpose |
|---------|---------------|---------|---------|
| `sqlserver` | `rhyno-sqlserver` | `1433` | SQL Server 2022 Express |
| `rabbitmq` | `rhyno-rabbitmq` | `5672`, `15672` | AMQP broker + management UI |
| `redis` | `rhyno-redis` | `6379` | In-memory cache & session store |
| `seq` | `rhyno-seq` | `5341` | Structured log aggregation |
| `app` | `rhyno-app` | `8080` | Main ASP.NET Core application |
| `worker` | `rhyno-worker` | – | Background job processor (Outbox, SLA) |
| `nginx` | `rhyno-nginx` | `80`, `443` | TLS-terminating reverse proxy |

### 3. Run Database Migrations

Once SQL Server is healthy (Docker healthcheck passes), run migrations:

```bash
docker exec -it rhyno-app dotnet ef database update --context PlatformDbContext
docker exec -it rhyno-app dotnet ef database update --context IdentityDbContext
docker exec -it rhyno-app dotnet ef database update --context WorkflowDbContext
docker exec -it rhyno-app dotnet ef database update --context FinanceDbContext
docker exec -it rhyno-app dotnet ef database update --context InventoryDbContext
docker exec -it rhyno-app dotnet ef database update --context OfficeDbContext
```

### 4. Access the Platform

| Endpoint | URL | Credentials |
|----------|-----|-------------|
| **Application** | `http://localhost:8080` | Bootstrap admin (see `.env`) |
| **Swagger API Docs** | `http://localhost:8080/swagger` | API Key header (`X-Api-Key`) |
| **RabbitMQ Management** | `http://localhost:15672` | User: `rhyno` / Pass: from `.env` |
| **Seq Log Dashboard** | `http://localhost:5341` | Admin / Pass: from `.env` |
| **Health Check** | `http://localhost:8080/health` | Public |

### 5. Verify All Services

```bash
docker-compose ps
```

All services should show `Up` (healthy). Check logs if any service is restarting:

```bash
docker-compose logs -f app
```

---

## Security & Compliance Highlights

Rhyno implements a defense-in-depth security model spanning the application, infrastructure, and data layers:

### Application Layer
| Control | Implementation |
|---------|---------------|
| **Auto-Antiforgery** | `AutoValidateAntiforgeryTokenAttribute` applied globally to all POST/PUT/PATCH/DELETE requests |
| **Account Lockout** | 5 failed attempts → 15-minute lockout (`MaxFailedAccessAttempts=5`, `DefaultLockoutTimeSpan=15min`) |
| **Password Policy** | Min 8 chars; requires digit, uppercase, lowercase, and non-alphanumeric |
| **RBAC + ABAC** | Role-based permissions plus attribute-based access control with segregation-of-duties enforcement |
| **API Key Auth** | Header-based (`X-Api-Key`) authentication for Swagger and external API consumers |
| **Field-Level Masking** | `SecurityFieldMaskingService` redacts sensitive fields (e.g., national ID, salary data) based on user permissions |
| **Input Sanitization** | `NormalizeInputFilter` applied globally to all controller actions |

### Infrastructure Layer
| Control | Implementation |
|---------|---------------|
| **HSTS Header** | `Strict-Transport-Security: max-age=31536000; includeSubDomains; preload` |
| **CSP Header** | `Content-Security-Policy` with `default-src 'self'` — restricts script/style/font/image sources |
| **X-Frame-Options** | `DENY` — prevents clickjacking |
| **X-Content-Type-Options** | `nosniff` — prevents MIME-type sniffing |
| **Referrer-Policy** | `strict-origin-when-cross-origin` |
| **TLS** | TLS 1.2 and 1.3 only; modern cipher suites; OCSP stapling enabled |
| **Rate Limiting** | Per-tenant global limiter + dedicated AI endpoint limiter; 429 responses with quota reasons |
| **Server Tokens** | `server_tokens off` — no version disclosure |

### Data & Audit Layer
| Control | Implementation |
|---------|---------------|
| **Immutable Audit Trail** | `AuditSaveChangesInterceptor` captures all entity changes with before/after snapshots; audit records are append-only |
| **Audit Retention** | Configurable retention policy (`AuditRetentionOptions`) with automated archival |
| **Row-Level Security** | EF Core interceptor enforces data isolation at the intra-tenant department level |
| **Tenant Isolation** | Separate databases or schemas per tenant; no shared data at rest |
| **Digital Signatures** | `IDigitalSignatureService` for document authenticity and non-repudiation |
| **Two-Factor Auth** | TOTP-based 2FA via `ITotpService` |

---

## Project Structure

```
rhyno-automation/
├── Areas/Identity/          # ASP.NET Core Identity Razor Pages
├── Controllers/             # MVC Controllers (per module)
├── Controllers/Api/         # REST API endpoints
├── Data/                    # DbContexts, interceptors, audit infrastructure
├── DTOs/                    # Data Transfer Objects
├── DTOs/Api/                # API-specific DTOs
├── Filters/                 # Global action filters (NormalizeInput, PermissionAccess)
├── Hubs/                    # SignalR hubs (AutomationHub)
├── Migrations/              # EF Core migrations (per module DbContext)
├── Models/                  # Domain models and view models
├── Modules/                 # Modular bounded contexts
│   ├── Finance/             #   Ledger, Invoices, TrialBalance
│   ├── Identity/            #   User/Role stores
│   ├── Inventory/           #   Stock, Counting, Transfer
│   ├── Office/              #   Letters, Archive, WorkInbox
│   ├── Platform/            #   Tenant registry, saved views
│   └── Workflow/            #   Workflow engine, process mining
├── Pages/                   # Razor Pages
├── Services/                # Application & domain services
│   ├── Auditing/            #   Audit logger, retention
│   ├── Connectors/          #   Integration connectors
│   ├── Decisioning/         #   Decision engine
│   ├── HealthChecks/        #   Custom health checks
│   ├── Middleware/           #   Custom middleware
│   ├── Outbox/              #   Transactional outbox + RabbitMQ publisher
│   ├── Security/            #   ABAC, field masking, SoD, API key auth
│   └── Tenancy/             #   Tenant isolation, resolution, quotas
├── Views/                   # Razor views (per module)
├── WorkflowUI/              # React Flow BPMN designer (SPA)
├── wwwroot/                 # Static assets (Tailwind CSS, JS, images)
├── docker-compose.yml       # Full-stack Docker orchestration
├── Dockerfile               # Multi-stage .NET 10 container build
├── nginx.conf               # Nginx reverse proxy with security headers
├── install.ps1              # Windows installation helper
└── script.sql               # Database initialization script
```

---

## Documentation

| Document | Description |
|----------|-------------|
| [Installation Guide](docs/Installation.md) | Detailed setup instructions for bare-metal deployments |
| [Admin Guide](docs/AdminGuide.md) | Platform administration and configuration |
| [User Guide](docs/UserGuide.md) | End-user feature walkthrough |
| [Migration & Roles](docs/MigrationAndRoles.md) | Database migration strategy and role setup |
| [Backup & Restore](docs/BackupRestore.md) | Data protection and recovery procedures |
| [Deployment Guide](docs/Deployment.md) | Production deployment best practices |
| [Multi-Tenancy Isolation](docs/multi-tenancy-isolation.md) | Tenant isolation architecture deep dive |
| [Product Readiness Gaps](docs/product-readiness-gaps-analysis.md) | Known gaps and roadmap items |
| [UI/UX Roadmap](docs/ui-ux-roadmap.md) | Frontend modernization plan |
| [Architectural Review](docs/architectural_review.txt) | Security and architecture audit findings |

---

## License

Proprietary. All rights reserved. Unauthorized distribution, reproduction, or use is strictly prohibited.