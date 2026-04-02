namespace CRM.Server.Utils
{
    public interface IDbProvider
    {
        System.Threading.Tasks.Task<IDb> GetDb(string? connectionString = null);
    }
}

