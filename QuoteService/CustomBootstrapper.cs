﻿using System;
using Autofac;
using FCMAPI;
using Microsoft.Extensions.Configuration;
using Nancy;
using Nancy.Bootstrappers.Autofac;
using Nancy.Configuration;
using Serilog;
using Serilog.Events;
using SKAPI;

namespace QuoteService
{
    public class CustomBootstrapper : AutofacNancyBootstrapper
    {
        protected ILogger _logger;

        public CustomBootstrapper(ILogger logger)
        {
            _logger = logger;
            _logger.Debug("[CustomBootstrapper.ctor]: ctor");
        }

        protected override void ConfigureApplicationContainer(ILifetimeScope existingContainer)
        {
            base.ConfigureApplicationContainer(existingContainer);
            // Register logger
            existingContainer.Update(builder => builder.RegisterInstance(_logger).As<ILogger>());
            var log = existingContainer.Resolve<ILogger>();
            log.Debug("[CustomBootstrapper.ConfigureApplicationContainer]: Register logger completed.");

            // Register configuration
            log.Debug("[CustomBootstrapper.ConfigureApplicationContainer]: Register configuration...");
            existingContainer.Update(builder => builder.RegisterInstance(LoadConfiguration()).As<IConfiguration>());
            log.Debug("[CustomBootstrapper.ConfigureApplicationContainer]: Register configuration...done.");

            // Register SKAPISetting
            log.Debug("[CustomBootstrapper.ConfigureApplicationContainer]: Register SKAPI configuration...");
            existingContainer.Configure<SKAPISetting>(existingContainer.Resolve<IConfiguration>().GetSection("SKAPISetting"));
            log.Debug("[CustomBootstrapper.ConfigureApplicationContainer]: Register SKAPI configuration...done.");

            // Register IFCMAPIConnection
            log.Debug("[CustomBootstrapper.ConfigureApplicationContainer]: Register Futures Commission Merchant (FCM) API ...");
            
            existingContainer.Update(builder => builder.RegisterInstance(new SKAPIConnection(
                existingContainer.Resolve<ILogger>(),
                existingContainer.Resolve<SKAPISetting>()
            )).As<IFCMAPIConnection>());
            log.Debug("[CustomBootstrapper.ConfigureApplicationContainer]: Register Futures Commission Merchant (FCM) API ...done.");
        }

        private IConfiguration LoadConfiguration() => new ConfigurationBuilder().AddJsonFile("appsettings.json", optional: true).Build();

        // Unknown methods.
        protected override INancyEnvironmentConfigurator GetEnvironmentConfigurator()
        {
            return this.ApplicationContainer.Resolve<INancyEnvironmentConfigurator>();
        }
        public override INancyEnvironment GetEnvironment()
        {
            return this.ApplicationContainer.Resolve<INancyEnvironment>();
        }
        protected override void RegisterNancyEnvironment(ILifetimeScope container, INancyEnvironment environment)
        {
            container.Update(builder => builder.RegisterInstance(environment));
        }
    }
}