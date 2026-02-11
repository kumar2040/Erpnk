# 07 - DevOps Strategy

This document describes the deployment and lifecycle management of the **NkplmErp** system.

## 1. Containerization

-   **Docker**: All components (API, Blazor, PostgreSQL, Redis) are containerized.
-   **Multi-Stage Builds**: Used in Dockerfiles to minimize image size and maximize security (no build tools in production images).
-   **Orchestration**: Docker Compose for development; Kubernetes (K8s) for Production.

## 2. CI/CD Pipeline

Using **GitHub Actions** (or Azure DevOps) for automation.

### Continuous Integration (CI)
-   **Trigger**: On pull requests to `develop` or `main`.
-   **Steps**:
    1.  Restore dependencies.
    2.  Build solution.
    3.  Run Unit & Integration Tests.
    4.  Static Code Analysis (SonarQube).
    5.  Vulnerability Scanning (Snyk).

### Continuous Deployment (CD)
-   **Environments**:
    -   **Development**: Automated deployment on every merge to `develop`.
    -   **Staging**: Automated deployment on tag creation (e.g., `v1.2.0-rc`).
    -   **Production**: Manual approval required after Staging sign-off.

## 3. Environment Strategy

We follow the **Twelve-Factor App** methodology:
-   Config is stored in the environment.
-   Development, Staging, and Production are as serupa (similar) as possible.

| Environment | Purpose | Infrastructure |
| :--- | :--- | :--- |
| **Local** | Dev Sandbox | Desktop Docker |
| **Dev** | Integration | Cloud (B-Series) |
| **Staging** | QA / User Review | Cloud (Standard Tier) |
| **Prod** | Business Value | Cloud (Premium Tier, HA) |

## 4. Monitoring & Observability

-   **Logging**: Centralized via **ELK Stack** (Elasticsearch, Logstash, Kibana) or **Seq**.
-   **Metrics**: Prometheus & Grafana for system health monitoring.
-   **Tracing**: OpenTelemetry (Jaeger) for request tracing across the API and DB.

## 5. Security in DevOps

-   **Secret Management**: Using cloud native vaults (Azure Key Vault / AWS KMS).
-   **IaC**: Infrastructure as Code using **Terraform** or **Pulumi** to ensure repeatable environments.
-   **Dependency Check**: GitHub Dependabot enabled to monitor and update vulnerable packages.
