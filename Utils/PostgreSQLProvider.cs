using Npgsql;

namespace CRM.Server.Utils
{
    public class PostgreSQLProvider : IDbProvider
    {
        private readonly NpgsqlDataSource dataSource;

        public PostgreSQLProvider(NpgsqlDataSource dataSource)
        {
            this.dataSource = dataSource;
        }

        public async System.Threading.Tasks.Task<IDb> GetDb(string? connectionString = null)
        {
            // Uses the shared NpgsqlDataSource; connectionString override is not supported in this refactor.
            return new PostgreSQL(dataSource);
        }
    }
}

