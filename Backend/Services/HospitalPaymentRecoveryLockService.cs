
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
/// Uses a MySQL session named lock together with a database transaction
/// for a hospital payment recovery operation.
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

        // Hash the payment ID to produce a bounded MySQL lock name.
        var resource = CreateLockResource(
            hospitalId,
            gatewayPaymentId);

        var connection = _db.Database.GetDbConnection();
        await _db.Database.OpenConnectionAsync(cancellationToken);
        var lockAcquired = false;
        IDbContextTransaction? transaction = null;

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandType = CommandType.Text;
            command.CommandText = "SELECT GET_LOCK(@resource, 0)";

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
                    "MySQL did not return a lock result.");
            }

            lockAcquired = Convert.ToInt32(result) == 1;
            if (!lockAcquired)
            {
                await _db.Database.CloseConnectionAsync();
                return null;
            }

            transaction = await _db.Database.BeginTransactionAsync(
                IsolationLevel.Serializable, cancellationToken);

            return new PaymentRecoveryLockHandle(
                transaction, connection, resource, _db.Database);
        }
        catch
        {
            if (transaction != null)
            {
                try { await transaction.RollbackAsync(CancellationToken.None); }
                finally { await transaction.DisposeAsync(); }
            }
            if (lockAcquired && connection.State == ConnectionState.Open)
            {
                await using var release = connection.CreateCommand();
                release.CommandText = "SELECT RELEASE_LOCK(@resource)";
                var parameter = release.CreateParameter();
                parameter.ParameterName = "@resource";
                parameter.Value = resource;
                release.Parameters.Add(parameter);
                await release.ExecuteScalarAsync(CancellationToken.None);
            }
            if (connection.State == ConnectionState.Open)
            {
                await _db.Database.CloseConnectionAsync();
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
/// Owns a database transaction and MySQL session lock.
/// Disposing without committing rolls the transaction back.
/// </summary>
public sealed class PaymentRecoveryLockHandle : IAsyncDisposable
{
    private readonly IDbContextTransaction _transaction;
    private readonly System.Data.Common.DbConnection _connection;
    private readonly string _resource;
    private readonly DatabaseFacade _database;

    private bool _completed;
    private bool _disposed;

    internal PaymentRecoveryLockHandle(
        IDbContextTransaction transaction,
        System.Data.Common.DbConnection connection,
        string resource,
        DatabaseFacade database)
    {
        _transaction = transaction;
        _connection = connection;
        _resource = resource;
        _database = database;
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
            try
            {
                if (_connection.State == ConnectionState.Open)
                {
                    await using var release = _connection.CreateCommand();
                    release.CommandText = "SELECT RELEASE_LOCK(@resource)";
                    var parameter = release.CreateParameter();
                    parameter.ParameterName = "@resource";
                    parameter.Value = _resource;
                    release.Parameters.Add(parameter);
                    await release.ExecuteScalarAsync(CancellationToken.None);
                }
            }
            finally
            {
                await _transaction.DisposeAsync();
                await _database.CloseConnectionAsync();
            }
        }
    }
}
