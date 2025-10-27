# File Mover - Setup Guide

## Prerequisites

- Docker 24.0+ and Docker Compose 2.0+
- .NET 9 SDK (for local development)
- Git

## Quick Start with Docker Compose

### 1. Clone the Repository

```bash
git clone https://github.com/tulon-net/file-mover.git
cd file-mover
```

### 2. Configure Environment

Create a `.env` file in the root directory:

```env
# Database
MARIADB_ROOT_PASSWORD=your_root_password
MARIADB_DATABASE=filemover
MARIADB_USER=filemover
MARIADB_PASSWORD=your_db_password

# RabbitMQ
RABBITMQ_USER=admin
RABBITMQ_PASSWORD=your_rabbitmq_password

# Secrets Provider (AzureKeyVault, KeyCloak, or HashiCorp)
SECRETS_PROVIDER=AzureKeyVault

# Azure Key Vault (if using AzureKeyVault)
AZURE_KEY_VAULT_URI=https://your-vault.vault.azure.net/
AZURE_CLIENT_ID=your_client_id
AZURE_CLIENT_SECRET=your_client_secret
AZURE_TENANT_ID=your_tenant_id

# Grafana
GRAFANA_PASSWORD=admin

# OpenTelemetry (optional - for Elastic)
OTEL_ENDPOINT=http://otel-collector:4317
ELASTIC_APM_ENDPOINT=https://your-elastic-apm.com
ELASTIC_APM_TOKEN=your_token

# Environment
ENVIRONMENT=Development
```

### 3. Start Services

```bash
# Start all services
docker-compose up -d

# Check service status
docker-compose ps

# View logs
docker-compose logs -f
```

### 4. Access Services

- **API**: http://localhost:8080
- **Grafana**: http://localhost:3000 (admin / [GRAFANA_PASSWORD])
- **Prometheus**: http://localhost:9090
- **RabbitMQ Management**: http://localhost:15672 (admin / [RABBITMQ_PASSWORD])

### 5. Initialize Database

The database will be created automatically. To apply migrations:

```bash
# Once EF Core migrations are set up
docker-compose exec api dotnet ef database update
```

## Local Development Setup

### 1. Install .NET 9 SDK

```bash
# Verify installation
dotnet --version
# Should output: 9.0.x
```

### 2. Restore Dependencies

```bash
dotnet restore
```

### 3. Build Solution

```bash
dotnet build
```

### 4. Run Tests

```bash
dotnet test
```

### 5. Run Services Locally

Start infrastructure services first:

```bash
# Start only infrastructure
docker-compose up -d mariadb redis rabbitmq
```

Run the API:

```bash
cd src/FileMover.Api
dotnet run
```

Run workers in separate terminals:

```bash
# Terminal 1
cd src/FileMover.Worker.GenerateFile
dotnet run

# Terminal 2
cd src/FileMover.Worker.SendFile
dotnet run
```

## Docker Swarm Deployment

### 1. Initialize Swarm

```bash
docker swarm init
```

### 2. Create Secrets

```bash
echo "your_root_password" | docker secret create mariadb_root_password -
echo "your_db_password" | docker secret create mariadb_password -
echo "your_rabbitmq_password" | docker secret create rabbitmq_password -
echo "your_grafana_password" | docker secret create grafana_password -
```

### 3. Create Configs

```bash
docker config create otel-config infrastructure/observability/otel-collector-config.yaml
docker config create prometheus-config infrastructure/observability/prometheus.yml
```

### 4. Set Environment Variables

```bash
export DOCKER_REGISTRY=your-registry.azurecr.io
export VERSION=1.0.0
export MARIADB_PASSWORD=your_db_password
export RABBITMQ_PASSWORD=your_rabbitmq_password
```

### 5. Deploy Stack

```bash
docker stack deploy -c infrastructure/docker/docker-stack.yml filemover
```

### 6. Monitor Deployment

```bash
# Check services
docker service ls

# Check service logs
docker service logs filemover_api

# Scale workers
docker service scale filemover_worker-generate=5
docker service scale filemover_worker-send=5
```

## Kubernetes Deployment

### 1. Create Namespace

```bash
kubectl apply -f infrastructure/k8s/configmap.yaml
```

### 2. Create Secrets

```bash
kubectl create secret generic filemover-secrets \
  --from-literal=mariadb-connection="Server=mariadb;Port=3306;Database=filemover;User=filemover;Password=your_password" \
  --from-literal=rabbitmq-password="your_rabbitmq_password" \
  -n filemover
```

### 3. Deploy Services

```bash
kubectl apply -f infrastructure/k8s/api-deployment.yaml
kubectl apply -f infrastructure/k8s/worker-deployments.yaml
```

### 4. Check Status

```bash
kubectl get pods -n filemover
kubectl get services -n filemover
```

## Secret Vault Configuration

### Azure Key Vault

1. Create a Key Vault in Azure Portal
2. Create a service principal:
   ```bash
   az ad sp create-for-rbac -n "FileMover"
   ```
3. Grant access to Key Vault:
   ```bash
   az keyvault set-policy --name your-vault \
     --spn YOUR_CLIENT_ID \
     --secret-permissions get list
   ```
4. Store FTP passwords in Key Vault:
   ```bash
   az keyvault secret set --vault-name your-vault \
     --name ftp-server1-password \
     --value "your_ftp_password"
   ```

### HashiCorp Vault

1. Start Vault server
2. Initialize and unseal
3. Enable KV secrets engine:
   ```bash
   vault secrets enable -path=secret kv-v2
   ```
4. Store secrets:
   ```bash
   vault kv put secret/ftp/server1 password="your_ftp_password"
   ```

### KeyCloak

1. Configure KeyCloak realm
2. Create client credentials
3. Store secrets in KeyCloak vault

## Monitoring Setup

### Grafana Dashboards

1. Access Grafana: http://localhost:3000
2. Login with admin credentials
3. Prometheus datasource is pre-configured
4. Import dashboards from `infrastructure/observability/grafana/dashboards/`

### Prometheus Alerts

Edit `infrastructure/observability/prometheus.yml` to add alerting rules.

### Zabbix Integration

1. Install Zabbix agent on hosts
2. Configure monitoring templates
3. Set up triggers for:
   - CPU usage
   - Memory usage
   - Disk space
   - Network traffic
   - Service availability

## Troubleshooting

### Services Not Starting

```bash
# Check logs
docker-compose logs [service-name]

# Verify network
docker network inspect filemover_filemover-network

# Check resource usage
docker stats
```

### Database Connection Issues

```bash
# Test MariaDB connection
docker-compose exec mariadb mysql -u filemover -p

# Check MariaDB logs
docker-compose logs mariadb
```

### RabbitMQ Issues

```bash
# Check queue status
docker-compose exec rabbitmq rabbitmqctl list_queues

# Check connections
docker-compose exec rabbitmq rabbitmqctl list_connections
```

### Worker Not Processing Messages

```bash
# Check worker logs
docker-compose logs worker-generate
docker-compose logs worker-send

# Verify RabbitMQ queues
# Access RabbitMQ Management UI: http://localhost:15672
```

## Maintenance

### Backup Database

```bash
docker-compose exec mariadb mysqldump -u root -p filemover > backup.sql
```

### Update Services

```bash
# Pull latest images
docker-compose pull

# Restart services
docker-compose up -d
```

### Clean Up

```bash
# Stop all services
docker-compose down

# Remove volumes (WARNING: deletes data)
docker-compose down -v
```

## Next Steps

- Review [Architecture Documentation](../architecture/README.md)
- Configure schedules via API
- Set up monitoring alerts
- Configure backup strategies
- Implement CI/CD pipelines
