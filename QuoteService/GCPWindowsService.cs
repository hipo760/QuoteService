using System;
using System.Threading;
using Microsoft.Owin.Hosting;
using Topshelf.Logging;

namespace QuoteService
{
    public class GCPWindowsService
    {
        readonly LogWriter _log = HostLogger.Get<GCPWindowsService>();
        public void Start()
        {
            _log.Info("Starting GCPWindowsService ...");
            var url = "http://localhost:8080";
            WebApp.Start<Startup>(url);
            //using ()
            //{
            //    Console.WriteLine("Running on {0}", url);
            //    // Keep console open.
            //    // Thread.Sleep(Timeout.Infinite);
            //    // Console.WriteLine("Press enter to exit");
            //    // Console.ReadLine();
            //}
        }
        public void Stop()
        {
            _log.Info("Stopping GCPWindowsService ...");
            // write code here that runs when the Windows Service stops.  
        }
    }
}