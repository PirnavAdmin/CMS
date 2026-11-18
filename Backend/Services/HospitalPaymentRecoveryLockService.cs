
using System;
using System.Data;
using System.Data.Common;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AuthDemo.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace AuthDemo.Services;

/// <summary>
/// Provides a SQL Server transaction-scoped application lock
/// for a hospital payment recovery operation.
///
/// The lock is released when its transaction commits
/// or rolls back.
/// </summary>
public sealed class HospitalPaymentRecoveryLockService
{
    private readonly AppDbContext _db;

    public HospitalPaymentRecoveryLockService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<PaymentRecoveryLockHandle?> TryAcquireAsync(
        int hospitalId,
        string gatewayPaymentId,
        CancellationToken cancellationToken = default)
    {
        if (hospitalId <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(hospitalId));
        }

        if (string.IsNullOrWhiteSpace(gatewayPaymentId))
        {
            throw new ArgumentException(
                "Gateway payment ID is required.",
                nameof(gatewayPaymentId));
        }

        gatewayPaymentId = gatewayPaymentId.Trim();

        if (gatewayPaymentId.Length > 150)
        {
            throw new ArgumentException(
                "Gateway payment ID cannot exceed 150 characters.",
                nameof(gatewayPaymentId));
        }

        // Avoid opening a nested transaction.
        if (_db.Database.CurrentTransaction != null)
        {
            throw new InvalidOperationException(
                "A database transaction is already active. " +
                "Acquire the payment recovery lock before " +
                "starting another transaction.");
        }

        // Hash the payment ID to produce a bounded,
        // consistent SQL application-lock resource.
        var resource = CreateLockResource(
            hospitalId,
            gatewayPaymentId);

        var transaction = await _db.Database
            .BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

        try
        {
            var connection = _db.Database.GetDbConnection();

            await using var command = connection.CreateCommand();

            command.Transaction = transaction.GetDbTransaction();

            command.CommandType = CommandType.Text;

            command.CommandText = @"
DECLARE @result INT;

EXEC @result = sys.sp_getapplock
    @Resource = @resource,
    @LockMode = 'Exclusive',
    @LockOwner = 'Transaction',
    @LockTimeout = 0;

SELECT @result;";

            var parameter = command.CreateParameter();

            parameter.ParameterName = "@resource";
            parameter.DbType = DbType.String;
            parameter.Size = 255;
            parameter.Value = resource;

            command.Parameters.Add(parameter);

            var result = await command.ExecuteScalarAsync(
                cancellationToken);

            if (result == null || result == DBNull.Value)
            {
                throw new InvalidOperationException(
                    "SQL Server did not return a lock result.");
            }

            var lockResult = Convert.ToInt32(result);

            // sp_getapplock returns >= 0 on success.
            // -1 is a lock timeout (another worker owns it).
            // Other negative values represent errors.
            if (lockResult == -1)
            {
                await transaction.RollbackAsync(
                    CancellationToken.None);

                await transaction.DisposeAsync();

                return null;
            }

            if (lockResult < 0)
            {
                throw new InvalidOperationException(
                    $"SQL Server application lock failed " +
                    $"with code {lockResult}.");
            }

            return new PaymentRecoveryLockHandle(
                transaction);
        }
        catch
        {
            try
            {
                await transaction.RollbackAsync(
                    CancellationToken.None);
            }
            finally
            {
                await transaction.DisposeAsync();
            }

            throw;
        }
    }

    private static string CreateLockResource(
        int hospitalId,
        string gatewayPaymentId)
    {
        var bytes = SHA256.HashData(
            Encoding.UTF8.GetBytes(gatewayPaymentId));

        var hash = Convert.ToHexString(bytes);

        return $"HospitalPaymentRecovery:v1:{hospitalId}:{hash}";
    }
}

/// <summary>
/// Owns a database transaction and its SQL application lock.
/// Disposing without committing rolls the transaction back.
/// </summary>
public sealed class PaymentRecoveryLockHandle : IAsyncDisposable
{
    private readonly IDbContextTransaction _transaction;

    private bool _completed;
    private bool _disposed;

    internal PaymentRecoveryLockHandle(
        IDbContextTransaction transaction)
    {
        _transaction = transaction;
    }

    public async Task CommitAsync(
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (_completed)
        {
            throw new InvalidOperationException(
                "Transaction has already been completed.");
        }

        await _transaction.CommitAsync(
            cancellationToken);

        _completed = true;
    }

    public async Task RollbackAsync(
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (_completed)
        {
            throw new InvalidOperationException(
                "Transaction has already been completed.");
        }

        await _transaction.RollbackAsync(
            cancellationToken);

        _completed = true;
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(
                nameof(PaymentRecoveryLockHandle));
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;

        try
        {
            if (!_completed)
            {
                await _transaction.RollbackAsync(
                    CancellationToken.None);
            }
        }
        finally
        {
            await _transaction.DisposeAsync();
        }
    }
}
