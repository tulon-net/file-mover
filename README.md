# File Mover

A distributed .NET 9 application for moving files from local storage to FTP servers with enterprise-grade reliability and observability.

## Features

- 📁 **Scheduled File Transfers**: Configure schedules with cron expressions
- 🔄 **Event-Driven Architecture**: RabbitMQ-based asynchronous processing
- 🔐 **Secure Secret Management**: Support for Azure Key Vault, KeyCloak, and HashiCorp Vault
- 📊 **Full Observability**: OpenTelemetry, Prometheus, and Grafana integration
- 🐳 **Cloud-Ready**: Docker and Kubernetes deployment support
- ⚡ **Scalable Workers**: Independent scaling of file generation and transfer workers
- 🏥 **Health Monitoring**: Built-in health checks and monitoring

## Architecture

The application follows a microservices architecture with three main components:

1. **API Service**: REST API for schedule management
2. **GenerateFile Worker**: Processes file generation requests
3. **SendFile Worker**: Handles FTP file transfers

### Technology Stack

- **Framework**: .NET 9 with C#
- **Database**: MariaDB (schedule configuration)
- **Cache**: Redis (active job tracking)
- **Message Queue**: RabbitMQ (GenerateFile and SendFile queues)
- **Secrets**: Azure Key Vault / KeyCloak / HashiCorp Vault
- **Observability**: OpenTelemetry, Prometheus, Grafana, Elastic Stack, Zabbix
- **Orchestration**: Docker Swarm / Kubernetes

## Quick Start

### Prerequisites

- Docker 24.0+ and Docker Compose 2.0+
- .NET 9 SDK (for local development)

### Run with Docker Compose

```bash
# Clone the repository
git clone https://github.com/tulon-net/file-mover.git
cd file-mover

# Create .env file with your configuration
cp .env.example .env

# Start all services
docker-compose up -d

# Check service status
docker-compose ps
```

### Access Services

- **API**: http://localhost:8080
- **Grafana**: http://localhost:3000 (admin/admin)
- **Prometheus**: http://localhost:9090
- **RabbitMQ Management**: http://localhost:15672 (admin/adminpassword)

## Project Structure

```
file-mover/
├── src/
│   ├── FileMover.Api/              # REST API for schedule management
│   ├── FileMover.Worker.GenerateFile/  # File generation worker
│   ├── FileMover.Worker.SendFile/      # FTP transfer worker
│   ├── FileMover.Domain/           # Domain entities and business logic
│   ├── FileMover.Infrastructure/   # Data access and external integrations
│   └── FileMover.Contracts/        # Shared message contracts
├── tests/
│   ├── FileMover.Api.Tests/
│   ├── FileMover.Worker.Tests/
│   └── FileMover.Domain.Tests/
├── infrastructure/
│   ├── docker/                     # Docker Swarm stack files
│   ├── k8s/                        # Kubernetes manifests
│   └── observability/              # OpenTelemetry, Prometheus, Grafana configs
├── docs/
│   ├── architecture/               # Architecture documentation
│   └── setup/                      # Setup and deployment guides
└── docker-compose.yml
```

## Development

### Build the Solution

```bash
dotnet restore
dotnet build
```

### Run Tests

```bash
dotnet test
```

### Run Locally

```bash
# Start infrastructure services
docker-compose up -d mariadb redis rabbitmq

# Run the API
cd src/FileMover.Api
dotnet run

# Run workers (in separate terminals)
cd src/FileMover.Worker.GenerateFile
dotnet run

cd src/FileMover.Worker.SendFile
dotnet run
```

## Deployment

### Docker Swarm

```bash
# Initialize swarm
docker swarm init

# Create secrets
echo "password" | docker secret create mariadb_password -

# Deploy stack
docker stack deploy -c infrastructure/docker/docker-stack.yml filemover
```

### Kubernetes

```bash
# Create namespace and configmap
kubectl apply -f infrastructure/k8s/configmap.yaml

# Create secrets
kubectl create secret generic filemover-secrets \
  --from-literal=mariadb-connection="..." \
  -n filemover

# Deploy services
kubectl apply -f infrastructure/k8s/
```

## Documentation

- [Architecture Overview](docs/architecture/README.md)
- [Setup Guide](docs/setup/README.md)

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.
