# UAT scenarios

Use these scenarios to validate the application end to end
before production acceptance. Record the request, response,
business reference, test user, UTC time, expected result, actual
result, and correlation ID for every test.

## Test identities

Prepare separate identities for:

- one `PlatformAdmin`;
- two organization `Admin` users;
- two `TreasuryOfficer` users;
- two `FinanceManager` users;
- two `CFO` users; and
- users in a second organization for isolation tests.

Do not reuse the maker identity as the checker in
separation-of-duty scenarios.

## Acceptance checklist

### UAT-01: Organization onboarding

1. Submit an organization application with a GUID
   `Idempotency-Key`.
2. Retry the exact request with the same key.
3. Verify that only one application exists.
4. Begin review as `PlatformAdmin`.
5. Approve with unique organization code and slug.
6. Verify the organization, default legal entity, default
   business unit, default policies, and first Admin invitation.
7. Accept the invitation and log in as the first Admin.

Expected: provisioning is atomic; retry is idempotent; the first
user has `Admin` membership only in the new organization.

### UAT-02: Rejected application

Submit and review another application, then reject it with a
reason.

Expected: no organization or invitation is provisioned and the
decision is retained.

### UAT-03: MFA and recovery codes

Enroll MFA, confirm it, sign out, log in, complete the MFA
challenge, then test one recovery code.

Expected: login does not issue a normal session until the
challenge succeeds; the recovery code cannot be reused.

### UAT-04: Sessions and refresh rotation

Log in from two controlled API clients with independent cookie
jars, list sessions, refresh one session, capture and attempt to
reuse its older cookie value in the test harness, revoke another
session, and test logout-all. Confirm that normal browser
application code cannot read the HttpOnly cookie.

Expected: refresh rotates; reuse triggers the configured security
response; revoked sessions cannot refresh.

### UAT-05: User invitations and roles

As organization `Admin`, invite one user for every organization
role. Accept invitations, change one role, and deactivate one
membership.

Expected: the reserved `PlatformAdmin` role is unavailable;
deactivated membership cannot access organization data.

### UAT-06: Tenant isolation

Create distinct accounts and transactions in two organizations.
Try to retrieve organization A identifiers with organization B
tokens. For a multi-membership user, switch organization,
replace the in-memory access token, and verify that the API
replaces the HttpOnly refresh cookie.

Expected: cross-tenant resources are not returned; after
switching, all data belongs to the selected organization.

### UAT-07: Direct opening balance

Create an account with a non-zero opening balance.

Expected: one completed `OpeningBalance` transaction and ledger
entry exist, and account balance agrees.

### UAT-08: Historical reporting import

1. Download the `HistoricalTransactions` template.
2. Upload an invalid dry-run and export its errors.
3. Upload a corrected file with a new key.
4. Submit it.
5. Attempt approval as uploader.
6. Approve as an independent eligible reviewer.
7. Commit as `Admin`.
8. Search and export committed records.

Expected: invalid batch is not submittable; uploader approval is
blocked; committed history is reportable and live balances do
not change.

### UAT-09: Cutover opening balances

Prepare zero-balance accounts, then dry-run and submit a
`CutoverOpeningBalances` batch.

Expected:

- one `Admin` and one `CFO` role approval are required;
- uploader cannot approve;
- commit is restricted to `Admin`;
- every row posts atomically;
- reconciliation has no variance; and
- a repeat attempt against active accounts is rejected.

### UAT-10: Cash receipt and idempotency

Post a receipt, then retry the same business request using its
same idempotency key and payload.

Expected: one transaction and one balance effect exist.

### UAT-11: Payment approval and reservation

Create a payment above its approval threshold.

Expected:

- the payment is pending;
- funds are reserved;
- maker approval is blocked;
- approval counts accumulate without duplicate approvers;
- full approval posts once and consumes the reservation; and
- rejection or expiry releases the reservation.

### UAT-12: Internal transfer

Transfer value between two eligible accounts and test both an
immediate and policy-controlled amount.

Expected: debits and credits are atomic, total value is
preserved, and maker-checker rules apply.

### UAT-13: Reversal

Request reversal of a completed transaction, reject one request,
then approve another valid request.

Expected: the original transaction remains immutable; an
approved request creates one linked compensating transaction.

### UAT-14: Concurrency conflict

Open the same policy, application, or import batch in two
clients. Complete a state change in client A, then submit the
stale concurrency token from client B.

Expected: client B receives `409` and must reload current state.

### UAT-15: Bank reconciliation

Import a test statement, run auto-match, manually match an
unmatched line, reconcile it, unmatch a test line, and ignore a
documented non-book line.

Expected: summary counts and both exception exports agree with
the final line states.

### UAT-16: Forecast and variance

Create expected inflows and outflows, realize one, cancel one,
and run variance reporting.

Expected: active forecasts and variance reflect each transition.

### UAT-17: FX exposure

Record rates for the test currencies, query latest rates,
conversion, cash position, and exposure.

Expected: reporting values use the intended effective rate;
missing or stale rate behavior is visible and documented.

### UAT-18: Investment lifecycle

Create counterparty and limit records, create a placement,
activate and approve it, generate accrual information, then test
a controlled redemption or rollover.

Expected: funding posts once, limit utilization changes, maker
cannot self-approve, and execution is separate from approval.

### UAT-19: Credit-facility lifecycle

Create and activate a facility, draw down, accrue interest,
repay, suspend, reactivate, and close it where rules allow.

Expected: cash and facility positions agree after each operation,
and invalid lifecycle transitions are rejected.

### UAT-20: Alerts, reports, and audit

Create conditions that trigger low-liquidity, old-pending, and
reconciliation alerts. Run a scan, resolve or dismiss with
rationale, query dashboards, export reports, and locate the
associated audit events.

Expected: counts agree across detail and summary; exports are
tenant-scoped; evidence includes correlation IDs.

### UAT-21: Authorization

For every role, call one permitted endpoint and one endpoint
reserved for a more privileged role.

Expected: permitted call succeeds; prohibited call returns `403`
without disclosing another tenant's data.

### UAT-22: Rate limits and error safety

Exercise login, password-recovery, MFA, and public application
limits in a non-production test environment. Submit invalid
requests and cause a safe not-found response.

Expected: throttled requests return `429` with retry guidance;
errors contain stable codes and correlation details but no stack
trace or secrets.

### UAT-23: Production readiness

Deploy the release candidate with production-like settings.
Check liveness, readiness, credentialed CORS from the allowed
frontend, rejection of an unlisted origin, Secure/HttpOnly
refresh-cookie behavior, HTTPS behavior, forwarded headers
through the trusted proxy, email delivery, and persistent
data-protection keys across an application restart.

Expected: readiness is healthy only when PostgreSQL is
reachable; unsafe Production configuration prevents startup.

## Exit criteria

- All critical and high-risk scenarios pass.
- No unresolved balance, ledger, approval, reconciliation, or
  tenant-isolation discrepancy remains.
- Security failures expose no secrets or cross-tenant data.
- Migration, backup, rollback, monitoring, and support ownership
  are accepted.
- Business owners sign off the onboarding, payment approval,
  cutover, reconciliation, investment, credit, and reporting
  evidence.
