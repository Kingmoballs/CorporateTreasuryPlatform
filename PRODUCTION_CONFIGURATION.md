# Production configuration

Do not put production passwords, connection strings, or
cryptographic keys in committed JSON files. Start from
`Treasury.Api/appsettings.Production.example.json`, then supply
secret values through the hosting platform's secret manager or
environment variables.

The application fails during Production startup when a critical
setting is unsafe or missing. Development startup is not subject
to these production-only checks.

## Required environment values

- `ConnectionStrings__DefaultConnection`
- `JwtSettings__SecretKey` (at least 32 characters and generated
  from a cryptographically secure source)
- `JwtSettings__Issuer`
- `JwtSettings__Audience`
- `AllowedHosts` (semicolon-separated API hosts; never `*`)
- `DeploymentReadiness__AllowedOrigins__0` and further indexed
  entries for every HTTPS frontend origin
- `DeploymentReadiness__DataProtectionKeysPath` pointing to
  persistent storage shared by all API instances
- `RefreshTokenCookie__Secure=true`
- `RefreshTokenCookie__SameSite=Strict` for the recommended
  same-site frontend/API deployment. Use `None` only when the
  frontend is genuinely cross-site; `Secure` remains mandatory.
- `UserInvitations__AcceptanceUrl`
- `PasswordRecovery__ResetUrl`

When the API is behind a load balancer or reverse proxy, set:

- `DeploymentReadiness__UseForwardedHeaders=true`
- `DeploymentReadiness__TrustedProxies__0` and further indexed
  entries to the proxy IP addresses

SMTP may remain disabled in Development. Production requires it
by default because invitations and password recovery depend on
delivery. Configure:

- `EmailDelivery__Enabled=true`
- `EmailDelivery__Host`
- `EmailDelivery__Port`
- `EmailDelivery__Username`
- `EmailDelivery__Password`
- `EmailDelivery__FromAddress`

## Platform health

- `GET /health/live` confirms that the API process is running.
- `GET /health/ready` confirms that required dependencies,
  currently PostgreSQL, are reachable.

Health responses contain status and timing information only; they
never include connection strings or exception details.

Every response includes `X-Correlation-ID`. Clients may send a
safe correlation ID in that header, or the API generates one.
Use it to connect client errors with server logs.
