using System;
using Autofac;
using Microsoft.Extensions.Configuration;
using Nancy;
using Nancy.Bootstrapper;
using Nancy.Bootstrappers.Autofac;
using Nancy.Configuration;
using QuoteService.FCMAPI;
using QuoteService.gRPC;
using QuoteService.Queue;
using QuoteService.QuoteData;
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

            // Register GCPPubSubSetting
            //log.Debug("[CustomBootstrapper.ConfigureApplicationContainer]: Register GCPPubSub configuration...");
            //existingContainer.Configure<GCPPubSubSetting>(existingContainer.Resolve<IConfiguration>().GetSection("GCPPubSubSetting"));
            //log.Debug("[CustomBootstrapper.ConfigureApplicationContainer]: Register GCPPubSub configuration...done.");


            // Register ConnectionStatusEvent broker.
            existingContainer.Update(builder =>
                builder.RegisterInstance(new DataEventBroker<ConnectionStatusEvent>())
                    .As<DataEventBroker<ConnectionStatusEvent>>());


            // Register IFCMAPIConnection
            log.Debug("[CustomBootstrapper.ConfigureApplicationContainer]: Register Futures Commission Merchant (FCM) API ...");
            existingContainer.Update(builder => builder.RegisterInstance(new SKAPIConnection(
                existingContainer.Resolve<ILogger>(),
                existingContainer.Resolve<DataEventBroker<ConnectionStatusEvent>>(),
                existingContainer.Resolve<IConfiguration>()
                //existingContainer.Resolve<GCPPubSubSetting>()
            )).As<IFCMAPIConnection>());
            log.Debug("[CustomBootstrapper.ConfigureApplicationContainer]: Register Futures Commission Merchant (FCM) API ...done.");

            // Register HealthAction
            existingContainer.Update(builder => builder.RegisterInstance(new HealthAction(
                existingContainer.Resolve<IFCMAPIConnection>(),
                existingContainer.Resolve<ILogger>(),
                existingContainer.Resolve<DataEventBroker<ConnectionStatusEvent>>()
                )));

            // Register gRPC
            existingContainer.Configure<QuoteActionGRPCServerSetting>(existingContainer.Resolve<IConfiguration>().GetSection("QuoteActionGRPCServerSetting"));
            existingContainer.Update(builder => builder.RegisterInstance(new QuoteActionServer(
                existingContainer.Resolve<IFCMAPIConnection>(),
                existingContainer.Resolve<QuoteActionGRPCServerSetting>(),
                existingContainer.Resolve<ILogger>()
                )));

            log.Debug("[CustomBootstrapper.ConfigureApplicationContainer]: Start grpc...");
            var grpc = existingContainer.Resolve<QuoteActionServer>();
            grpc.Start();

        }

        public static IConfiguration LoadConfiguration() => new ConfigurationBuilder().AddJsonFile("appsettings.json", optional: true).Build();

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