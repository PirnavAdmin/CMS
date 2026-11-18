# Deploy on the server

1. Point DNS at the EC2 instance in your domain registrar's DNS panel:
   - `A` record, host `@`, value `43.205.42.69`
   - `A` record, host `www`, value `43.205.42.69`
   Remove conflicting A/AAAA records for these names. DNS changes can take time to propagate.
2. Copy the repository to the server. Install Docker Engine and the Docker Compose plugin.
3. Create `.env` from `.env.example`. Use strong, unique values for DB_PASSWORD, MSSQL_SA_PASSWORD and JWT_KEY (at least 32 random characters). Keep `.env` private; it is git-ignored.
4. Allow inbound TCP 80, 443, 3000 and 5000 in the EC2 security group. For normal domain traffic the site uses port 80; 3000 and 5000 are available for direct access as requested. Do not open SQL Server port 1433.
5. From the repository root run:

   ```sh
   docker compose up -d --build
   ```

Nginx serves `pulseleef.com` on port 80, sends `/api`, `/images`, and `/uploads` to the backend, and serves other paths from the React app. Direct ports are `http://43.205.42.69:3000` (frontend) and `http://43.205.42.69:5000` (backend).

The SQL Server container creates the requested `cmsdb` database and `cmsdbuser` login on first initialization. EF Core migrations run when the production backend starts. Persistent Docker volumes hold database files, uploads, and the ASP.NET Data Protection key ring. Back up these volumes; losing the key ring prevents decrypting stored Razorpay credentials.

The supplied `CMSDBscript.sql` is a Windows SQL Server export with hard-coded Windows data-file paths and creates a database named `CMSDB`; it is not run by this setup. This deployment starts a fresh `cmsdb` schema from the backend's EF migrations. Restore existing business data separately using a compatible SQL Server backup/export.

HTTPS requires a certificate for the domain. After DNS resolves and port 80 is reachable, install a Let's Encrypt certificate and add a TLS server block to `nginx/default.conf` before exposing port 443. The current Nginx configuration serves HTTP only.

To inspect services: `docker compose ps` and `docker compose logs -f backend`.
