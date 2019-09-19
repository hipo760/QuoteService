using System;
using System.Security.Cryptography.X509Certificates;
using QuoteService.QuoteData;
using Serilog;

namespace QuoteService.FCMAPI
{
    public class ConnectionStatusEvent
    {
        public DateTime LocalTime { get; set; }
        public ConnectionStatus ConnectionStatus { get; set; }
        public string Message { get; set; }
        public override string ToString() => ConnectionStatus.ToString() + ": " + Message;

        public static ConnectionStatusEvent GetEvent(ConnectionStatus connectionStatus, string message)
        {
            return new ConnectionStatusEvent
            {
                LocalTime = DateTime.Now,
                ConnectionStatus = connectionStatus,
                Message = message
            };
        }
    }

    public class HealthAction
    {
        private DataEventBroker<ConnectionStatusEvent> _connStatusBroker;
        private static readonly Object obj = new Object();

        public HealthAction(IFCMAPIConnection apiConnection, ILogger logger, DataEventBroker<ConnectionStatusEvent> connStatusBroker)
        {
            _connStatusBroker = connStatusBroker;
            _connStatusBroker.Subscribe(x =>
            {
                logger.Debug("[HealthAction()] {ConnectionStatus} : {Message}",
                    x.ConnectionStatus.ToString(),
                    x.Message
                );
                switch (x.ConnectionStatus)
                {
                    case ConnectionStatus.NotConnected:
                        apiConnection.Reconnect().Wait();
                        break;
                    case ConnectionStatus.Connecting:
                        break;
                    case ConnectionStatus.ConnectionReady:
                        break;
                    case ConnectionStatus.ConnectionError:
                        break;
                    default:
                        //throw new ArgumentOutOfRangeException();
                        break;
                }
            });
            //apiConnection.Connect().Wait();
            _connStatusBroker.Publish(ConnectionStatusEvent.GetEvent(apiConnection.APIStatus, "Ready for Connect()."));
        }
    }
}