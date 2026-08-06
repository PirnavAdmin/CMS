# EC2 Docker Deployment

The deployment is configured for Elastic IP `3.7.108.129`.

- `frontend/.env`
- `frontend/src/config/api.jsx`

Use the requested ports:

- Frontend: `http://3.7.108.129:3000`
- Backend: `http://3.7.108.129:5000`

Update production secrets before the first deploy:

- `backend/.env`

The backend database connection currently uses:

- Database: `ClinicalManagementSystemDB`
- User: `sa`
- Server: `3.7.108.129,1433`

Start the stack from the repository root:

```sh
docker compose up -d --build
```

Check logs:

```sh
docker compose logs -f backend
docker compose logs -f frontend
docker compose logs -f db
```

The backend runs migrations on startup. Keep port `3000` and port `5000` open in the EC2 security group.
