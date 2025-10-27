# File Mover Architecture

## Overview

File Mover is a distributed .NET 9 application designed to move files from local storage to FTP servers. The architecture follows a microservices pattern with event-driven processing.

## Components

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
- **Purpose**: Transfer files to FTP servers
- **Technology**: .NET Worker Service
- **Responsibilities**:
  - Consume messages from SendFile queue
  - Retrieve FTP credentials from secrets vault
  - Transfer files via FTP
  - Update job status in Redis
  - Handle retries and failures
  - OpenTelemetry tracing

### 4. Domain Library (FileMover.Domain)
- **Purpose**: Core domain models and business logic
- **Contains**:
  - Entities (Schedule, FileTransferJob)
  - Domain enums and value objects
  - Business logic interfaces

### 5. Infrastructure Library (FileMover.Infrastructure)
- **Purpose**: Infrastructure concerns and external integrations
- **Contains**:
  - Database access (MariaDB via EF Core)
  - Redis client for job tracking
  - RabbitMQ messaging
  - FTP client implementation
  - Secret vault integrations (Azure Key Vault, KeyCloak, HashiCorp Vault)

### 6. Contracts Library (FileMover.Contracts)
- **Purpose**: Shared message contracts
- **Contains**:
  - GenerateFileMessage
  - SendFileMessage
  - Event schemas

## Data Stores

### MariaDB
- **Purpose**: Persistent storage for schedule configuration
- **Schema**:
  - Schedules table with cron expressions
  - FTP connection details (host, username, secret vault key)
  - Source and destination paths

### Redis
- **Purpose**: Active job tracking and caching
- **Data**:
  - Current job status
  - Job progress tracking
  - Distributed locks
  - Rate limiting

### RabbitMQ
- **Purpose**: Asynchronous message processing
- **Queues**:
  - `GenerateFile`: Triggers for file generation
  - `SendFile`: Triggers for file transfer
- **Features**:
  - Message persistence
  - Dead letter queues
  - Retry policies

## Secret Management

Supports multiple secret vault providers:
- **Azure Key Vault**: Cloud-native secret management
- **KeyCloak**: Open-source identity and access management
- **HashiCorp Vault**: Enterprise-grade secret management

FTP passwords are never stored in configuration; only references to vault keys.

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

```
┌─────────────┐
│   API       │
│  (Schedule) │
└──────┬──────┘
       │ Publish
       ▼
┌─────────────────┐
│   RabbitMQ      │
│ GenerateFile Q  │
└────────┬────────┘
         │ Consume
         ▼
┌─────────────────┐
│ GenerateFile    │
│    Worker       │
└────────┬────────┘
         │ Generate & Publish
         ▼
┌─────────────────┐
│   RabbitMQ      │
│   SendFile Q    │
└────────┬────────┘
         │ Consume
         ▼
┌─────────────────┐
│   SendFile      │
│    Worker       │◄────┐
└────────┬────────┘     │
         │              │
         ▼              │
┌─────────────────┐     │
│  Secret Vault   │     │
│  (Get FTP Pwd)  │     │
└────────┬────────┘     │
         │              │
         ▼              │
┌─────────────────┐     │
│   FTP Server    │     │
│ (File Transfer) │     │
└─────────────────┘     │
                        │
         Redis ─────────┘
      (Job Status)
```

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
