using System;
using Autofac;
using Grpc.Core;
using Microsoft.Extensions.Configuration;
using Nancy;
using Nancy.Bootstrapper;
using Nancy.Bootstrappers.Autofac;
using Nancy.Configuration;
using QuoteResearch.HealthCheck;
using QuoteResearch.Service.ScheduleService;
using QuoteService.FCMAPI;
using QuoteService.gRPC;
using QuoteService.Queue;
using QuoteService.Queue.RabbitMQ;
using QuoteService.QuoteData;
using QuoteService.Schedule;
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

            // Register ConnectionStatusEvent broker.
            existingContainer.Update(builder =>
                builder.RegisterInstance(new DataEventBroker<ConnectionStatusEvent>())
                    .As<DataEventBroker<ConnectionStatusEvent>>());

            // Register SKWrapper
            existingContainer.Update(b => b.RegisterInstance(new SkapiWrapper()).As<SkapiWrapper>());
            var skWrapper = existingContainer.Resolve<SkapiWrapper>();
            skWrapper.InitSkcomLib();

            // Register IFCMAPIConnection
            log.Debug("[CustomBootstrapper.ConfigureApplicationContainer]: Register Futures Commission Merchant (FCM) API ...");
            existingContainer.Update(builder => builder.RegisterInstance(new SKAPIConnection(
                existingContainer.Resolve<ILogger>(),
                skWrapper,
                existingContainer.Resolve<DataEventBroker<ConnectionStatusEvent>>(),
                existingContainer.Resolve<IConfiguration>()
                //existingContainer.Resolve<GCPPubSubSetting>()
            )).As<IFCMAPIConnection>());
            existingContainer.Resolve<IFCMAPIConnection>().InitAPI();
            log.Debug("[CustomBootstrapper.ConfigureApplicationContainer]: Register Futures Commission Merchant (FCM) API ...done.");

            // Register HealthAction
            log.Debug("[CustomBootstrapper.ConfigureApplicationContainer]: Register HealthAction ...");
            existingContainer.Update(builder => builder.RegisterInstance(new HealthAction(
                existingContainer.Resolve<IFCMAPIConnection>(),
                existingContainer.Resolve<ILogger>(),
                existingContainer.Resolve<DataEventBroker<ConnectionStatusEvent>>()
                )));
            log.Debug("[CustomBootstrapper.ConfigureApplicationContainer]: Register HealthAction ...done.");

            // Register ScheduleQueue
            log.Debug("[CustomBootstrapper.ConfigureApplicationContainer]: Register ScheduleServiceClientAction ...");
            existingContainer.Configure<QuoteScheduleSetting>(existingContainer.Resolve<IConfiguration>().GetSection("QuoteScheduleSetting"));

            var quoteScheduleSetting = existingContainer.Resolve<QuoteScheduleSetting>();

            existingContainer.Update(b => b.RegisterInstance(new Channel(quoteScheduleSetting.ScheduleService, ChannelCredentials.Insecure)));
            existingContainer.Update(b => b.RegisterInstance(new ScheduleService.ScheduleServiceClient(
                existingContainer.Resolve<Channel>()
                )));
            existingContainer.Update(b => b.RegisterInstance(new ScheduleService.ScheduleServiceClient(
                existingContainer.Resolve<Channel>()
            )));
            existingContainer.Update(b => b.RegisterInstance(new Health.HealthClient(
                existingContainer.Resolve<Channel>()
            )));

            existingContainer.Update(b => b.RegisterInstance(new ScheduleServiceClientAction(
            existingContainer.Resolve<ILogger>(),
            existingContainer.Resolve<ScheduleService.ScheduleServiceClient>(),
            existingContainer.Resolve<Health.HealthClient>()
                )));
            log.Debug("[CustomBootstrapper.ConfigureApplicationContainer]: Register ScheduleServiceClientAction ...done.");

            log.Debug("[CustomBootstrapper.ConfigureApplicationContainer]: Register QuoteScheduleQueue ...");
            existingContainer.Update(builder =>
                builder.RegisterInstance(new QueueConnectionClient(
                    new RabbitQueueService(log, existingContainer.Resolve<IConfiguration>())
                    )));
            var queueConnectionClient = existingContainer.Resolve<QueueConnectionClient>();
            queueConnectionClient.FanoutReceiver.InitListening(quoteScheduleSetting.ScheduleTopic);
            log.Debug("[CustomBootstrapper.ConfigureApplicationContainer]: Register QuoteScheduleQueue ...done.");

            existingContainer.Update(builder => builder.RegisterInstance(new QuoteServiceSchedule(
                existingContainer.Resolve<IFCMAPIConnection>(),
                existingContainer.Resolve<ILogger>(),
                existingContainer.Resolve<ScheduleServiceClientAction>(),
                existingContainer.Resolve<QueueConnectionClient>()
            )));


            // Register QuoteService
            existingContainer.Configure<QuoteActionGRPCServerSetting>(existingContainer.Resolve<IConfiguration>().GetSection("QuoteActionGRPCServerSetting"));
            existingContainer.Update(builder => builder.RegisterInstance(new GrpcServer(
                existingContainer.Resolve<IFCMAPIConnection>(),
                existingContainer.Resolve<QuoteActionGRPCServerSetting>(),
                existingContainer.Resolve<ILogger>()
                )));

            log.Debug("[CustomBootstrapper.ConfigureApplicationContainer]: Start grpc...");
            var grpc = existingContainer.Resolve<GrpcServer>();
            grpc.Start();
            log.Debug("[CustomBootstrapper.ConfigureApplicationContainer]: DI complete.");
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