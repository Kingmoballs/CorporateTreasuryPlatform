# Documentation index

This documentation describes the behavior implemented by the
current backend. It is intended for business stakeholders,
frontend developers, backend developers, testers, operators, and
support teams.

## Suggested reading order

1. [Architecture and security](architecture-and-security.md)
2. [Roles and access](roles-and-access.md)
3. [Organization onboarding and authentication](onboarding-and-authentication.md)
4. [Organization setup and cutover](organization-setup-and-cutover.md)
5. [Frontend application flow](frontend-application-flow.md)
6. [Treasury operations](treasury-operations.md)
7. [Reconciliation, forecasting, and FX](reconciliation-forecasting-and-fx.md)
8. [Investments and credit facilities](investments-and-credit.md)
9. [Alerts, audit, and reporting](alerts-audit-and-reporting.md)
10. [API reference](api-reference.md)
11. [Development and deployment](development-and-deployment.md)
12. [Render presentation deployment](render-presentation-deployment.md)
13. [UAT scenarios](uat-scenarios.md)

## Documentation conventions

- Paths are relative to the API host, for example
  `POST /api/v1/auth/login`.
- Protected endpoints require
  `Authorization: Bearer <access-token>`.
- Dates and times are UTC unless a field explicitly says
  otherwise.
- Monetary operations use the account or resource currency.
- Organization-owned records are automatically restricted to
  the organization in the authenticated token.
- `PlatformAdmin` operates in the reserved platform context.
  Organization roles do not receive cross-organization access.

## Implemented boundary

The API records and controls treasury operations. It does not
currently initiate payments through an external bank network.
Bank APIs, payment rails, SSO providers, external observability
collectors, and production infrastructure are integration and
deployment concerns that can be added around the current backend.
