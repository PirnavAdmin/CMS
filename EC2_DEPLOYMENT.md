# Deploy on the server

1. In your domain registrar's DNS panel, point the domain to this server:
   - `A` record, host `@`, value `43.205.42.69`
   - `A` record, host `www`, value `43.205.42.69`
   Remove conflicting A/AAAA records for these names. DNS changes can take time to propagate.
2. Copy the repository to the server. Install Docker Engine and the Docker Compose plugin. Nginx is installed and managed directly on the server; Compose does not run an Nginx container.
3. Create `.env` from `.env.example`. Set unique strong values for DB_PASSWORD, MSSQL_SA_PASSWORD and JWT_KEY (at least 32 random characters). Keep `.env` private; it is git-ignored.
4. Allow inbound TCP 80, 443, 3000 and 5000 in the EC2 security group. Port 80 serves the domain; ports 3000 and 5000 provide direct access as requested. Do not open SQL Server port 1433.
5. Start the app from the repository root:

   ```sh
   docker compose up -d --build
   ```

6. Install the host Nginx site config and reload Nginx:

   ```sh
   sudo cp nginx/pulseleef.com.conf /etc/nginx/sites-available/pulseleef.com
   sudo ln -s /etc/nginx/sites-available/pulseleef.com /etc/nginx/sites-enabled/pulseleef.com
   sudo nginx -t
   sudo systemctl reload nginx
   ```

Host Nginx proxies `/api`, `/images`, and `/uploads` to the backend on `127.0.0.1:5000` and other requests to the frontend on `127.0.0.1:3000`. Direct URLs are `http://43.205.42.69:3000` (frontend) and `http://43.205.42.69:5000` (backend).

The SQL Server container creates the requested `cmsdb` database and `cmsdbuser` login on first initialization. EF Core migrations run when the backend starts. Docker volumes persist the database files, uploads, and ASP.NET Data Protection keys. Back up these volumes; losing the key ring prevents decrypting stored Razorpay credentials.

The supplied `CMSDBscript.sql` has Windows-specific database file paths and creates a database named `CMSDB`, so it is not run by this setup. This deployment starts a fresh `cmsdb` schema from the backend's EF migrations. Restore existing business data separately from a compatible SQL Server backup/export.

The Nginx site config currently serves HTTP. After DNS resolves and port 80 is reachable, install a Let's Encrypt certificate (for example with Certbot's Nginx plugin) to enable HTTPS on the host.

Check app status with `docker compose ps` and logs with `docker compose logs -f backend`.
