# API reference

This is a route catalog for the implemented controllers. Swagger
is the authoritative interactive description of request and
response schemas in Development.

## Conventions

- All routes are relative to the API host.
- Protected routes require
  `Authorization: Bearer <access-token>`.
- `{id}`, `{batchId}`, and similar segments are resource
  identifiers.
- Query parameters are omitted below for readability.
- CSV endpoints return a downloadable file.
- Unless marked public or platform-only, data is scoped to the
  active organization in the token.
- See [Roles and access](roles-and-access.md) for the capability
  matrix.

## Platform health

| Method | Route | Access |
|---|---|---|
| GET | `/health/live` | Public |
| GET | `/health/ready` | Public |

## Organization applications

| Method | Route | Purpose |
|---|---|---|
| POST | `/api/v1/organization-applications` | Submit a public application; requires a GUID `Idempotency-Key` header |
| GET | `/api/platform/organization-applications` | PlatformAdmin application search |
| GET | `/api/platform/organization-applications/{applicationId}` | PlatformAdmin application detail |
| POST | `/api/platform/organization-applications/{applicationId}/review` | Begin review |
| POST | `/api/platform/organization-applications/{applicationId}/approve` | Provision organization and first Admin invitation |
| POST | `/api/platform/organization-applications/{applicationId}/reject` | Reject application |
| POST | `/api/platform/organization-applications/{applicationId}/admin-invitation/resend` | Resend first Admin invitation |

## Authentication and sessions

| Method | Route | Access/purpose |
|---|---|---|
| POST | `/api/v1/auth/login` | Public login |
| POST | `/api/v1/auth/refresh` | Public token refresh |
| POST | `/api/v1/auth/invitations/accept` | Public invitation acceptance |
| POST | `/api/v1/auth/password/forgot` | Public password-reset request |
| POST | `/api/v1/auth/password/reset` | Public password reset |
| POST | `/api/v1/auth/mfa/challenges/verify` | Public MFA challenge verification |
| POST | `/api/v1/auth/mfa/challenges/recovery-code` | Public recovery-code challenge |
| POST | `/api/v1/auth/logout` | Revoke current session |
| POST | `/api/v1/auth/logout-all` | Revoke all owned sessions |
| POST | `/api/v1/auth/logout-others` | Revoke other owned sessions |
| GET | `/api/v1/auth/sessions` | List active owned sessions |
| DELETE | `/api/v1/auth/sessions/{sessionId}` | Revoke one owned session |
| GET | `/api/v1/auth/organizations` | List active memberships |
| POST | `/api/v1/auth/organizations/switch` | Switch membership and issue a new token pair |
| POST | `/api/v1/auth/mfa/enrollment/start` | Start TOTP enrollment |
| POST | `/api/v1/auth/mfa/enrollment/confirm` | Confirm TOTP enrollment |
| POST | `/api/v1/auth/mfa/recovery-codes/regenerate` | Replace recovery codes |
| POST | `/api/v1/auth/mfa/disable` | Disable MFA after verification |
| GET | `/api/v1/auth/me` | Return current identity claims |
| GET | `/api/v1/admin/authentication-security-events` | Admin security-event search |

## Organization administration

| Method | Route |
|---|---|
| GET | `/api/v1/organization` |
| PUT | `/api/v1/organization` |
| GET | `/api/v1/organization/legal-entities` |
| POST | `/api/v1/organization/legal-entities` |
| PUT | `/api/v1/organization/legal-entities/{id}` |
| PATCH | `/api/v1/organization/legal-entities/{id}/status` |
| GET | `/api/v1/organization/business-units` |
| POST | `/api/v1/organization/business-units` |
| PUT | `/api/v1/organization/business-units/{id}` |
| PATCH | `/api/v1/organization/business-units/{id}/status` |
| GET | `/api/admin/users` |
| GET | `/api/admin/roles` |
| PATCH | `/api/admin/users/{userId}/role` |
| PATCH | `/api/admin/users/{userId}/status` |
| POST | `/api/admin/invitations` |
| GET | `/api/admin/invitations` |
| POST | `/api/admin/invitations/{invitationId}/resend` |
| DELETE | `/api/admin/invitations/{invitationId}` |
| GET | `/api/admin/approval-policies` |
| PUT | `/api/admin/approval-policies` |

Organization profile and structure reads are available to the
organization roles; writes and user/policy administration
require `Admin`.

## Accounts, cash, and transactions

| Method | Route |
|---|---|
| POST | `/api/accounts` |
| GET | `/api/accounts` |
| GET | `/api/accounts/{id}/ledger` |
| POST | `/api/cash-movements/receipts` |
| POST | `/api/cash-movements/payments` |
| GET | `/api/cash-movements/payments/pending` |
| POST | `/api/cash-movements/payments/{id}/approve` |
| POST | `/api/cash-movements/payments/{id}/reject` |
| POST | `/api/transfers` |
| GET | `/api/transfers/pending` |
| POST | `/api/transfers/{id}/approve` |
| POST | `/api/transfers/{id}/reject` |
| POST | `/api/transactions/{reference}/reversal-request` |
| GET | `/api/reversals/pending` |
| POST | `/api/reversals/{id}/approve` |
| POST | `/api/reversals/{id}/reject` |
| GET | `/api/transactions` |
| GET | `/api/transactions/activity-summary` |
| GET | `/api/transactions/export/csv` |
| GET | `/api/transactions/{reference}` |

Approval history:

| Method | Route |
|---|---|
| GET | `/api/approval-history/transfers/{requestId}` |
| GET | `/api/approval-history/payments/{requestId}` |
| GET | `/api/approval-history/reversals/{requestId}` |
| GET | `/api/approval-history/investment-placements/{placementId}` |

## Historical imports

| Method | Route |
|---|---|
| GET | `/api/v1/historical-imports/template` |
| GET | `/api/v1/historical-imports` |
| GET | `/api/v1/historical-imports/dashboard` |
| POST | `/api/v1/historical-imports/dry-run` |
| GET | `/api/v1/historical-imports/{batchId}` |
| GET | `/api/v1/historical-imports/{batchId}/rows` |
| GET | `/api/v1/historical-imports/{batchId}/errors` |
| GET | `/api/v1/historical-imports/{batchId}/errors/export/csv` |
| POST | `/api/v1/historical-imports/{batchId}/submit` |
| POST | `/api/v1/historical-imports/{batchId}/approve` |
| POST | `/api/v1/historical-imports/{batchId}/reject` |
| GET | `/api/v1/historical-imports/{batchId}/decisions` |
| GET | `/api/v1/historical-imports/{batchId}/approval-report` |
| GET | `/api/v1/historical-imports/{batchId}/approval-report/export/csv` |
| GET | `/api/v1/historical-imports/{batchId}/opening-balance-reconciliation` |
| GET | `/api/v1/historical-imports/{batchId}/opening-balance-reconciliation/export/csv` |
| POST | `/api/v1/historical-imports/{batchId}/commit` |
| GET | `/api/v1/historical-imports/records` |
| GET | `/api/v1/historical-imports/records/{recordId}` |
| GET | `/api/v1/historical-imports/records/export/csv` |

`dry-run` consumes `multipart/form-data` and requires a GUID
`Idempotency-Key` header.

## Bank statements

| Method | Route |
|---|---|
| POST | `/api/bank-statements/imports/csv` |
| POST | `/api/bank-statements/imports/pdf` |
| POST | `/api/bank-statements/imports` |
| GET | `/api/bank-statements/imports/{id}` |
| GET | `/api/bank-statements/imports/{id}/summary` |
| GET | `/api/bank-statements/imports/{id}/exceptions` |
| GET | `/api/bank-statements/imports/{id}/book-exceptions` |
| GET | `/api/bank-statements/imports/{id}/exceptions/export/csv` |
| GET | `/api/bank-statements/imports/{id}/book-exceptions/export/csv` |
| POST | `/api/bank-statements/imports/{id}/auto-match` |
| POST | `/api/bank-statements/lines/{id}/manual-match` |
| POST | `/api/bank-statements/lines/{id}/reconcile` |
| POST | `/api/bank-statements/lines/{id}/unmatch` |
| POST | `/api/bank-statements/lines/{id}/ignore` |
| GET | `/api/bank-statements/unmatched` |

## Forecasts and FX

| Method | Route |
|---|---|
| POST | `/api/cash-flow-forecasts` |
| GET | `/api/cash-flow-forecasts/{id}` |
| GET | `/api/cash-flow-forecasts/active` |
| POST | `/api/cash-flow-forecasts/{id}/cancel` |
| POST | `/api/cash-flow-forecasts/{id}/realize` |
| GET | `/api/cash-flow-forecasts/report` |
| GET | `/api/cash-flow-forecasts/variance` |
| GET | `/api/cash-flow-forecasts/variance/export/csv` |
| POST | `/api/fx-rates` |
| PUT | `/api/fx-rates/{id}` |
| GET | `/api/fx-rates/{id}` |
| GET | `/api/fx-rates` |
| GET | `/api/fx-rates/latest` |
| GET | `/api/fx-rates/convert` |
| GET | `/api/fx-rates/cash-position` |
| GET | `/api/fx-rates/currency-exposure` |

## Counterparties and investments

| Method | Route |
|---|---|
| POST | `/api/counterparties` |
| GET | `/api/counterparties` |
| GET | `/api/counterparties/{id}` |
| PUT | `/api/counterparties/{id}` |
| PATCH | `/api/counterparties/{id}/status` |
| POST | `/api/investment-limits` |
| GET | `/api/investment-limits` |
| GET | `/api/investment-limits/{id}` |
| PUT | `/api/investment-limits/{id}` |
| PATCH | `/api/investment-limits/{id}/status` |
| GET | `/api/investment-limits/utilization` |
| GET | `/api/investment-limits/utilization/export/csv` |
| POST | `/api/investment-placements` |
| GET | `/api/investment-placements` |
| GET | `/api/investment-placements/{id}` |
| POST | `/api/investment-placements/{id}/activate` |
| POST | `/api/investment-placements/{id}/approve-activation` |
| POST | `/api/investment-placements/{id}/reject-activation` |
| PATCH | `/api/investment-placements/{id}/counterparty` |
| POST | `/api/investment-placements/{id}/redeem` |
| POST | `/api/investment-placements/{id}/cancel` |
| POST | `/api/investment-placements/process-maturities` |
| GET | `/api/investment-placements/portfolio-report` |
| GET | `/api/investment-placements/portfolio-report/export/csv` |
| GET | `/api/investment-placements/maturity-schedule` |
| GET | `/api/investment-accruals/report` |
| POST | `/api/investment-accrual-snapshots/generate` |
| GET | `/api/investment-accrual-snapshots` |
| GET | `/api/investment-accrual-snapshots/export/csv` |

Early redemption:

| Method | Route |
|---|---|
| GET | `/api/investment-early-redemptions/{investmentPlacementId}/quote` |
| POST | `/api/investment-early-redemptions/{investmentPlacementId}/requests` |
| GET | `/api/investment-early-redemptions/requests/{requestId}` |
| GET | `/api/investment-early-redemptions/requests/pending` |
| POST | `/api/investment-early-redemptions/requests/{requestId}/approve` |
| POST | `/api/investment-early-redemptions/requests/{requestId}/reject` |
| POST | `/api/investment-early-redemptions/requests/{requestId}/execute` |

Rollover:

| Method | Route |
|---|---|
| GET | `/api/investment-rollovers/{investmentPlacementId}/quote` |
| POST | `/api/investment-rollovers/{investmentPlacementId}/requests` |
| GET | `/api/investment-rollovers/requests/{requestId}` |
| GET | `/api/investment-rollovers/requests/pending` |
| POST | `/api/investment-rollovers/requests/{requestId}/approve` |
| POST | `/api/investment-rollovers/requests/{requestId}/reject` |
| POST | `/api/investment-rollovers/requests/{requestId}/execute` |

## Credit facilities

| Method | Route |
|---|---|
| POST | `/api/credit-facilities` |
| GET | `/api/credit-facilities` |
| GET | `/api/credit-facilities/{id}` |
| PUT | `/api/credit-facilities/{id}` |
| POST | `/api/credit-facilities/{id}/activate` |
| POST | `/api/credit-facilities/{id}/approve-activation` |
| POST | `/api/credit-facilities/{id}/reject-activation` |
| POST | `/api/credit-facilities/{id}/cancel` |
| POST | `/api/credit-facilities/{id}/suspend` |
| POST | `/api/credit-facilities/{id}/reactivate` |
| POST | `/api/credit-facilities/{id}/close` |
| POST | `/api/credit-facilities/process-maturities` |
| POST | `/api/credit-facilities/{creditFacilityId}/drawdowns` |
| GET | `/api/credit-facilities/{creditFacilityId}/drawdowns` |
| GET | `/api/credit-facilities/{creditFacilityId}/drawdowns/{drawdownId}` |
| POST | `/api/credit-facilities/{creditFacilityId}/repayments` |
| GET | `/api/credit-facilities/{creditFacilityId}/repayments` |
| GET | `/api/credit-facilities/{creditFacilityId}/repayments/{repaymentId}` |
| POST | `/api/credit-facility-interest-accruals/generate` |
| GET | `/api/credit-facility-interest-accruals` |

## Alerts, audit, and reporting

| Method | Route |
|---|---|
| POST | `/api/treasury-alerts` |
| GET | `/api/treasury-alerts` |
| GET | `/api/treasury-alerts/summary` |
| GET | `/api/treasury-alerts/export/csv` |
| POST | `/api/treasury-alerts/{id}/resolve` |
| POST | `/api/treasury-alerts/{id}/dismiss` |
| POST | `/api/treasury-alerts/run-scan` |
| GET | `/api/audit-logs` |
| GET | `/api/audit-logs/export/csv` |
| GET | `/api/treasury/balances` |
| GET | `/api/treasury/dashboard` |
| GET | `/api/treasury/liquidity` |
| GET | `/api/treasury/liquidity/export/csv` |

## Response behavior

- Creation commonly returns `201 Created`.
- Successful queries and state transitions commonly return
  `200 OK`.
- Successful operations with no body return `204 No Content`.
- Password-reset request returns `202 Accepted`.
- Validation, authorization, conflict, and business-rule errors
  use the standard error shape described in
  [Architecture and security](architecture-and-security.md).
- Every response includes `X-Correlation-ID`.

