using System;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
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
        public async Task<IQueueFanoutPublisher> GetQueueFanoutPublisher()
        {
            return await Task.Run(() => new RabbitFanoutPublisher(_logger,_setting));
        }
    }

    public class RabbitFanoutPublisher : IQueueFanoutPublisher
    {
        internal IConnection _conn;
        internal IModel _channel;
        private RabbitPubSubSetting _setting;
        private ILogger _logger;
        private string _topicName;

        public RabbitFanoutPublisher(ILogger logger,RabbitPubSubSetting rabbitSetting)
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
            _logger.Debug("[RabbitFanoutPublisher.Dispose()] Delete topic: {_topicID}.", _topicName);
            _channel?.Dispose();
            _conn?.Dispose();
        }

        public async Task InitTopic(string topicID)
        {
            _topicName = topicID;
            await Task.Run(() => { _channel.ExchangeDeclare(topicID, "fanout"); });
        }

        public async Task Send(byte[] message)
        {
            //var body = Encoding.UTF8.GetBytes(message);
            await Task.Run(() =>
            {
                _channel.BasicPublish(
                    exchange: _topicName,
                    routingKey: "",
                    basicProperties: null,
                    body: message
                );
            });
            
        }
    }

    public class RabbitFanoutReceiver : IQueueFanoutReceiver
    {
        internal IConnection _conn;
        internal IModel _channel;
        private RabbitPubSubSetting _setting;
        private ILogger _logger;
        private string _topicName;
        private EventingBasicConsumer _consumer;

        public RabbitFanoutReceiver(ILogger logger, RabbitPubSubSetting rabbitSetting)
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
            _channel?.Dispose();
            _conn?.Dispose();
        }
        
        public event EventHandler<byte[]> ReceivedMessageEvent;

        public async Task InitListening(string topicId)
        {
            await Task.Run(() =>
            {
                _topicName = topicId;
                _channel.ExchangeDeclare(exchange: _topicName, type: "fanout");

                var queueName = _channel.QueueDeclare().QueueName;
                _channel.QueueBind(queue: queueName, exchange: _topicName, routingKey: "");
                _consumer = new EventingBasicConsumer(_channel);
                _consumer.Received += Consumer_Received;
            });
        }

        private void Consumer_Received(object sender, BasicDeliverEventArgs e)
        {
            //var message = Encoding.UTF8.GetString(e.Body);
            ReceivedMessageEvent?.Invoke(null,e.Body);
        }
    }


}