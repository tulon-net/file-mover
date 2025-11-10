# File Mover Architecture

## Overview

File Mover is a distributed .NET 9 application following **Schematic Driven Development (SDD)** paradigm, designed to schedule, generate, and transfer files from local storage to FTP servers with enterprise-grade reliability and observability. The system supports dual deployment modes (Azure cloud-native and local on-premises) with flexible authentication and secret management strategies.

## Architectural Paradigm: Schematic Driven Development (SDD)

SDD emphasizes:
1. **Schema-first design**: Define data models, message contracts, and API schemas before implementation.
2. **Contract validation**: Ensure backward compatibility through versioned contracts and automated schema validation.
3. **Documentation as source of truth**: Architecture diagrams, API specs (OpenAPI), and message schemas maintained alongside code.
4. **Type safety**: Leverage strong typing in C#, database schemas, and message formats to catch errors early.
5. **Evolvability**: Design for change with additive-only contracts and feature flags.

## System Components

### 1. API Service (FileMover.Api)
- **Purpose**: Schedule management and configuration
- **Technology**: ASP.NET Core Web API (.NET 9)
- **Responsibilities**:
  - CRUD operations for schedules
  - Trigger file generation jobs
  - Health checks and monitoring endpoints
  - OpenTelemetry instrumentation

### 2. GenerateFile Worker (FileMover.Worker.GenerateFile)
- **Purpose**: Process file generation requests
- **Technology**: .NET Worker Service
- **Responsibilities**:
  - Consume messages from GenerateFile queue
  - Generate/prepare files for transfer
  - Store job status in Redis
  - Publish messages to SendFile queue
  - OpenTelemetry tracing

### 3. SendFile Worker (FileMover.Worker.SendFile)
- **Purpose**: Transfer files to multiple FTP servers concurrently
- **Technology**: .NET Worker Service
- **Responsibilities**:
  - Consume messages from SendFile queue
  - Retrieve FTP credentials from secrets vault (Azure Key Vault or encrypted DB)
  - Transfer files via FTP with **streaming** (max file size: 1GB)
  - Support **multi-server schedules**: parallel transfers to multiple FTP servers per schedule
  - Update job status in Redis for each FTP server transfer
  - Handle retries and failures per FTP server independently
  - OpenTelemetry tracing
  - **Rate limiting**: Respect RabbitMQ prefetch count to control concurrent transfers

### 4. GUI (FileMover.GUI) - Planned
- **Purpose**: User interface for system management
- **Technology**: TBD (React/Angular/Blazor)
- **Responsibilities**:
  - User authentication (Azure AD or local login)
  - Manage users (admin only)
  - Manage FTP servers (CRUD, test connections)
  - Manage schedules (CRUD, cron expression builder, assign FTP servers)
  - View job history and status (real-time via SignalR or polling)
  - View logs and statistics (Grafana embed or custom dashboards)
  - System health monitoring
- **Deployment**: Separate SPA; connects to API via HTTPS
- **Authentication Flow**:
  - Azure mode: Redirect to Azure AD, obtain token, call API with bearer token.
  - Local mode: Login form, API issues JWT, GUI stores token in secure storage.

### 5. Domain Library (FileMover.Domain)
- **Purpose**: Core domain models and business logic
- **Contains**:
  - Entities (Schedule, FileTransferJob, FtpServer, User, ScheduleFtpServerMapping)
  - Domain enums and value objects (JobStatus, DeploymentMode, AuthProvider, SecretProvider)
  - Business logic interfaces
  - **Many-to-many relationship**: Schedule ↔ FtpServer (one schedule can send to multiple FTP servers)

### 6. Infrastructure Library (FileMover.Infrastructure)
- **Purpose**: Infrastructure concerns and external integrations
- **Contains**:
  - Database access (MariaDB via EF Core)
  - Redis client for job tracking, rate limiting, distributed locks
  - RabbitMQ messaging with concurrent job control
  - FTP client implementation with streaming support (max 1GB files)
  - Dual secret vault integrations:
    - **Azure Key Vault** (Azure deployment mode)
    - **Encrypted database storage** (Local deployment mode, AES-256)
  - Dual authentication providers:
    - **Azure AD** authentication (Azure mode)
    - **Local user database** with JWT (Local mode)

### 7. Contracts Library (FileMover.Contracts)
- **Purpose**: Shared message contracts
- **Contains**:
  - GenerateFileMessage
  - SendFileMessage
  - Event schemas

## Data Stores

### MariaDB
- **Purpose**: Persistent storage for schedule configuration, FTP servers, users, job history
- **Schema**:
  - **Schedules** table: ID, Name, CronExpression, SourcePath, DestinationPath, IsActive, CreatedBy, CreatedAt, UpdatedAt
  - **FtpServers** table: ID, Name, Host, Port, Username, EncryptedPassword (local mode) or KeyVaultSecretName (Azure mode), Protocol (FTP/FTPS/SFTP)
  - **ScheduleFtpServers** table: ScheduleId, FtpServerId (many-to-many join table)
  - **Users** table: ID, Username, PasswordHash (local mode only), Email, Role (Admin/User), CreatedAt, LastLogin
  - **JobHistory** table: ID, ScheduleId, FtpServerId, JobId (Redis key), Status, StartTime, EndTime, FileSize, ErrorMessage

### Redis
- **Purpose**: Active job tracking, caching, rate limiting, distributed locks
- **Data Structures** (see system prompt section 19 for detailed Redis strategy):
  - `job:{jobId}:status` – current job status (Queued, InProgress, Completed, Failed)
  - `job:{jobId}:metadata` – JSON metadata (schedule ID, FTP servers, file size, timestamps)
  - `schedule:{scheduleId}:lastrun` – last successful execution timestamp
  - `lock:{resource}` – distributed locks for schedule execution
  - `ratelimit:{workerId}:{minute}` – sliding window counters
- **TTL Policies**:
  - Job status: 24 hours after completion
  - Locks: 5-10 minutes with renewal
  - Rate limits: 1-5 minutes
- **Persistence**: Redis is ephemeral; critical state persisted to MariaDB

### RabbitMQ
- **Purpose**: Asynchronous message processing with concurrent job control
- **Queues**:
  - `GenerateFile`: Triggers for file generation (one message per schedule execution)
  - `SendFile`: Triggers for file transfer (multiple messages per schedule if multi-server; one per FTP server)
- **Features**:
  - Message persistence (durable queues)
  - Dead letter queues for failed messages
  - Retry policies with exponential backoff
  - **Prefetch count** configuration to limit concurrent transfers per worker (e.g., prefetch=5 → max 5 parallel FTP uploads per SendFile worker instance)
- **Concurrency Control**:
  - GenerateFile workers: Scale based on schedule volume
  - SendFile workers: Prefetch count limits parallel FTP connections; prevents resource exhaustion

## Deployment Modes

File Mover supports two deployment strategies (see [DEPLOYMENT_STRATEGIES.md](../DEPLOYMENT_STRATEGIES.md) for details):

### 1. Azure Deployment
- **Authentication**: Azure AD (Entra ID) with Service Principal for worker access
- **Secret Management**: Azure Key Vault for FTP credentials
- **Benefits**: Scalable, managed services, centralized identity, audit trails
- **Use Case**: Production cloud deployments, enterprise environments

### 2. Local Deployment
- **Authentication**: Local user database with JWT tokens
- **Secret Management**: Encrypted database storage (AES-256) for FTP credentials
- **Benefits**: No cloud dependency, full control, lower cost for small-scale
- **Use Case**: On-premises, air-gapped environments, development

## Secret Management

### Azure Mode
- **Azure Key Vault**: Cloud-native secret management
- **Access**: Service Principal (Client ID + Secret) or Managed Identity
- **Secrets**: FTP passwords stored as Key Vault secrets; referenced by name in FtpServers table
- **Rotation**: Automatic via Key Vault versioning; workers reload on cache expiry

### Local Mode
- **Encrypted Database Storage**: FTP passwords stored encrypted in FtpServers table
- **Encryption**: AES-256-GCM with key from environment variable `ENCRYPTION_KEY`
- **Access**: Workers decrypt passwords in-memory at runtime; never persist decrypted values
- **Rotation**: Manual re-encryption utility provided for key rotation

**Common Principles**:
- FTP passwords never stored in configuration or logs
- Credentials cached in memory with short TTL (default: 15 minutes)
- Secrets fetched per-job to support rotation without restart

## Observability

### OpenTelemetry
- Distributed tracing across services
- Metrics collection
- Log correlation

### Prometheus
- Metrics storage and querying
- Alerting rules
- Service health monitoring

### Grafana
- Visualization dashboards
- Real-time monitoring
- Alert management

### Optional: Elastic Stack (LGTM)
- Logs: Elasticsearch + Logstash + Kibana
- Metrics: Metricbeat
- Traces: APM Server
- Grafana integration

### Zabbix
- Infrastructure monitoring
- Server health
- Network metrics
- Database performance

## Deployment

### Docker Compose
- Local development
- Quick start
- Integration testing

### Docker Swarm
- Production orchestration
- Service scaling
- Rolling updates
- Secret management

### Kubernetes (Optional)
- Cloud-native deployments
- Advanced orchestration
- Auto-scaling
- Multi-region support

## Flow Diagram

### Single-Server Schedule (Basic Flow)
```
┌─────────────┐
│   API       │
│  (Schedule) │
└──────┬──────┘
       │ Publish GenerateFileMessage
       ▼
┌─────────────────┐
│   RabbitMQ      │
│ GenerateFile Q  │
└────────┬────────┘
         │ Consume
         ▼
┌─────────────────┐
│ GenerateFile    │
│    Worker       │◄─────┐
└────────┬────────┘      │
         │               │ Redis: Set job status
         │ Generate File │ job:{id}:status = "InProgress"
         │               │
         │ Publish ──────┘
         ▼
┌─────────────────┐
│   RabbitMQ      │
│   SendFile Q    │
└────────┬────────┘
         │ Consume
         ▼
┌─────────────────┐
│   SendFile      │◄────┐
│    Worker       │     │ Redis: Update status
└────────┬────────┘     │ Get FTP creds
         │              │ (Key Vault or DB)
         ▼              │
┌─────────────────┐     │
│  Secret Vault   │     │
│  (Azure KV or   │     │
│   Encrypted DB) │     │
└────────┬────────┘     │
         │              │
         ▼              │
┌─────────────────┐     │
│   FTP Server    │     │
│ (File Transfer) │     │
│   Streaming     │     │
│   Max 1GB       │     │
└─────────────────┘     │
                        │
         Redis ─────────┘
      (Job Status)

     MariaDB (Job History)
```

### Multi-Server Schedule Flow
```
┌─────────────┐
│   API       │ Trigger schedule with
│  (Schedule) │ 3 FTP servers assigned
└──────┬──────┘
       │ Publish 1x GenerateFileMessage
       ▼
┌─────────────────┐
│   RabbitMQ      │
│ GenerateFile Q  │
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│ GenerateFile    │
│    Worker       │
└────────┬────────┘
         │ Generate file once
         │ Publish 3x SendFileMessage
         │ (one per FTP server)
         ▼
┌─────────────────┐
│   RabbitMQ      │
│   SendFile Q    │
│  ┌─────────┐    │
│  │ Msg1    │    │
│  │ Msg2    │    │
│  │ Msg3    │    │
│  └─────────┘    │
└────────┬────────┘
         │
    ┌────┼────┐ (Concurrent, limited by prefetch count)
    ▼    ▼    ▼
 Worker Worker Worker
 Send1  Send2  Send3
    │    │    │
    ▼    ▼    ▼
 FTP-A FTP-B FTP-C
    │    │    │
    └────┼────┘
         │ All transfers complete
         ▼
   Redis: Mark job "Completed"
   MariaDB: Record history per FTP server
```

**Key Points**:
- One schedule execution → 1 GenerateFile message → N SendFile messages (N = number of FTP servers).
- SendFile workers process messages concurrently (controlled by RabbitMQ prefetch count).
- Each FTP server transfer tracked independently in Redis (`job:{jobId}:ftp:{ftpServerId}:status`).
- Job marked "Completed" only when all FTP transfers succeed; "Failed" if any transfer fails after retries.

## Security Considerations

1. **Secrets**: Never store credentials in code or configuration
2. **Network**: Use TLS for all external communication
3. **Authentication**: API requires authentication
4. **Authorization**: Role-based access control
5. **Audit**: All operations are logged and traced

## Scalability

- **Horizontal**: Scale workers independently
- **Queue-based**: Natural backpressure handling
- **Stateless**: Workers can be added/removed dynamically
- **Distributed**: Multi-node deployment support
