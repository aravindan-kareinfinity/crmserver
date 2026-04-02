using System.Data.Common;

namespace CRM.Server.Utils
{
    public interface IDb : System.IDisposable
    {
        System.Threading.Tasks.Task Connect();
        System.Threading.Tasks.Task Close();
        System.Threading.Tasks.Task BeginTransaction();
        System.Threading.Tasks.Task CommitTransaction();
        System.Threading.Tasks.Task RollbackTransaction();
        DbCommand GetCommand(string query);
        DbCommand GetCommand();
        DbParameter AddParameter(DbCommand command, string parameterName, DbTypes.Types type);
        System.Threading.Tasks.Task<DbDataReader> Execute(DbCommand command);
        System.Threading.Tasks.Task<int> ExecuteNonQuery(DbCommand command);
    }
}

