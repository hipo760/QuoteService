using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Owin.Hosting;
using Nancy;
using Nancy.Hosting.Self;
using Serilog;
using Topshelf;

namespace QuoteService
{
    class Program
    {
        static void Main(string[] args)
        {
            //var url = "http://localhost:8080";

            //using (WebApp.Start<Startup>(url))
            //{
            //    Console.WriteLine("Running on {0}", url);
            //    // Keep console open.
            //    Thread.Sleep(Timeout.Infinite);
            //    //Console.WriteLine("Press enter to exit");
            //    //Console.ReadLine();
            //}
            HostFactory.Run(configure =>
            {
                configure.Service<GCPWindowsService>(service =>
                {
                    // here you can pass dependencies and configuration to the service
                    service.ConstructUsing(s => new GCPWindowsService());

                    service.WhenStarted(s => s.Start());
                    service.WhenStopped(s => s.Stop());
                });

                configure.StartAutomatically();
                configure.EnableServiceRecovery(r => r.RestartService(0));
                configure.RunAsLocalSystem();

                configure.SetServiceName("QuoteService");
                configure.SetDisplayName("QuoteService");
                configure.SetDescription("QuoteService by CapitalFCM.");

                configure.UseSerilog(CreateLogger());
            });

        }
        private static ILogger CreateLogger()
        {
            var logger = new LoggerConfiguration()
                .WriteTo.File("log.txt", Serilog.Events.LogEventLevel.Debug)
                .CreateLogger();
            return logger;
        }
    }

}
