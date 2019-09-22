using System;
using System.Threading;
using System.Threading.Tasks;
using Google.Cloud.PubSub.V1;
using Serilog;

namespace QuoteService.Queue
{
    public class GCPPubSubService : IQueueService
    {
        private string _projectID;
        private ILogger _logger;
        public GCPPubSubService(string projectID, ILogger logger)
        {
            _projectID = projectID;
            _logger = logger;
        }

        public async Task<IQueueFanoutConnection> GetQueueFanoutConnection()
        {
            return await Task<IQueueFanoutConnection>.Run(() => new GCPFanout(_projectID,_logger));
        }

        public async Task<IQueueRequestConnection> GetQueueRequestConnection()
        {
            throw new NotImplementedException();
        }
    }


    public class GCPFanout: IQueueFanoutConnection
    {
        private string _projectID;
        private string _topicID;
        private TopicName _topicName;
        private PublisherServiceApiClient _publisherService;
        private PublisherClient _publisher;
        private ILogger _logger;

        public GCPFanout(string projectID, ILogger logger)
        {
            _projectID = projectID;
            _logger = logger;
        }

        public void Dispose()
        {
            _logger.Debug("[GCPFanout.Dispose()] Delete topic: {_topicID}.",_topicID);
            _publisher.ShutdownAsync(TimeSpan.FromSeconds(15));
            _publisherService.DeleteTopic(_topicName);
        }

        public async Task InitTopic(string topicID)
        {
            await Task.Run(() =>
            {
                _topicID = topicID;
                _topicName = new TopicName(_projectID, _topicID);
                _publisherService = PublisherServiceApiClient.Create();

                try
                {
                    _publisherService.CreateTopic(_topicName);
                }
                catch (Grpc.Core.RpcException e) when (e.Status.StatusCode == Grpc.Core.StatusCode.AlreadyExists)
                {
                    //Console.WriteLine("Topic existed.");
                    _logger.Debug($"[GCPFanout.InitTopic()] Topic: {_topicID} existed");

                }
                _publisher = PublisherClient.CreateAsync(_topicName).Result;
            });
        }

        public  async Task Send(string message)
        {
            await Task.Run(() =>
            {
                _publisher.PublishAsync(message);
                //Thread.Sleep(TimeSpan.FromMilliseconds(1000));
            });
        }
    }
}