# Local and server setup

## Local Windows environment

Prerequisites installed on this machine:

- .NET SDK 8.0.425
- MySQL Community Server 8.4

Start the database and web application from PowerShell:

```powershell
.\scripts\Start-Local.ps1
```

The command prints the generated administrator password. Credentials, application
PIDs, and application logs live under `.local/`; this directory is excluded from Git.
MySQL data and logs live under `C:\ProgramData\SuperShortLink\mysql` because MySQL on
Windows cannot initialize inside the Chinese-character workspace path. Stop both
processes with:

```powershell
.\scripts\Stop-Local.ps1
```

Important log files are `.local/app.stderr.log` and
`C:\ProgramData\SuperShortLink\mysql\mysql-error.log`.

## Linux server with Docker Compose

Install Docker Engine and the Compose plugin, clone this repository, then create the
deployment environment file:

```bash
cp .env.example .env
chmod 600 .env
```

Replace every `replace-with-...` value with a long random password. Keep
`SHORTLINK_SECRET` at exactly 62 unique Base62 characters and do not change it after
links have been created. Start and verify the stack:

```bash
docker compose up -d --build
docker compose ps
docker compose logs --tail=100 app
```

After startup, use `/health/live` for a process liveness probe and `/health/ready`
for a readiness probe that also verifies the database connection.

To change the administrator password, log in and select **修改密码** in the top bar.
Enter the current password and a new password of at least 12 characters. The
application signs out the current session and invalidates other sessions after a
successful change. The new salted password hash is stored in the persistent
`admin-credentials` Docker volume. Back up this volume along with `.env` and
`mysql-data`.

If the password is forgotten, SSH to the server and restore access using the
bootstrap password in `.env`. Preserve the previous hash for recovery:

```bash
cd /opt/supershortlink
docker compose -p supershortlink -f compose.yaml -f deploy/compose.runtime.yaml exec app \
  sh -c 'mv /app-data/admin-password.hash /app-data/admin-password.hash.backup-$(date +%Y%m%d-%H%M%S)'
docker compose -p supershortlink -f compose.yaml -f deploy/compose.runtime.yaml restart app
```

After restart, sign in with `ADMIN_USER` and `ADMIN_PASSWORD` from `.env`, then
change the password in the application. Once an in-app password has been set,
editing `.env` alone does not override it.

The application listens only on `127.0.0.1:${APP_PORT}`. Put Nginx or Caddy in front
of it, enable HTTPS, and proxy to `http://127.0.0.1:5080`. Back up both `.env` and the
`mysql-data` Docker volume. Restoring only the database without the same
`SHORTLINK_SECRET` makes existing short codes undecodable.

For an existing production database, run
`build/mysql_migrate_002_metadata.sql` and then
`build/mysql_migrate_003_link_status.sql` once before deploying the new application.

## Link safety and batch generation

Single and batch generation share the same publication policy. The application rejects
non-HTTP(S) URLs, URLs containing credentials, localhost/private/cloud-metadata targets,
configured blocked domains, and configured risk keywords. Set `ShortLink:UrlSafety` (or
the matching environment variables) to add a domain blocklist or connect an approved
content-moderation provider. A provider must return JSON such as `{"allowed":true}`;
provider errors fail closed and do not publish the link.

Published links can be enabled, disabled, and rechecked from the records page, including
batch operations. The background review checks the oldest reviewed active links every
`URL_REVIEW_INTERVAL_MINUTES`; links that fail the current policy are disabled and their
cached redirects are removed. Without an external moderation provider, this recheck
applies URL, DNS, domain, and keyword policy only; the provider is responsible for
inspecting destination-page content and reputation.

This is a compliance safeguard, not a way to bypass WeChat or any other platform's
review. No implementation can guarantee that a clean short URL will never be blocked;
use a verified domain, HTTPS, clear abuse reporting, and respond to platform notices.

The Dashboard accepts one long URL per line and returns a per-line result. Each record
also supports a remark and group. The records page supports selecting multiple rows and
updating their remark/group in one operation.

Useful maintenance commands:

```bash
docker compose pull
docker compose up -d --build
docker compose logs -f app
docker compose down
```

`docker compose down` keeps database data. Do not add `--volumes` unless the database
is intentionally being deleted.
