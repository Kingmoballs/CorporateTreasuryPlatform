# Render presentation deployment

This guide deploys the API as one Render Free web service in Frankfurt.
Neon stores PostgreSQL data and ASP.NET data-protection keys, Cloudflare
Pages hosts the React application, and Resend delivers account emails
over HTTPS.

## Deployment order

1. Choose the final Cloudflare Pages project name.
2. Prepare the Neon .NET connection string.
3. Create the Resend API key and sender.
4. Push the backend repository, including `render.yaml`, to GitHub.
5. Create the Render Blueprint and verify both health endpoints.
6. Deploy the frontend with the Render API origin.
7. Run the end-to-end smoke tests in this guide.

## 1. Choose the frontend origin

Choose a Cloudflare Pages project name before creating the Render
service. Its production origin will be:

```text
https://<frontend-project>.pages.dev
```

The origin must not end with `/`. Keep it available for the Render
environment values below.

## 2. Prepare Neon

In the Neon project dashboard, select **Connect** and choose the .NET
connection-string snippet. Use a direct, non-pooler hostname for this
single-instance presentation deployment because EF Core migrations run
during API startup.

The value stored in Render must use Npgsql's key/value format, similar
to:

```text
Host=<neon-host>;Port=5432;Database=<database>;Username=<role>;Password=<password>;SSL Mode=Require;Maximum Pool Size=5
```

Keep the value private. Do not commit it, paste it into frontend
variables, or include it in screenshots.

## 3. Prepare Resend

Create a Resend account, then create a sending API key. The Render Free
service cannot use the usual SMTP ports, so the backend calls Resend's
HTTPS API.

For a quick private demonstration without a domain:

- use `onboarding@resend.dev` as the sender address;
- send invitations and password resets only to the email address used
  for the Resend account.

To email any user, add a domain you own to Resend, publish the supplied
DNS records, wait for verification, and use an address on that domain,
for example `no-reply@mail.example.com`.

Save the API key when it is shown. It will be entered once in Render and
must never be committed.

## 4. Create the Render Blueprint

In the Render dashboard:

1. Select **New > Blueprint**.
2. Connect the GitHub repository containing this backend.
3. Render detects the root `render.yaml` file.
4. Confirm the Free plan and Frankfurt region.
5. Enter every value for which the Blueprint displays a prompt.

Use these prompted values:

| Key | Value |
|---|---|
| `AllowedHosts` | `<render-service-name>.onrender.com` |
| `ConnectionStrings__DefaultConnection` | The private Neon Npgsql connection string |
| `JwtSettings__Issuer` | `https://<render-service-name>.onrender.com` |
| `DeploymentReadiness__AllowedOrigins__0` | `https://<frontend-project>.pages.dev` |
| `UserInvitations__AcceptanceUrl` | `https://<frontend-project>.pages.dev/accept-invitation` |
| `PasswordRecovery__ResetUrl` | `https://<frontend-project>.pages.dev/reset-password` |
| `EmailDelivery__ResendApiKey` | The private `re_...` API key |
| `EmailDelivery__FromAddress` | `onboarding@resend.dev` for owner-only testing, or an address on the verified domain |

The Blueprint generates the JWT signing secret. Do not replace or
rotate it during the presentation because existing access tokens would
be invalidated.

If Render changes the requested service name, update `AllowedHosts` and
`JwtSettings__Issuer` to the actual `onrender.com` hostname before
retrying the deployment.

## 5. Verify the API

The first deployment builds the Docker image and applies EF Core
migrations. When Render reports the service as live, open:

```text
https://<render-service-name>.onrender.com/health/live
https://<render-service-name>.onrender.com/health/ready
```

Both must report `Healthy`. Liveness confirms the API process is
running; readiness also verifies Neon connectivity. If readiness fails,
review the Render logs and the Neon connection value without posting the
secret anywhere.

Migration on startup is acceptable only for this one-instance
presentation service. Disable
`DeploymentReadiness__MigrateDatabaseOnStartup` before scaling the API
and run migrations as a separate release operation.

## 6. Connect Cloudflare Pages

Create a Cloudflare Pages project from the frontend GitHub repository.
Use:

```text
Build command: npm run build
Build output directory: dist
Environment variable:
VITE_API_ORIGIN=https://<render-service-name>.onrender.com
```

After Cloudflare assigns the production URL, confirm it exactly matches
all three frontend values stored in Render: the allowed origin,
invitation URL, and password-reset URL. Redeploy the frontend after
changing `VITE_API_ORIGIN` because Vite embeds it at build time.

## 7. Smoke test

Use a private browser window and verify:

1. the frontend loads after the API wake-up message;
2. login succeeds and the workspace opens;
3. a page refresh restores the session through the secure cookie;
4. logout clears the session;
5. organization approval sends the initial-admin invitation;
6. the HTTPS invitation link opens the acceptance page;
7. password recovery sends an HTTPS reset link;
8. `/health/ready` remains healthy after the flows.

The temporary `pages.dev` and `onrender.com` origins are cross-site, so
the presentation cookie uses `SameSite=None; Secure`. Browser privacy
settings can block third-party cookies; test in the actual presentation
browser. A later custom domain should use sibling hosts such as
`app.example.com` and `api.example.com` and return the cookie to a
same-site policy.

## Free-tier limitations

- Render spins the API down after 15 minutes without inbound traffic.
- The first request after sleep can take about one minute; the frontend
  retries and explains the delay.
- Scheduled background work does not run while the service sleeps.
- The container filesystem is ephemeral. PostgreSQL and data-protection
  keys therefore remain in Neon.
- This topology is suitable for a demonstration, not a production
  treasury workload.
