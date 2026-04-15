namespace DataCrud.DBOps.Shared.Models
{
    public class DatabaseConnector
    {
        public DatabaseConnector(string serverName, string databaseName)
        {
            ServerName = serverName;
            DatabaseName = databaseName;
        }

        public string ServerName { get; set; }
        public string DatabaseName { get; set; }
        public string ConnectionString => $"Data Source={ServerName};Initial Catalog={DatabaseName};Integrated Security=true;";
    }
}

