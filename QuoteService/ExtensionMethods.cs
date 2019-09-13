using System;
using System.IO;
using Autofac;
using Microsoft.Extensions.Configuration;
using Nancy.Bootstrappers.Autofac;
using Polly;
using Serilog;

namespace QuoteService
{
    public static class ExtensionMethods
    {
        public static void Configure<TOptions>(this ILifetimeScope container, IConfiguration config) where TOptions : class, new()
        {
            var options = new TOptions();
            config.Bind(options);
            container.Update(b => b.RegisterInstance(options));
        }
    }
}