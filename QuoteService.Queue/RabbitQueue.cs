using System;
using System.Text;
using RabbitMQ.Client;
using Serilog;

namespace QuoteService.Queue
{
    public class QueueSetting
    {
        public string HostName { get; set; }
        public string RoutingKey { get; set; }
        public string ExchangeName { get; set; }
    }

    public class RabbitFanout : IQueueFanout
    {
        internal IConnection _conn;
        internal IModel _channel;
        private QueueSetting _setting;
        private ILogger _log;

        private RabbitFanout(ILogger logger, QueueSetting setting)
        {
            _setting = setting;
            _log = logger;
            _conn = new ConnectionFactory() { HostName = _setting.HostName }.CreateConnection();
            _channel = _conn.CreateModel();
        }
        public void Send(string message)
        {
            var body = Encoding.UTF8.GetBytes(message);
            _channel.BasicPublish(
                exchange: _setting.ExchangeName,
                routingKey: _setting.RoutingKey, 
                basicProperties: null, 
                body: body
                );
        }

        public void Dispose()
        {
            _channel?.Dispose();
            _conn?.Dispose();
        }
    }

    

}