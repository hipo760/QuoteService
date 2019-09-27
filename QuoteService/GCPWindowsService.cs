using System;
using System.Threading;
using Microsoft.Extensions.Configuration;
using Microsoft.Owin.Hosting;
using Topshelf.Logging;

namespace QuoteService
{
    public class SelfHostSetting
    {
        public string Host { get; set; }
    }


    public class GCPWindowsService
    {
        readonly LogWriter _log = HostLogger.Get<GCPWindowsService>();
        private SelfHostSetting _setting = new SelfHostSetting();

        public void Start()
        {
            _log.Info("[GCPWindowsService.Start()] Load Config ...");
            IConfiguration config = CustomBootstrapper.LoadConfiguration();
            config.GetSection("SelfHostSetting").Bind(_setting);
            _log.Info($"[GCPWindowsService.Start()] Start at {_setting.Host} ...");
            WebApp.Start<Startup>(_setting.Host);
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