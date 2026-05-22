using Serilog;
using Serilog.Events;

namespace OS.Infrastructure.Logging;

public static class SerilogConfig
{
    public static ILogger Configure(string logPath)
    {
        return new LoggerConfiguration()
            .MinimumLevel.Debug()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .WriteTo.File(
                logPath,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 30,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .WriteTo.Debug(outputTemplate: "[{Level}] {Message}{NewLine}{Exception}")
            .CreateLogger();
    }
}
