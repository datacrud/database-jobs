using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using Dapper;
using DataCrud.DBOps.Shared;
using DataCrud.DBOps.Shared.Models;

namespace DataCrud.DBOps.Maintenance
{
    public static class IndexOrganizer
    {
        private const string SpReorganize = @"Exec sp_msforeachtable 'ALTER INDEX ALL ON ? Reorganize'";
        private const string SpRebuild = @"Exec sp_msforeachtable 'SET QUOTED_IDENTIFIER ON; ALTER INDEX ALL ON ? REBUILD'";

        public static void  Reorganize(List<DatabaseConnector> databaseConnections)
        {
            if(!AppSettings.EnableIndexMaintenance) return;

            Console.WriteLine("Starting index reorganize...");

            foreach (var databaseServer in databaseConnections)
            {
                using (var con = new SqlConnection(databaseServer.ConnectionString))
                {
                    Console.WriteLine($"Reorganizing...{databaseServer.ConnectionString}");

                    con.Query(SpReorganize, commandTimeout: 24 * 60 * 60);
                }
            }
        }
        
        
        public static void Rebuild(List<DatabaseConnector> databaseConnections)
        {
            if (!AppSettings.EnableIndexMaintenance) return;

            Console.WriteLine("Starting index rebuild...");

            foreach (var databaseServer in databaseConnections)
            {
                using (var con = new SqlConnection(databaseServer.ConnectionString))
                {
                    Console.WriteLine($"Rebuilding...{databaseServer.ConnectionString}");

                    con.Query(SpRebuild, commandTimeout: 24 * 60 * 60);
                }
            }
        }
    }
}

