using AuthDemo.Data;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;

namespace AuthDemo.Services;

/// <summary>Serializes a hospital settlement operation with a MySQL named lock.</summary>
public sealed class HospitalSettlementReconciliationLockService
{
    private readonly string _connectionString;

    public HospitalSettlementReconciliationLockService(AppDbContext db)
    {
        _connectionString = db.Database.GetConnectionString()
            ?? throw new InvalidOperationException("MySQL connection string is missing.");
    }

    public async Task<T> ExecuteAsync<T>(
        int hospitalId,
        long settlementRecordId,
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default)
    {
        if (hospitalId <= 0) throw new ArgumentOutOfRangeException(nameof(hospitalId));
        if (settlementRecordId <= 0) throw new ArgumentOutOfRangeException(nameof(settlementRecordId));
        ArgumentNullException.ThrowIfNull(operation);

        var resource = $"HospitalSettlementReconciliation:{hospitalId}:{settlementRecordId}";
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using (var acquire = new MySqlCommand("SELECT GET_LOCK(@resource, 30)", connection))
        {
            acquire.Parameters.AddWithValue("@resource", resource);
            var result = await acquire.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            if (Convert.ToInt32(result) != 1)
                throw new InvalidOperationException($"Could not acquire settlement lock '{resource}'.");
        }

        Exception? operationError = null;
        try
        {
            var result = await operation(cancellationToken).ConfigureAwait(false);
            if (connection.State != System.Data.ConnectionState.Open)
                throw new InvalidOperationException("MySQL lock connection was lost during settlement reconciliation.");
            return result;
        }
        catch (Exception ex)
        {
            operationError = ex;
            throw;
        }
        finally
        {
            if (connection.State == System.Data.ConnectionState.Open)
            {
                try
                {
                    await using var release = new MySqlCommand("SELECT RELEASE_LOCK(@resource)", connection);
                    release.Parameters.AddWithValue("@resource", resource);
                    await release.ExecuteScalarAsync(CancellationToken.None).ConfigureAwait(false);
                }
                catch when (operationError is not null)
                {
                    // Preserve the original reconciliation error.
                }
            }
        }
    }
}
