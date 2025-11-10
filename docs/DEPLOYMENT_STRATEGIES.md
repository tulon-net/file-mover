# Deployment Strategies

This document describes the two deployment modes supported by File Mover: Azure deployment with enterprise integration and local deployment for on-premises or development scenarios.

## Overview

File Mover supports dual deployment architectures to accommodate different operational environments:

1. **Azure Deployment**: Cloud-native with Azure AD authentication and Azure Key Vault for secrets.
2. **Local Deployment**: Self-contained with local user management and encrypted database storage for secrets.

## Azure Deployment Mode

### Authentication & Authorization
- **Azure AD (Entra ID)** authentication for API and GUI.
- API validates bearer tokens issued by Azure AD.
- **Service Principal** used for programmatic access (worker authentication to Key Vault).
- Role-based access control via Azure AD groups or app roles:
  - `FileMover.Admin`: Full access to schedules, FTP servers, users, system configuration.
  - `FileMover.User`: Read-only access to schedules and job status; limited write permissions.

### Secret Management
- **Azure Key Vault** stores FTP server credentials (username/password pairs).
- API and workers authenticate to Key Vault using **Service Principal** (Client ID + Client Secret or Managed Identity).
- Secrets cached in memory with configurable TTL (default: 15 minutes).
- Secret rotation supported via Key Vault versioning; application reloads on expiry.

### Configuration
```json
{
  "Authentication": {
    "Provider": "AzureAD",
    "AzureAd": {
      "Instance": "https://login.microsoftonline.com/",
      "TenantId": "<tenant-id>",
      "ClientId": "<api-app-registration-client-id>",
      "Audience": "api://filemover"
    }
  },
  "Secrets": {
    "Provider": "AzureKeyVault",
    "AzureKeyVault": {
      "VaultUri": "https://<vault-name>.vault.azure.net/",
      "ClientId": "<service-principal-client-id>",
      "ClientSecret": "<service-principal-secret>",
      "TenantId": "<tenant-id>"
    }
  }
}
```

### Deployment Architecture
```
┌──────────────┐
│  Azure AD    │
│ (Auth)       │
└──────┬───────┘
       │ Token Validation
       ▼
┌──────────────┐      ┌────────────────┐
│   GUI        │─────►│   API          │
│ (SPA)        │ HTTPS │ (Azure App Svc)│
└──────────────┘      └────────┬───────┘
                               │
                ┌──────────────┼──────────────┐
                ▼              ▼              ▼
          ┌─────────┐    ┌─────────┐   ┌──────────┐
          │ MariaDB │    │ Redis   │   │ RabbitMQ │
          │(Azure DB)│   │(Azure)  │   │(Azure)   │
          └─────────┘    └─────────┘   └────┬─────┘
                                             │
                     ┌───────────────────────┼───────┐
                     ▼                       ▼       ▼
               ┌──────────┐          ┌──────────┐
               │ Worker   │          │ Worker   │
               │ Generate │          │  Send    │
               └────┬─────┘          └────┬─────┘
                    │                     │
                    └─────────┬───────────┘
                              ▼
                      ┌───────────────┐
                      │ Azure Key     │
                      │ Vault         │
                      │ (FTP Secrets) │
                      └───────────────┘
```

### Benefits
- Centralized identity management via Azure AD.
- Secure secret storage with audit trails and rotation.
- Scalable infrastructure with Azure PaaS services.
- Integration with Azure Monitor, Application Insights.

---

## Local Deployment Mode

### Authentication & Authorization
- Local user database (MariaDB table: `Users`).
- Passwords hashed with **bcrypt** or **PBKDF2** (work factor: 12+).
- API issues **JWT tokens** upon successful login.
- Role-based authorization via `Role` column in `Users` table:
  - `Admin`: Full system access.
  - `User`: Limited read/write permissions.

### Secret Management
- FTP server credentials stored **encrypted** in local database (`FtpServers` table, column: `EncryptedPassword`).
- Encryption: **AES-256** in GCM mode.
- Encryption key:
  - Stored in environment variable `ENCRYPTION_KEY` or configuration (never in repo).
  - Rotated manually; re-encryption utility provided for key rotation.
- Decryption occurs only in-memory at runtime when worker retrieves FTP credentials.

### Configuration
```json
{
  "Authentication": {
    "Provider": "Local",
    "Jwt": {
      "SecretKey": "<jwt-signing-key>",
      "Issuer": "FileMover",
      "Audience": "FileMover",
      "ExpirationMinutes": 60
    }
  },
  "Secrets": {
    "Provider": "Local",
    "Encryption": {
      "Algorithm": "AES-256-GCM",
      "KeyEnvironmentVariable": "ENCRYPTION_KEY"
    }
  }
}
```

### Deployment Architecture
```
┌──────────────┐
│  GUI         │
│ (Local SPA)  │
└──────┬───────┘
       │ JWT Token
       ▼
┌──────────────┐      ┌────────────────┐
│   API        │      │   MariaDB      │
│ (Local Host) │◄────►│ (Users, FTP    │
└──────┬───────┘      │  Schedules)    │
       │              └────────────────┘
       ▼
┌──────────────┐      ┌────────────────┐
│  Redis       │      │  RabbitMQ      │
│ (Local)      │      │  (Local)       │
└──────────────┘      └────────┬───────┘
                               │
                     ┌─────────┼─────────┐
                     ▼                   ▼
               ┌──────────┐        ┌──────────┐
               │ Worker   │        │ Worker   │
               │ Generate │        │  Send    │
               └────┬─────┘        └────┬─────┘
                    │                   │
                    └────────┬──────────┘
                             ▼
                     (Decrypt FTP pwd
                      from MariaDB)
```

### Benefits
- No external dependencies (Azure, cloud services).
- Full control over infrastructure and data.
- Suitable for air-gapped environments or compliance requirements.
- Lower operational cost for small-scale deployments.

---

## Feature Comparison

| Feature                     | Azure Deployment                  | Local Deployment                  |
|-----------------------------|-----------------------------------|-----------------------------------|
| Authentication              | Azure AD (Entra ID)               | Local user DB + JWT               |
| User Management             | Azure Portal / Graph API          | API endpoints + GUI               |
| FTP Secret Storage          | Azure Key Vault                   | Encrypted DB column (AES-256)     |
| Secret Rotation             | Automatic via Key Vault           | Manual re-encryption utility      |
| Identity Provider           | Service Principal / Managed ID    | N/A                               |
| Audit Logging               | Azure Monitor / Key Vault logs    | Local logs (OpenTelemetry)        |
| Scalability                 | Azure App Service / AKS           | Docker Swarm / Manual scaling     |
| Cost                        | Pay-per-use (PaaS services)       | Self-hosted (hardware + licenses) |

---

## Switching Between Modes

### Configuration Toggle
Set `Authentication:Provider` and `Secrets:Provider` in `appsettings.json` or environment variables:
- Azure: `"Provider": "AzureAD"` and `"Provider": "AzureKeyVault"`
- Local: `"Provider": "Local"` and `"Provider": "Local"`

### Migration Path
**Azure → Local**:
1. Export FTP credentials from Azure Key Vault.
2. Import into local DB with encryption enabled.
3. Create local user accounts (migrate Azure AD users manually or via script).
4. Update configuration and redeploy.

**Local → Azure**:
1. Create Azure Key Vault and store FTP credentials as secrets.
2. Register API app in Azure AD; configure roles.
3. Create Service Principal for worker Key Vault access.
4. Update configuration and redeploy to Azure App Service or AKS.

---

## Security Considerations

### Azure Mode
- Use **Managed Identity** instead of Service Principal secrets when possible (Azure App Service, AKS).
- Enable **Key Vault firewall** to restrict access to specific VNets.
- Rotate Service Principal secrets every 90 days.
- Enable **Conditional Access** policies for user authentication.

### Local Mode
- Store `ENCRYPTION_KEY` in a Hardware Security Module (HSM) or secure vault if available.
- Rotate encryption key annually; use re-encryption utility.
- Enforce strong password policies for local users (min 12 chars, complexity).
- Enable **HTTPS only** for API and GUI; use Let's Encrypt or internal CA.
- Implement **rate limiting** on login endpoints to prevent brute force.

---

## Testing Both Modes

### Integration Tests
- Separate test configurations for Azure and Local modes.
- Use **test doubles** (mocked Key Vault, in-memory user store) for CI/CD.
- Validate authentication flows, secret retrieval, encryption/decryption.

### Local Development
- Default to Local mode for development (no Azure subscription required).
- Use `appsettings.Development.json` to override providers.
- Docker Compose includes local MariaDB, Redis, RabbitMQ for full stack testing.

---

## Recommended Deployment Strategy

| Environment       | Recommended Mode | Rationale                                       |
|-------------------|------------------|-------------------------------------------------|
| Production (Cloud)| Azure            | Scalability, managed services, enterprise auth  |
| Production (On-Prem)| Local          | No cloud dependency, full control               |
| Staging           | Azure or Local   | Match production mode                           |
| Development       | Local            | Faster setup, no Azure costs                    |
| CI/CD Pipeline    | Local (mocked)   | Faster tests, no external dependencies          |

---

## Next Steps
- Implement `IAuthenticationProvider` interface with Azure AD and Local providers.
- Implement `ISecretProvider` interface with Key Vault and Local encrypted DB providers.
- Add GUI login flows for both modes (Azure AD redirect vs local form).
- Document encryption key rotation procedure for Local mode.
- Create migration scripts for Azure ↔ Local transitions.

