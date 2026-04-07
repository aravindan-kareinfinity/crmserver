using System.Data.Common;
using Npgsql;
using NpgsqlTypes;

namespace CRM.Server.Utils
{
    public class PostgreSQL : IDb
    {
        private readonly NpgsqlDataSource dataSource;
        private NpgsqlConnection? connection;
        private NpgsqlTransaction? transaction;

        public PostgreSQL(NpgsqlDataSource dataSource)
        {
            this.dataSource = dataSource;
        }

        public async System.Threading.Tasks.Task Connect()
        {
            if (connection == null)
                connection = await dataSource.OpenConnectionAsync();
            if (connection.State != System.Data.ConnectionState.Open)
                await connection.OpenAsync();
        }

        public System.Threading.Tasks.Task Close()
        {
            if (connection != null)
                connection.Close();
            return System.Threading.Tasks.Task.CompletedTask;
        }

        public async System.Threading.Tasks.Task BeginTransaction()
        {
            if (connection == null)
                throw new InvalidOperationException("Connection not opened. Call Connect() first.");
            if (transaction != null)
                return;
            transaction = await connection.BeginTransactionAsync();
        }

        public async System.Threading.Tasks.Task CommitTransaction()
        {
            if (transaction == null)
                throw new InvalidOperationException("Transaction not started.");
            await transaction.CommitAsync();
        }

        public async System.Threading.Tasks.Task RollbackTransaction()
        {
            if (transaction == null)
                throw new InvalidOperationException("Transaction not started.");
            await transaction.RollbackAsync();
        }

        public DbCommand GetCommand(string query)
        {
            if (connection == null)
                throw new InvalidOperationException("Connection not opened. Call Connect() first.");

            return transaction == null
                ? new NpgsqlCommand(query, connection)
                : new NpgsqlCommand(query, connection, transaction);
        }

        public DbCommand GetCommand()
        {
            if (connection == null)
                throw new InvalidOperationException("Connection not opened. Call Connect() first.");

            var cmd = new NpgsqlCommand();
            cmd.Connection = connection;
            if (transaction != null)
                cmd.Transaction = transaction;
            return cmd;
        }

        public DbParameter AddParameter(DbCommand command, string parameterName, DbTypes.Types type)
        {
            if (command is not NpgsqlCommand npgCmd)
                throw new InvalidOperationException("Expected NpgsqlCommand.");

            NpgsqlParameter parameter = type switch
            {
                DbTypes.Types.String => new NpgsqlParameter(parameterName, NpgsqlTypes.NpgsqlDbType.Varchar),
                DbTypes.Types.Date => new NpgsqlParameter(parameterName, NpgsqlTypes.NpgsqlDbType.Date),
                DbTypes.Types.DateTime => new NpgsqlParameter(parameterName, NpgsqlTypes.NpgsqlDbType.Timestamp),
                DbTypes.Types.Integer => new NpgsqlParameter(parameterName, NpgsqlTypes.NpgsqlDbType.Integer),
                DbTypes.Types.Long => new NpgsqlParameter(parameterName, NpgsqlTypes.NpgsqlDbType.Bigint),
                DbTypes.Types.Decimal => new NpgsqlParameter(parameterName, NpgsqlTypes.NpgsqlDbType.Numeric),
                DbTypes.Types.Boolean => new NpgsqlParameter(parameterName, NpgsqlTypes.NpgsqlDbType.Boolean),
                DbTypes.Types.Json => new NpgsqlParameter(parameterName, NpgsqlTypes.NpgsqlDbType.Json),
                DbTypes.Types.ByteArray => new NpgsqlParameter(parameterName, NpgsqlTypes.NpgsqlDbType.Bytea),
                DbTypes.Types.DateTimeOffset => new NpgsqlParameter(parameterName, NpgsqlTypes.NpgsqlDbType.TimestampTz),
                _ => new NpgsqlParameter(parameterName, null),
            };

            npgCmd.Parameters.Add(parameter);
            return parameter;
        }

        public async System.Threading.Tasks.Task<DbDataReader> Execute(DbCommand command)
        {
            if (command is not NpgsqlCommand npgCmd)
                throw new InvalidOperationException("Expected NpgsqlCommand.");

            NormalizeDateTimes(npgCmd);
            return await npgCmd.ExecuteReaderAsync();
        }

        public async System.Threading.Tasks.Task<int> ExecuteNonQuery(DbCommand command)
        {
            if (command is not NpgsqlCommand npgCmd)
                throw new InvalidOperationException("Expected NpgsqlCommand.");

            NormalizeDateTimes(npgCmd);
            return await npgCmd.ExecuteNonQueryAsync();
        }

        private static void NormalizeDateTimes(NpgsqlCommand cmd)
        {
            foreach (var pObj in cmd.Parameters)
            {
                if (pObj is not NpgsqlParameter p || p.Value is null || p.Value is DBNull)
                    continue;

                // Npgsql (7+) disallows writing Kind=UTC to timestamp without time zone.
                if (p.NpgsqlDbType == NpgsqlDbType.Timestamp && p.Value is DateTime dt && dt.Kind != DateTimeKind.Unspecified)
                {
                    p.Value = DateTime.SpecifyKind(dt, DateTimeKind.Unspecified);
                    continue;
                }

                // timestamptz requires UTC DateTime (or DateTimeOffset).
                if (p.NpgsqlDbType == NpgsqlDbType.TimestampTz && p.Value is DateTime dtTz && dtTz.Kind != DateTimeKind.Utc)
                {
                    p.Value = dtTz.Kind == DateTimeKind.Unspecified
                        ? DateTime.SpecifyKind(dtTz, DateTimeKind.Utc)
                        : dtTz.ToUniversalTime();
                }
            }
        }

        public void Dispose()
        {
            try
            {
                transaction?.Dispose();
                if (connection != null)
                {
                    connection.Dispose();
                    connection = null;
                }
            }
            catch
            {
                // Ignore dispose exceptions to avoid masking original failures.
            }
        }
    }
}

