using AuthDemo.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace AuthDemo.Services;

/// <summary>
/// Serializes reconciliation of a single hospital settlement across application instances
/// using a SQL Server session-owned application lock.
///
/// IMPORTANT: Every reconciliation entry point must use ExecuteAsync with the same
/// hospital ID and gateway-settlement record ID. The operation should use its own
/// DbContext/transaction; this lock connection is deliberately kept separate.
/// </summary>
public sealed class HospitalSettlementReconciliationLockService
{
    private const int LockWaitMilliseconds = 30_000;
    private const int AcquireCommandTimeoutSeconds = 40;
    private const int ReleaseCommandTimeoutSeconds = 10;

    private readonly string _connectionString;

    public HospitalSettlementReconciliationLockService(AppDbContext db)
    {
        ArgumentNullException.ThrowIfNull(db);

        if (!db.Database.IsSqlServer())
            throw new NotSupportedException(
                "Hospital settlement reconciliation locking requires SQL Server.");

        _connectionString = db.Database.GetConnectionString()
            ?? throw new InvalidOperationException("SQL Server connection string is missing.");
    }

    public async Task<T> ExecuteAsync<T>(
        int hospitalId,
        long settlementRecordId,
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default)
    {
        if (hospitalId <= 0)
            throw new ArgumentOutOfRangeException(nameof(hospitalId));
        if (settlementRecordId <= 0)
            throw new ArgumentOutOfRangeException(nameof(settlementRecordId));
        ArgumentNullException.ThrowIfNull(operation);

        // SQL Server application locks are scoped to the current database.
        // Use the same ID type/meaning in every caller (gateway settlement row ID).
        string resource = $"HospitalSettlementReconciliation:{hospitalId}:{settlementRecordId}";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        bool acquired = false;
        Exception? operationError = null;

        try
        {
            await using (var acquire = connection.CreateCommand())
            {
                acquire.CommandText = "sp_getapplock";
                acquire.CommandType = CommandType.StoredProcedure;
                acquire.CommandTimeout = AcquireCommandTimeoutSeconds;
                acquire.Parameters.Add(new SqlParameter("@Resource", SqlDbType.NVarChar, 255)
                {
                    Value = resource
                });
                acquire.Parameters.Add(new SqlParameter("@LockMode", SqlDbType.VarChar, 32)
                {
                    Value = "Exclusive"
                });
                acquire.Parameters.Add(new SqlParameter("@LockOwner", SqlDbType.VarChar, 32)
                {
                    Value = "Session"
                });
                acquire.Parameters.Add(new SqlParameter("@LockTimeout", SqlDbType.Int)
                {
                    Value = LockWaitMilliseconds
                });
                acquire.Parameters.Add(new SqlParameter("@DbPrincipal", SqlDbType.NVarChar, 128)
                {
                    Value = "public"
                });

                var returnCode = new SqlParameter("@ReturnCode", SqlDbType.Int)
                {
                    Direction = ParameterDirection.ReturnValue
                };
                acquire.Parameters.Add(returnCode);

                await acquire.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                int code = Convert.ToInt32(returnCode.Value);

                if (code < 0)
                {
                    string reason = code switch
                    {
                        -1 => "timed out waiting for another reconciliation",
                        -2 => "lock request was cancelled",
                        -3 => "SQL Server selected the request as a deadlock victim",
                        -999 => "SQL Server rejected the lock request or its parameters",
                        _ => "SQL Server could not acquire the lock"
                    };

                    throw new InvalidOperationException(
                        $"Settlement reconciliation lock failed for hospital {hospitalId}, " +
                        $"settlement record {settlementRecordId}: {reason} (code {code}).");
                }

                acquired = true;
            }

            // A session lock is held only while this SQL connection remains open.
            // If the session is lost, do not silently treat the operation as protected.
            // Callers must keep financial writes transactional and idempotent.
            T result = await operation(cancellationToken).ConfigureAwait(false);

            if (connection.State != ConnectionState.Open)
                throw new InvalidOperationException(
                    "The SQL Server lock connection was lost during settlement reconciliation. " +
                    "Review the result before retrying.");

            return result;
        }
        catch (Exception ex)
        {
            operationError = ex;
            throw;
        }
        finally
        {
            if (acquired && connection.State == ConnectionState.Open)
            {
                try
                {
                    await using var release = connection.CreateCommand();
                    release.CommandText = "sp_releaseapplock";
                    release.CommandType = CommandType.StoredProcedure;
                    release.CommandTimeout = ReleaseCommandTimeoutSeconds;
                    release.Parameters.Add(new SqlParameter("@Resource", SqlDbType.NVarChar, 255)
                    {
                        Value = resource
                    });
                    release.Parameters.Add(new SqlParameter("@LockOwner", SqlDbType.VarChar, 32)
                    {
                        Value = "Session"
                    });
                    release.Parameters.Add(new SqlParameter("@DbPrincipal", SqlDbType.NVarChar, 128)
                    {
                        Value = "public"
                    });

                    var returnCode = new SqlParameter("@ReturnCode", SqlDbType.Int)
                    {
                        Direction = ParameterDirection.ReturnValue
                    };
                    release.Parameters.Add(returnCode);

                    await release.ExecuteNonQueryAsync(CancellationToken.None)
                        .ConfigureAwait(false);

                    if (Convert.ToInt32(returnCode.Value) < 0)
                        throw new InvalidOperationException(
                            "SQL Server reported failure releasing the settlement reconciliation lock.");
                }
                catch (Exception releaseError)
                {
                    // Do not return a potentially still-locked pooled session.
                    SqlConnection.ClearPool(connection);

                    // Preserve the original reconciliation failure, if any.
                    if (operationError is null)
                        throw new InvalidOperationException(
                            "Settlement reconciliation completed but lock release failed. " +
                            "Check reconciliation state before retrying.", releaseError);
                }
            }
            else if (acquired)
            {
                // A broken session releases session-owned locks on SQL Server,
                // but its loss makes the reconciliation outcome uncertain.
                SqlConnection.ClearPool(connection);
            }
        }
    }
}
