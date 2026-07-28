# Corporate Treasury Platform

Corporate Treasury Platform is a multi-organization treasury
management API built with ASP.NET Core, Entity Framework Core,
and PostgreSQL. It covers organization onboarding, identity and
access management, cash operations, approval workflows,
reconciliation, forecasting, foreign exchange, investments,
credit facilities, alerts, audit evidence, and reporting.

## Documentation

Start with the [documentation index](docs/README.md).

- [Architecture and security](docs/architecture-and-security.md)
- [Roles and access](docs/roles-and-access.md)
- [Organization onboarding and authentication](docs/onboarding-and-authentication.md)
- [Organization setup and cutover](docs/organization-setup-and-cutover.md)
- [Frontend application flow](docs/frontend-application-flow.md)
- [Treasury operations](docs/treasury-operations.md)
- [Reconciliation, forecasting, and FX](docs/reconciliation-forecasting-and-fx.md)
- [Investments and credit facilities](docs/investments-and-credit.md)
- [Alerts, audit, and reporting](docs/alerts-audit-and-reporting.md)
- [API reference](docs/api-reference.md)
- [Development and deployment](docs/development-and-deployment.md)
- [UAT scenarios](docs/uat-scenarios.md)

## Technology

- .NET 10 and ASP.NET Core Web API
- Entity Framework Core 10
- PostgreSQL
- JWT access tokens and rotating refresh-token sessions
- TOTP multi-factor authentication
- FluentValidation
- xUnit, Moq, and PostgreSQL Testcontainers

## Quick start

1. Install the .NET 10 SDK and PostgreSQL.
2. Configure the development connection string and JWT secret
   with .NET user secrets.
3. Apply migrations:

   ```powershell
   dotnet ef database update `
     --project Treasury.Infrastructure `
     --startup-project Treasury.Api
   ```

4. Start the API:

   ```powershell
   dotnet run --project Treasury.Api
   ```

5. Open Swagger at `https://localhost:7126/swagger`.

See [Development and deployment](docs/development-and-deployment.md)
for complete configuration and testing instructions.

## Health endpoints

- `GET /health/live` — the API process is running.
- `GET /health/ready` — required dependencies, including
  PostgreSQL, are reachable.

All API responses carry an `X-Correlation-ID` header for
support and log correlation.
