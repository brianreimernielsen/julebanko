# Julebanko

Internal Christmas bingo (julebanko) web app for intranet / VPN.  
ASP.NET Core 10 Razor Pages + SQL Server + cookie auth. Deployable on IIS.

## Features

- **Player page** — today's numbers, all season numbers (draw order + sorted), claim form (1 row / 2 rows / full board), public list of **confirmed** winners only
- **Banko Admin** — draw N unique numbers 1–90 (default 2), approve/reject claims
- **Setup Admin** — everything Banko Admin can do, plus theme colors, image uploads, claim email To:, password management, season reset with backup/undo, edit/delete confirmed winners
- **Email** on claim via SMTP `outbound12.electric.net:587` (TLS, no password; IP allowlisted)
- **i18n** — Danish (default), English, Swedish

## Requirements

- .NET 10 SDK
- SQL Server (existing instance, Express, or Docker). LocalDB works on Windows.
- Outbound TCP 587 to `outbound12.electric.net` from the IIS/app server (for claim mail)

This cloud/dev image may not have SQL Server or Docker. Point `ConnectionStrings:DefaultConnection` at your SQL Server before running.

## Configuration

### Connection string

Edit `appsettings.json` / `appsettings.Development.json`, or set IIS / User Secrets:

```json
"ConnectionStrings": {
  "DefaultConnection": "Server=YOUR_SQL_SERVER;Database=Julebanko;User Id=YOUR_USER;Password=YOUR_PASSWORD;TrustServerCertificate=True;Encrypt=True"
}
```

**Do not commit real passwords.** Prefer IIS app settings or User Secrets in production.

### SMTP

File defaults in `Smtp` (seeded into `AppSettings` on first run if missing):

| Setting | Default |
|---------|---------|
| Host | `outbound12.electric.net` |
| Port | `587` |
| TLS | StartTLS / SSL |
| Username / Password | empty (provider IP-allowlists the server) |

**Setup Admin → Claim email** edits Claim To, From, and full SMTP (host/port/SSL/user/password) in the database; runtime mail uses those overrides and falls back to `appsettings.json` when keys are absent.

### Default admin logins (temporary)

Seeded on first successful migrate/seed. **Change these immediately** via Setup Admin → Passwords.

| Role | Username | Temporary password |
|------|----------|--------------------|
| Banko Admin | `bankoadmin` | `BankoTemp2026!` |
| Setup Admin | `setupadmin` | `SetupTemp2026!` |

## Local run

```bash
dotnet restore
dotnet ef database update   # requires SQL Server + connection string
dotnet run
```

Open https://localhost:5xxx (see `Properties/launchSettings.json`).

### SQL Server in Docker (optional)

```bash
docker run -e "ACCEPT_EULA=Y" -e "MSSQL_SA_PASSWORD=Your_strong_Password123" \
  -p 1433:1433 --name julebanko-sql \
  -d mcr.microsoft.com/mssql/server:2022-latest
```

Match `appsettings.Development.json` to that SA password, then:

```bash
dotnet ef database update
dotnet run
```

### EF migrations

```bash
dotnet tool install --global dotnet-ef   # once
dotnet ef migrations add <Name>
dotnet ef database update
```

**Alternatively**, run the ready T-SQL script `sql/001_CreateJulebanko.sql` in SSMS/sqlcmd (creates DB/tables/indexes, seeds admins + settings, marks the EF migration applied). Use the script **or** `dotnet ef database update` — not both for first create.

On startup the app runs `Migrate()` + seed (admins, active season, default theme/mail settings). Seed is idempotent. If SQL is unreachable, the app still starts and logs a warning; DB pages will fail until SQL is available.

## IIS deploy

1. Publish: `dotnet publish -c Release -o ./publish`
2. IIS site with **ASP.NET Core Hosting Bundle** (ANCM V2)
3. App pool: **No Managed Code**, suitable identity
4. Set connection string in IIS Configuration Editor / environment / `appsettings`
5. Grant the app pool **write** access to `App_Data/uploads` and `App_Data/backups`
6. Allow outbound SMTP to `outbound12.electric.net:587`
7. HTTPS per IT policy; restrict to intranet + VPN as needed

Uploaded images are stored under `App_Data/uploads` and served at `/uploads/...`.

## Roles

| Capability | Banko Admin | Setup Admin |
|------------|:-----------:|:-----------:|
| Draw numbers | ✓ | ✓ |
| Approve/reject claims | ✓ | ✓ |
| Theme / colors | | ✓ |
| Upload Christmas images | | ✓ |
| Claim notify To: | | ✓ |
| Change admin passwords | | ✓ |
| Season reset + undo | | ✓ |
| Edit/delete confirmed winners | | ✓ |

## Known gaps / notes

- No automatic verification of physical bingo cards (by design)
- Claim email send failures are logged; the claim is still saved as Pending
- Without SQL Server in the environment, full end-to-end smoke must be done on a machine with SQL
- Change temporary admin passwords before production use
