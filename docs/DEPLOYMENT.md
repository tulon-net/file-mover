# Deployment Guide

This guide covers deployment strategies for the File Mover application across different environments.

## Table of Contents

1. [Local Development](#local-development)
2. [Docker Compose](#docker-compose)
3. [Docker Swarm](#docker-swarm)
4. [Kubernetes](#kubernetes)
5. [Cloud Providers](#cloud-providers)
6. [Monitoring Setup](#monitoring-setup)

## Local Development

### Prerequisites
- .NET 9 SDK
- Docker and Docker Compose
- Git

### Steps

1. **Start Infrastructure Services**
   ```bash
   docker-compose up -d mariadb redis rabbitmq
   ```

2. **Run the API**
   ```bash
   cd src/FileMover.Api
   dotnet run
   ```

3. **Run Workers**
   ```bash
   # Terminal 1
   cd src/FileMover.Worker.GenerateFile
   dotnet run
   
   # Terminal 2
   cd src/FileMover.Worker.SendFile
   dotnet run
   ```

## Docker Compose

Best for: Development, Testing, Single-server deployments

### Quick Start

```bash
# Create .env file
cp .env.example .env

# Edit .env with your configuration
nano .env

# Start all services
docker-compose up -d

# View logs
docker-compose logs -f

# Stop services
docker-compose down
```

### Scaling Workers

```bash
# Scale GenerateFile workers to 5 instances
docker-compose up -d --scale worker-generate=5

# Scale SendFile workers to 3 instances
docker-compose up -d --scale worker-send=3
```

### Updating Services

```bash
# Pull latest images
docker-compose pull

# Restart with new images
docker-compose up -d
```

## Docker Swarm

Best for: Production, Multi-node deployments, High availability

### Initial Setup

1. **Initialize Swarm**
   ```bash
   docker swarm init --advertise-addr <MANAGER-IP>
   ```

2. **Add Worker Nodes**
   ```bash
   # On manager node, get join token
   docker swarm join-token worker
   
   # On worker nodes, run the join command
   docker swarm join --token <TOKEN> <MANAGER-IP>:2377
   ```

3. **Create Secrets**
   ```bash
   # Database passwords
   echo "your_root_password" | docker secret create mariadb_root_password -
   echo "your_db_password" | docker secret create mariadb_password -
   
   # RabbitMQ password
   echo "your_rabbitmq_password" | docker secret create rabbitmq_password -
   
   # Grafana password
   echo "your_grafana_password" | docker secret create grafana_password -
   ```

4. **Create Configs**
   ```bash
   # OpenTelemetry config
   docker config create otel-config infrastructure/observability/otel-collector-config.yaml
   
   # Prometheus config
   docker config create prometheus-config infrastructure/observability/prometheus.yml
   ```

5. **Set Environment Variables**
   ```bash
   export DOCKER_REGISTRY=your-registry.azurecr.io
   export VERSION=1.0.0
   export MARIADB_PASSWORD=your_db_password
   export RABBITMQ_PASSWORD=your_rabbitmq_password
   ```

6. **Deploy Stack**
   ```bash
   docker stack deploy -c infrastructure/docker/docker-stack.yml filemover
   ```

### Management Commands

```bash
# List services
docker service ls

# View service logs
docker service logs filemover_api -f

# Scale services
docker service scale filemover_worker-generate=5
docker service scale filemover_worker-send=5

# Update service image
docker service update --image your-registry/filemover-api:1.1.0 filemover_api

# Remove stack
docker stack rm filemover
```

### Rolling Updates

```bash
# Update with zero downtime
docker service update \
  --image your-registry/filemover-api:1.1.0 \
  --update-parallelism 1 \
  --update-delay 10s \
  filemover_api
```

## Kubernetes

Best for: Cloud-native deployments, Advanced orchestration, Multi-region

### Prerequisites
- Kubernetes cluster (AKS, EKS, GKE, or on-premises)
- kubectl configured
- Docker images pushed to registry

### Deployment Steps

1. **Create Namespace**
   ```bash
   kubectl apply -f infrastructure/k8s/configmap.yaml
   ```

2. **Create Secrets**
   ```bash
   # Database connection
   kubectl create secret generic filemover-secrets \
     --from-literal=mariadb-connection="Server=mariadb;Port=3306;Database=filemover;User=filemover;Password=your_password" \
     --from-literal=rabbitmq-password="your_rabbitmq_password" \
     -n filemover
   
   # Azure Key Vault credentials (if using)
   kubectl create secret generic azure-keyvault-secrets \
     --from-literal=client-id="your_client_id" \
     --from-literal=client-secret="your_client_secret" \
     --from-literal=tenant-id="your_tenant_id" \
     -n filemover
   ```

3. **Deploy Infrastructure Services**
   ```bash
   # MariaDB
   kubectl apply -f infrastructure/k8s/mariadb-deployment.yaml
   
   # Redis
   kubectl apply -f infrastructure/k8s/redis-deployment.yaml
   
   # RabbitMQ
   kubectl apply -f infrastructure/k8s/rabbitmq-deployment.yaml
   ```

4. **Deploy Application Services**
   ```bash
   # API
   kubectl apply -f infrastructure/k8s/api-deployment.yaml
   
   # Workers
   kubectl apply -f infrastructure/k8s/worker-deployments.yaml
   ```

5. **Verify Deployment**
   ```bash
   # Check pods
   kubectl get pods -n filemover
   
   # Check services
   kubectl get services -n filemover
   
   # View logs
   kubectl logs -f deployment/filemover-api -n filemover
   ```

### Scaling

```bash
# Scale API
kubectl scale deployment filemover-api --replicas=3 -n filemover

# Scale workers
kubectl scale deployment filemover-worker-generate --replicas=5 -n filemover
kubectl scale deployment filemover-worker-send --replicas=5 -n filemover
```

### Auto-scaling

```bash
# Create HPA for API
kubectl autoscale deployment filemover-api \
  --cpu-percent=70 \
  --min=2 \
  --max=10 \
  -n filemover

# Create HPA for workers
kubectl autoscale deployment filemover-worker-generate \
  --cpu-percent=80 \
  --min=3 \
  --max=20 \
  -n filemover
```

## Cloud Providers

### Azure (AKS)

1. **Create AKS Cluster**
   ```bash
   az aks create \
     --resource-group filemover-rg \
     --name filemover-aks \
     --node-count 3 \
     --enable-addons monitoring \
     --generate-ssh-keys
   ```

2. **Get Credentials**
   ```bash
   az aks get-credentials --resource-group filemover-rg --name filemover-aks
   ```

3. **Setup Azure Key Vault Integration**
   ```bash
   # Enable managed identity
   az aks update --resource-group filemover-rg --name filemover-aks --enable-managed-identity
   
   # Install CSI driver
   az aks enable-addons --addons azure-keyvault-secrets-provider --resource-group filemover-rg --name filemover-aks
   ```

### AWS (EKS)

1. **Create EKS Cluster**
   ```bash
   eksctl create cluster \
     --name filemover-eks \
     --version 1.28 \
     --region us-west-2 \
     --nodegroup-name standard-workers \
     --node-type t3.medium \
     --nodes 3 \
     --nodes-min 1 \
     --nodes-max 10 \
     --managed
   ```

2. **Configure kubectl**
   ```bash
   aws eks update-kubeconfig --region us-west-2 --name filemover-eks
   ```

### Google Cloud (GKE)

1. **Create GKE Cluster**
   ```bash
   gcloud container clusters create filemover-gke \
     --num-nodes=3 \
     --region=us-central1 \
     --machine-type=e2-medium \
     --enable-autoscaling \
     --min-nodes=1 \
     --max-nodes=10
   ```

2. **Get Credentials**
   ```bash
   gcloud container clusters get-credentials filemover-gke --region=us-central1
   ```

## Monitoring Setup

### Grafana Dashboards

1. **Access Grafana**
   - Docker Compose: http://localhost:3000
   - Kubernetes: `kubectl port-forward svc/grafana 3000:3000 -n filemover`

2. **Import Dashboards**
   - Login with admin credentials
   - Navigate to Dashboards > Import
   - Import JSON files from `infrastructure/observability/grafana/dashboards/`

### Prometheus Alerts

Edit `infrastructure/observability/prometheus.yml` to add alerting rules:

```yaml
rule_files:
  - "alerts/*.yml"

alerting:
  alertmanagers:
    - static_configs:
        - targets:
            - alertmanager:9093
```

### Elastic Stack (Optional)

1. **Deploy Elasticsearch**
   ```bash
   kubectl apply -f infrastructure/k8s/elasticsearch-deployment.yaml
   ```

2. **Deploy Kibana**
   ```bash
   kubectl apply -f infrastructure/k8s/kibana-deployment.yaml
   ```

3. **Configure APM**
   Update `.env` or ConfigMap:
   ```
   ELASTIC_APM_ENDPOINT=https://your-apm.elastic.co
   ELASTIC_APM_TOKEN=your_token
   ```

## Backup and Disaster Recovery

### Database Backup

```bash
# Manual backup
docker exec filemover-mariadb mysqldump -u root -p filemover > backup.sql

# Automated backup (cron)
0 2 * * * docker exec filemover-mariadb mysqldump -u root -p filemover | gzip > /backups/filemover-$(date +\%Y\%m\%d).sql.gz
```

### Restore Database

```bash
# Restore from backup
docker exec -i filemover-mariadb mysql -u root -p filemover < backup.sql
```

## Troubleshooting

### Common Issues

1. **Services not starting**
   ```bash
   # Check logs
   docker-compose logs [service-name]
   kubectl logs -f pod/[pod-name] -n filemover
   ```

2. **Database connection errors**
   ```bash
   # Verify database is running
   docker-compose exec mariadb mysql -u filemover -p
   
   # Check connection string in configuration
   kubectl get configmap filemover-config -n filemover -o yaml
   ```

3. **Workers not processing messages**
   ```bash
   # Check RabbitMQ queues
   docker-compose exec rabbitmq rabbitmqctl list_queues
   
   # Verify worker logs
   kubectl logs -f deployment/filemover-worker-generate -n filemover
   ```

## Performance Tuning

### Worker Scaling Strategy

- **GenerateFile Workers**: Scale based on file generation workload
- **SendFile Workers**: Scale based on FTP transfer volume
- Monitor RabbitMQ queue depth for scaling decisions

### Resource Limits

Adjust based on workload:

```yaml
resources:
  requests:
    memory: "256Mi"
    cpu: "500m"
  limits:
    memory: "512Mi"
    cpu: "1000m"
```

## Security Best Practices

1. **Never commit secrets** to version control
2. **Use secret management** (Key Vault, Vault, Secrets Manager)
3. **Enable TLS** for all external communication
4. **Implement RBAC** for API access
5. **Regular security updates** for base images
6. **Scan images** for vulnerabilities (Trivy, Snyk)
7. **Network policies** in Kubernetes
8. **Audit logging** enabled

## Next Steps

- Configure CI/CD pipelines
- Set up monitoring alerts
- Implement backup strategies
- Plan disaster recovery
- Performance testing
- Security hardening
