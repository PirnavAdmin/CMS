# Deploy on the server

## Host setup

1. In your registrar's DNS panel, point `pulseleef.com` and `www.pulseleef.com` to `43.205.42.69` with A records. Remove conflicting A/AAAA records.
2. Install Docker Engine, the Docker Compose plugin, and Nginx on the server. MySQL is installed on the host; Compose does not start a database container.
3. Configure MySQL to accept connections from Docker's bridge gateway. On Linux, this usually means setting `bind-address` to `0.0.0.0`, restarting MySQL, and creating/granting the application user for the Docker bridge host (for example `'cmsdbuser'@'%'`). Restrict MySQL port 3306 to the server's local/Docker network in the firewall; do not expose it publicly.
4. Ensure the migrated MySQL database `cmsdb` and tables already exist. This app's SQL Server EF migrations are not compatible with MySQL and are not run at startup.

## Configure and start the app

Create `.env` from `.env.example` and set the MySQL host credentials, database name, SSL mode, and a private JWT key of at least 32 characters. The Compose default `DB_HOST=host.docker.internal` resolves to the Docker host using `host-gateway`; change it if your Docker setup uses a different route. Keep `.env` private; it is git-ignored.

Start the frontend and backend:

```sh
docker compose up -d --build
```

The backend uses the Pomelo EF Core MySQL provider. The existing SQL Server migrations remain in the repository for historical reference; the app does not execute them against MySQL. Because the database has already been migrated, schema updates must be made with MySQL-compatible scripts/migrations.

## Host Nginx and DNS

Install the host Nginx site config:

```sh
sudo cp nginx/pulseleef.com.conf /etc/nginx/sites-available/pulseleef.com
sudo ln -s /etc/nginx/sites-available/pulseleef.com /etc/nginx/sites-enabled/pulseleef.com
sudo nginx -t
sudo systemctl reload nginx
```

Nginx proxies `/api`, `/images`, and `/uploads` to the backend at `127.0.0.1:5000`, and other paths to the frontend at `127.0.0.1:3000`. Allow inbound TCP 80 and 443 for the domain; ports 3000 and 5000 are published for direct access. MySQL 3306 should remain private.

The Nginx site config currently serves HTTP. Once DNS resolves and port 80 is reachable, install a Let's Encrypt certificate (for example with Certbot's Nginx plugin) to enable HTTPS.

Inspect services with `docker compose ps` and `docker compose logs -f backend`.
