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
                .WriteTo.Console(
                    LogEventLevel.Verbose,
                    "{Timestamp:HH:mm:ss} [{Level}] ({CorrelationToken}) {Message}{NewLine}{Exception}")
            //"{NewLine}{Timestamp:HH:mm:ss} [{Level}] ({CorrelationToken}) {Message}{NewLine}{Exception}")
                .CreateLogger();
        }

    }

}