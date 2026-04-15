using System;
using System.Collections.Generic;
using DataCrud.DBOps.Shared.Models;
using Microsoft.SqlServer.Management.Smo;

namespace DataCrud.DBOps.Shared
{
    public class DatabaseProvider
    {
        private static readonly List<DatabaseConnector> DatabaseConnections = new List<DatabaseConnector>();

        private static bool IgnoreSystemDb(Database database)
        {
            if (database.IsSystemObject) return true;
            return false;
        }


        public static List<Database> GetDatabasesToBackup(Server server)
        {
            Console.WriteLine("Collecting databases to take backups...");

            List<Database> databases = new List<Database>();


            var backupAllDatabases = AppSettings.BackupAllDatabases;

            if (backupAllDatabases)
            {
                foreach (Database database in server.Databases)
                {
                    if (IgnoreSystemDb(database)) continue;

                    databases.Add(new Database { Name = database.Name });
                }
                
            }
            else
            {
                var backupDatabases = AppSettings.BackupDatabases;
                var databaseNames = backupDatabases.Split(',');

                foreach (var databaseName in databaseNames)
                {
                    databases.Add(new Database { Name = databaseName });
                }
                
            }
            

            return databases;
        }

       

        public static List<DatabaseConnector> GetDatabaseConnections(Server server)
        {
            Console.WriteLine("Collecting database connections...");

            DatabaseConnections.Clear();

            var backupAllDatabases = AppSettings.BackupAllDatabases;

            if (backupAllDatabases)
            {
                foreach (Database database in server.Databases)
                {
                    if (IgnoreSystemDb(database)) continue;

                    var databaseServer = BuildConnectionString(server, database.Name);

                    DatabaseConnections.Add(databaseServer);
                }
            }
            else
            {
                var backupDatabases = AppSettings.BackupDatabases;
                var databaseNames = backupDatabases.Split(',');

                foreach (var databaseName in databaseNames)
                {
                    var databaseServer = BuildConnectionString(server, databaseName);

                    DatabaseConnections.Add(databaseServer);
                }

            }
            

            return DatabaseConnections;
        }

        private static DatabaseConnector BuildConnectionString(Server server, string databaseName)
        {

            return new DatabaseConnector(server.Name, databaseName);

            //return $"Data Source={server.Name};Initial Catalog={databaseName};Integrated Security=true;";
        }

    }
}
