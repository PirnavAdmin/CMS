#!/usr/bin/env bash
set -euo pipefail
/opt/mssql/bin/sqlservr &
SQL_PID=$!
trap 'kill "$SQL_PID"; wait "$SQL_PID"' SIGTERM SIGINT
for i in $(seq 1 90); do
  if /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -C -Q "SELECT 1" >/dev/null 2>&1; then break; fi
  sleep 2
done
/opt/mssql-tools18/bin/sqlcmd -b -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -C \
  -v DbName="$DB_NAME" DbUser="$DB_USER" DbPassword="$DB_PASSWORD" \
  -i /usr/local/bin/init-db.sql
wait "$SQL_PID"
