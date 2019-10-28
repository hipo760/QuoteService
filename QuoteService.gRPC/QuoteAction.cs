using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Remoting.Messaging;
using System.Text;
using System.Threading.Tasks;
using Google.Protobuf.Collections;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
//using Grpc.HealthCheck;
using QuoteResearch.HealthCheck;
using QuoteResearch.Service.CommandService;
using QuoteResearch.Service.QuoteService;
using QuoteResearch.Service.Share.Type;
using QuoteService.FCMAPI;
using QRService = QuoteResearch.Service;
using Serilog;


namespace QuoteService.gRPC
{
    public class QuoteServiceGrpc: QRService.QuoteService.QuoteService.QuoteServiceBase
    {
        private IFCMAPIConnection _conn;
        private ILogger _log;
        public QuoteServiceGrpc(IFCMAPIConnection conn, ILogger logger)
        {
            _conn = conn;
            _log = logger;
        }

        public override Task<GetQuoteListResponse> GetQuoteList(EmptyRequest request, ServerCallContext context)
        {
            return Task.Run(() =>
            { 
                var response = new GetQuoteListResponse();
                response.QuoteList.AddRange(_conn.QuotesList);
                return response;
            });
        }
    }

    public class CommandServiceGrpc : CommandService.CommandServiceBase
    {
        private IFCMAPIConnection _conn;
        private ILogger _log;
        public CommandServiceGrpc(IFCMAPIConnection conn, ILogger logger)
        {
            _conn = conn;
            _log = logger;
        }


        public override Task<CommandServiceResponse> Start(EmptyRequest request, ServerCallContext context)
        {
            return Task.Run(() =>
            {
                var response = new CommandServiceResponse();
                bool result = _conn.Reconnect().Result;
                response.Result = result?0:-1;
                return response;
            });
        }

        public override Task<CommandServiceResponse> Stop(EmptyRequest request, ServerCallContext context)
        {
            return Task.Run(() =>
            {
                var response = new CommandServiceResponse();
                _conn.Disconnect().Wait();
                response.Result = 0;
                return response;
            });
        }

        public override Task<CommandServiceResponse> Restart(EmptyRequest request, ServerCallContext context)
        {
            return Task.Run(() =>
            {
                var response = new CommandServiceResponse();
                bool result = _conn.Reconnect().Result;
                response.Result = result ? 0 : -1;
                return response;
            });
        }
    }

    public class HealthCheckGrpc : Health.HealthBase
    {
        private IFCMAPIConnection _conn;
        private ILogger _log;
        public HealthCheckGrpc(IFCMAPIConnection conn, ILogger logger)
        {
            _conn = conn;
            _log = logger;
        }
        public override async Task<HealthCheckResponse> Check(HealthCheckRequest request, ServerCallContext context)
        {
            //return base.Check(request, context);
            var status = GetAPIServingStatus();
            return new HealthCheckResponse() { Status = status };
        }

        private HealthCheckResponse.Types.ServingStatus GetAPIServingStatus()
        {
            HealthCheckResponse.Types.ServingStatus status;
            switch (_conn.APIStatus)
            {
                case ConnectionStatus.NotConnected:
                case ConnectionStatus.Connecting:
                case ConnectionStatus.ConnectionError:
                    status = HealthCheckResponse.Types.ServingStatus.NotServing;
                    break;
                case ConnectionStatus.ConnectionReady:
                    status = HealthCheckResponse.Types.ServingStatus.Serving;
                    break;
                case ConnectionStatus.Unknown:
                    status = HealthCheckResponse.Types.ServingStatus.Unknown;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
            return status;
        }

        public override async Task Watch(HealthCheckRequest request, IServerStreamWriter<HealthCheckResponse> responseStream, ServerCallContext context)
        {
            var status = GetAPIServingStatus();
            var response = new HealthCheckResponse() {Status = status};
            await responseStream.WriteAsync(response);
        }
    }


    public class QuoteActionGRPCServerSetting
    {
        public string Host { get; set; }
        public int Port { get; set; }
    }

    public class GrpcServer
    {
        private Server _server;
        private ILogger _log;
        private QuoteActionGRPCServerSetting _setting;
        public GrpcServer(IFCMAPIConnection conn,QuoteActionGRPCServerSetting setting, ILogger logger)
        {
            _log = logger;
            _setting = setting;
            _log.Debug("[QuoteActionServer.ctor] Host on {Host}:{Port}",setting.Host,setting.Port);


            



            _server = new Server()
            {
                Services =
                {
                    QRService.QuoteService.QuoteService.BindService(new QuoteServiceGrpc(conn,logger)),
                    CommandService.BindService(new CommandServiceGrpc(conn,logger)),
                    Health.BindService(new HealthCheckGrpc(conn,logger))
                },
                Ports =
                {
                    new ServerPort(setting.Host,setting.Port,ServerCredentials.Insecure)
                }
            };

            //var healthImplementation = new HealthServiceImpl();

        }
        public void Start()
        {
            _log.Debug("[QuoteActionServer.Start()] start on {Host}:{Port}", _setting.Host, _setting.Port);
            _server.Start();
        }

        public void Stop()
        {
            _log.Debug("[QuoteActionServer.Stop()] stop on {Host}:{Port}", _setting.Host, _setting.Port);
            _server.ShutdownAsync().Wait();
        }
    }
}
