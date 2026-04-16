using System;
using System.IO;
using Serilog;
using Serilog.Events;

namespace DataCrud.DBOps.Shared.Loggers
{
    public class LoggerConfig
    {

        public static void Register()
        {
            string logFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs\\");

            Log.Logger = new LoggerConfiguration()
                //.WriteTo.Console()
                .WriteTo.File(logFilePath, LogEventLevel.Information, rollingInterval: RollingInterval.Day)
                .CreateLogger();

        }

    }
}

