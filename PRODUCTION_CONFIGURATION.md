# Production configuration

Do not put production passwords, connection strings, or cryptographic
keys in committed JSON files. Start from
`Treasury.Api/appsettings.Production.example.json`, then supply secret
values through the hosting platform's secret manager or environment
variables.

The application fails during Production startup when a critical setting
is unsafe or missing. Development startup is not subject to these
production-only checks.

## Required environment values

- `ConnectionStrings__DefaultConnection`
- `JwtSettings__SecretKey` (at least 32 characters from a
  cryptographically secure source)
- `JwtSettings__Issuer`
- `JwtSettings__Audience`
- `AllowedHosts` (semicolon-separated API hosts; never `*`)
- `DeploymentReadiness__AllowedOrigins__0` and further indexed entries
  for every HTTPS frontend origin
- either `DeploymentReadiness__PersistDataProtectionKeysToDatabase=true`
  or `DeploymentReadiness__DataProtectionKeysPath` pointing to shared
  persistent storage. Database persistence is required on Render Free
  because its filesystem is ephemeral.
- `RefreshTokenCookie__Secure=true`
- `RefreshTokenCookie__SameSite=Strict` for a same-site frontend/API.
  Use `None` only when the frontend is genuinely cross-site; `Secure`
  remains mandatory.
- `UserInvitations__AcceptanceUrl`
- `PasswordRecovery__ResetUrl`

When the API is behind a load balancer or reverse proxy, set:

- `DeploymentReadiness__UseForwardedHeaders=true`
- `DeploymentReadiness__TrustedProxies__0` and further indexed entries
  to known proxy IP addresses; or
- `DeploymentReadiness__TrustForwardedHeadersFromAnyProxy=true` only
  when a managed platform such as Render prevents clients from reaching
  the container directly and controls the forwarded headers.

For the single-instance Render presentation deployment, set
`DeploymentReadiness__MigrateDatabaseOnStartup=true`. Disable it before
scaling to multiple API instances and run migrations as a separate
release operation instead.

## Email delivery

Email may remain disabled in Development. Production requires delivery
by default because invitations and password recovery depend on it.
Render Free blocks the usual SMTP ports, so configure Resend over HTTPS:

- `EmailDelivery__Enabled=true`
- `EmailDelivery__Provider=Resend`
- `EmailDelivery__ResendApiKey`
- `EmailDelivery__FromAddress`

The backend also supports `Provider=Smtp` for environments that allow
SMTP. That provider additionally requires host and port settings, plus
credentials when required by the SMTP service.

## Platform health

- `GET /health/live` confirms that the API process is running.
- `GET /health/ready` confirms that required dependencies, currently
  PostgreSQL, are reachable.

Health responses contain status and timing information only; they never
include connection strings or exception details.

Every response includes `X-Correlation-ID`. Clients may send a safe
correlation ID in that header, or the API generates one. Use it to
connect client errors with server logs.
