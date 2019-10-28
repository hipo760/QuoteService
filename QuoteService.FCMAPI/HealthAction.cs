using System;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
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
        private ILogger _log;
        private IFCMAPIConnection _conn;

        private static readonly Object obj = new Object();

        public HealthAction(IFCMAPIConnection apiConnection, ILogger logger, DataEventBroker<ConnectionStatusEvent> connStatusBroker)
        {
            _connStatusBroker = connStatusBroker;
            _conn = apiConnection;
            _log = logger;
            _connStatusBroker.Subscribe(async x =>
            {
                logger.Debug("[HealthAction()] {ConnectionStatus} : {Message}",
                    x.ConnectionStatus.ToString(),
                    x.Message
                );
                await Task.Run(async () =>
                {
                    switch (x.ConnectionStatus)
                    {
                        case ConnectionStatus.NotConnected:
                            await apiConnection.Reconnect();
                            break;
                        case ConnectionStatus.Connecting:
                            //await CheckConnectingStatus();
                            await CheckConnectingStatus();
                            break;
                        case ConnectionStatus.ConnectionReady:
                            break;
                        case ConnectionStatus.ConnectionError:
                            await apiConnection.Reconnect();
                            break;
                        default:
                            //throw new ArgumentOutOfRangeException();
                            break;
                    }

                });
            });
            //apiConnection.Connect().Wait();
            _connStatusBroker.Publish(ConnectionStatusEvent.GetEvent(apiConnection.APIStatus, "Ready for Connect()."));
        }

        private Task CheckConnectingStatus()
        {
            return Task.Run(async () =>
            {
                _log.Debug("[HealthAction.CheckConnectingStatus()] Connecting, check connection status after 30 seconds...");
                Thread.Sleep(TimeSpan.FromSeconds(30));
                _log.Debug("[HealthAction.CheckConnectingStatus()] {ConnectionStatus}...",_conn.APIStatus.ToString());
                if (_conn.APIStatus != ConnectionStatus.ConnectionReady)
                {
                    _log.Debug("[HealthAction.CheckConnectingStatus()] Still connecting,  reconnect...");
                    await _conn.Reconnect();
                }
            });
        }
    }
}