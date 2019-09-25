using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using RabbitMQ.Client;
using RabbitMQ.Client.Framing.Impl;
using Serilog;

namespace QuoteService.Queue.RabbitMQ
{
    public class RabbitQueueService:IQueueService
    {
        private RabbitPubSubSetting _setting;
        private ILogger _logger;
        public RabbitQueueService(ILogger logger, IConfiguration config)
        {
            _logger = logger;
            _setting = new RabbitPubSubSetting();
            config.GetSection("RabbitPubSubSetting").Bind(_setting);
        }
        public async Task<IQueueFanoutConnection> GetQueueFanoutConnection()
        {
            return await Task<IQueueFanoutConnection>.Run(() => new RabbitFanout(_logger,_setting));
        }
    }

    public class RabbitFanout : IQueueFanoutConnection
    {
        internal IConnection _conn;
        internal IModel _channel;
        private RabbitPubSubSetting _setting;
        private ILogger _logger;
        private string _topicName;

        public RabbitFanout(ILogger logger,RabbitPubSubSetting rabbitSetting)
        {
            _logger = logger;
            _setting = rabbitSetting;
            ConnectionFactory factory = new ConnectionFactory();
            factory.UserName = _setting.UserName;
            factory.Password = _setting.Password;
            factory.HostName = _setting.HostName;
            
            _conn = factory.CreateConnection();
            _channel = _conn.CreateModel();

        }

        public void Dispose()
        {
            _logger.Debug("[RabbitFanout.Dispose()] Delete topic: {_topicID}.", _topicName);
            _channel?.Dispose();
            _conn?.Dispose();
        }

        public async Task InitTopic(string topicID)
        {
            _topicName = topicID;
            _channel.ExchangeDeclare(topicID,"fanout");
        }

        public async Task Send(string message)
        {
            var body = Encoding.UTF8.GetBytes(message);
            _channel.BasicPublish(
                exchange: _topicName,
                routingKey: "",
                basicProperties: null,
                body: body
            );
        }
    }
}