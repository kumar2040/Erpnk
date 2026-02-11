# 05 - API Standards

The NkplmErp API provides a standardized, secure, and versioned interface for the Blazor frontend and external integrations.

## 1. RESTful Principles

We adhere strictly to REST design patterns:
-   **GET**: Retrieve resources.
-   **POST**: Create new resources.
-   **PUT**: Replace existing resources.
-   **PATCH**: Partially update resources (JSON Patch or specific DTOs).
-   **DELETE**: Soft or hard delete resources.

## 2. API Versioning

Versioning is mandatory to ensure backward compatibility as the ERP evolves.
-   **Strategy**: URL-based versioning (e.g., `/api/v1/invoices`).
-   **Default**: Current stable version is `v1`.

## 3. Request/Response Standards

### Success Responses (2xx)
-   `200 OK`: Successful retrieval.
-   `201 Created`: Successful creation (includes `Location` header).
-   `204 No Content`: Successful update/delete with no body returned.

### Error Responses (4xx/5xx)
Used with **Problem Details (RFC 7807)** format:
```json
{
  "type": "https://nkplm.erp/errors/validation",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "Amount": ["Must be greater than zero."]
  }
}
```

## 4. Global Error Handling

A centralized middleware catches all unhandled exceptions:
-   **Logging**: Full stack trace logged to the internal logging system (Serilog).
-   **User Message**: Only a sanitized correlation ID and generic message returned to the client to avoid information leakage.

## 5. Swagger / OpenAPI

-   **Auto-generation**: Swagger UI enabled in Development and Staging.
-   **Documentation**: XML comments on controllers and DTOs are used to generate descriptions.
-   **Authorization**: Swagger is configured to support JWT Bearer token authentication.

## 6. Rate Limiting

-   Standard Tier: 100 requests / minute / IP.
-   Premium/System Tier: Configurable per tenant.
-   Headers: `X-Rate-Limit-Limit`, `X-Rate-Limit-Remaining`.
