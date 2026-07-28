# Roles and access

## Roles

| Role | Scope | Primary responsibility |
|---|---|---|
| `PlatformAdmin` | Platform | Reviews and provisions organization applications |
| `Admin` | One organization membership | Manages users, structure, accounts, policies, imports, and administrative operations |
| `TreasuryOfficer` | One organization membership | Initiates and monitors day-to-day treasury operations |
| `FinanceManager` | One organization membership | Reviews and approves financial operations, manages treasury master data |
| `CFO` | One organization membership | Senior review, approval, oversight, audit, and reporting |

A user can have memberships in more than one organization.
Membership status and role are organization-specific. The
organization-switch endpoint issues tokens for the selected
membership.

## Capability matrix

`Read` means search, view, report, or export. `Manage` includes
creation or status changes. Exact operation-level restrictions
remain authoritative in controller authorization.

| Capability | PlatformAdmin | Admin | TreasuryOfficer | FinanceManager | CFO |
|---|---:|---:|---:|---:|---:|
| Review organization applications | Yes | No | No | No | No |
| Manage organization users and invitations | No | Yes | No | No | No |
| Manage organization structure | No | Yes | No | No | No |
| View organization structure | No | Yes | Yes | Yes | Yes |
| Create accounts | No | Yes | No | Yes | Yes |
| View accounts and ledgers | No | Yes | Yes | Yes | Yes |
| Initiate transfers and cash movements | No | Yes | Yes | Yes | Yes |
| Approve payments, transfers, and reversals | No | Yes | No | Yes | Yes |
| Manage approval policies | No | Yes | No | No | No |
| Upload historical-import batches | No | Yes | Yes | No | No |
| Review historical-import batches | No | Yes | No | Yes | Yes |
| Commit historical-import batches | No | Yes | No | No | No |
| Manage counterparties and investment limits | No | Yes | No | Yes | Yes |
| View investment and credit modules | No | Yes | Yes | Yes | Yes |
| Fund or approve investment operations | No | Yes | No | Yes | Yes |
| Execute credit drawdowns and repayments | No | Yes | No | Yes | Yes |
| View audit logs | No | Yes | No | Yes | Yes |
| View authentication security events | No | Yes | No | No | No |
| Manage alerts | No | Yes | No | Yes | Yes |

## Important separation-of-duty rules

- A maker cannot approve their own pending request.
- A historical-import uploader cannot approve or reject that
  batch.
- Historical transaction imports require one independent
  `Admin`, `FinanceManager`, or `CFO` approval.
- Cutover opening balances require two independent approvals:
  exactly one `Admin` role approval and one `CFO` role approval.
- Only an organization `Admin` can commit an approved historical
  import.
- The reserved `PlatformAdmin` role cannot be assigned through
  ordinary organization user administration or invitations.
- Deactivating a user or membership prevents further access even
  if an older access token has not yet expired.

## Frontend behavior

The frontend should build navigation from the active
organization role, but it must not rely on hidden menu items as
security. The backend remains authoritative and can return
`403 operation_forbidden`.

When the role or active organization changes, discard cached
organization data and reload permissions, dashboards, pending
work, and reference data.
