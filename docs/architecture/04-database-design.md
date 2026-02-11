# 04 - Database Design

NkplmErp uses **PostgreSQL** as its primary relational engine, leveraging its robust transaction support and extensibility.

## 1. ORM & Data Access

-   **Entity Framework Core (EF Core)** is used as the primary ORM.
-   **Code-First Approach**: Migrations are used to manage schema versioning.
-   **Split Queries**: Used for complex includes to prevent the "Cartesian Product" performance issue.
-   **Global Query Filters**: Automatically applied for multi-tenancy and soft-delete logic.

## 2. Multi-Tenancy Strategy

To support multiple organizations securely, we use a **Shared Database, Isolated Schema** (or Isolated Tables) approach.

-   **TenantID**: Every shared table contains an indexed `TenantId`.
-   **Row-Level Security (RLS)**: Can be implemented at the PostgreSQL level for an extra layer of "banking-grade" isolation.
-   **Tenant Resolver**: Middleware identifies the current tenant from the request (header or subdomain).

## 3. Auditing & History

Every entity inherits from a base class (e.g., `BaseEntity`) containing:
-   `CreatedBy` / `CreatedAt`
-   `LastModifiedBy` / `LastModifiedAt`
-   `IsDeleted` (Soft Delete)
-   `RowVersion` (Optimistic Concurrency)

### Audit Logs Table
We maintain a centralized `AuditLogs` table that records the JSON representation of changes for every transaction.

## 4. Naming Conventions

-   **Tables**: PascalCase (e.g., `Invoices`, `CustomerAccounts`).
-   **Columns**: PascalCase (consistent with C# POCOs).
-   **Indexes**: Prefixed with `IX_TableName_ColumnName`.
-   **Foreign Keys**: Prefixed with `FK_SourceTable_TargetTable`.

## 5. Performance Optimization

-   **Indexing**: Proper use of B-Tree, GIN (for JSONB), and Partial Indexes.
-   **Read/Write Splitting**: Future-proofing for Replicas/CQRS.
-   **No-Tracking Queries**: Used by default for read-only operations to save memory.

## 6. Schema Organization (Modular)

While in a single database, we use PostgreSQL Schemas to separate modules:
-   `identity.*`: User accounts, roles, tokens.
-   `finance.*`: General ledger, accounts payable/receivable.
-   `inventory.*`: Products, stock levels, warehouses.
-   `common.*`: Currencies, countries, settings.
