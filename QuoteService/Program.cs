using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Owin.Hosting;
using Nancy;
using Nancy.Hosting.Self;

namespace QuoteService
{
    class Program
    {
        static void Main(string[] args)
        {
            var url = "http://localhost:8080";

            using (WebApp.Start<Startup>(url))
            {
                Console.WriteLine("Running on {0}", url);
                // Keep console open.
                Thread.Sleep(Timeout.Infinite);
                //Console.WriteLine("Press enter to exit");
                //Console.ReadLine();
            }
        }   
    }
}
