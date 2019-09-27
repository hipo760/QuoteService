using Microsoft.Extensions.Configuration;
using Nancy.Owin;
using Owin;
using Serilog;
using Serilog.Events;

namespace QuoteService
{
    public class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            var logger = ConfigureLogger();
            app.UseNancy(new NancyOptions()
            {
                Bootstrapper = new CustomBootstrapper(logger)
            });
        }
        private ILogger ConfigureLogger()
        {
            return new LoggerConfiguration()
                .MinimumLevel.Debug()
                .Enrich.FromLogContext()
                .Enrich.WithThreadId()
                .WriteTo.Console(
                    LogEventLevel.Verbose,
                    "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level}] ({ThreadId}) {Message}{NewLine}{Exception}")
                .WriteTo.RollingFile(
                    @"C:\QuoteService\bin\logs\QuoteService.log", 
                    LogEventLevel.Verbose,
                    "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level}] <{ThreadId}> {Message}{NewLine}{Exception}",
                    retainedFileCountLimit:7
                    )
                .CreateLogger();
        }
    }

}