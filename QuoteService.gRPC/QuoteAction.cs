using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Google.Protobuf.Collections;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using QuoteService.FCMAPI;
using QuoteService.GRPC;
using Serilog;


namespace QuoteService.gRPC
{
    public class QuoteAction: QuoteService.GRPC.QuoteService.QuoteServiceBase
    {
        private IFCMAPIConnection _conn;
        private ILogger _log;
        public QuoteAction(IFCMAPIConnection conn, ILogger logger)
        {
            _conn = conn;
        }
        public override Task<QuoteResponse> AddQuote(QuoteInfo request, ServerCallContext context)
        {
            return Task<QuoteResponse>.Run(() => new QuoteResponse()
                {Result = _conn.AddQuote(request.Exchange, request.Symbol).Result ? 0 : -1});
        }

        public override Task<QuoteResponse> RemoveQuote(QuoteInfo request, ServerCallContext context)
        {
            return Task<QuoteResponse>.Run(() => new QuoteResponse()
                {Result = _conn.RemoveQuote(request.Exchange, request.Symbol).Result ? 0 : -1});
        }

        public override Task<GetQuoteListResponse> GetQuoteList(QuoteActionEmptyRequest request, ServerCallContext context)
        {
            return Task<GetQuoteListResponse>.Run(() =>
            {
                var quoteListResponse = new GetQuoteListResponse();
                var list = _conn.QuotesList;
                list.ForEach(x =>
                {
                    var quoteInfo = x.Split('.');
                    quoteListResponse.QuoteList.Add(new QuoteInfo(){Exchange = quoteInfo[0],Symbol = quoteInfo[1]});
                });
                return quoteListResponse;
            });
        }

        public override Task<ConnectionStatusResponse> GetConnectionStatus(ConnectionActionEmptyRequest request, ServerCallContext context)
        {
            return Task<ConnectionStatusResponse>.Run(() => new ConnectionStatusResponse() {Status = _conn.APIStatus});
        }
        
        public override Task<ConnectionActionResponse> Reconnect(ConnectionActionEmptyRequest request, ServerCallContext context)
        {
            return Task<ConnectionActionResponse>.Run(() => new ConnectionActionResponse()
                {Result = _conn.Reconnect().Result ? 0 : -1});
        }
    }

    public class QuoteActionGRPCServerSetting
    {
        public string Host { get; set; }
        public int Port { get; set; }
    }

    public class QuoteActionServer
    {
        private Server _server;
        private ILogger _log;
        private QuoteActionGRPCServerSetting _setting;
        public QuoteActionServer(IFCMAPIConnection conn,QuoteActionGRPCServerSetting setting, ILogger logger)
        {
            _log = logger;
            _setting = setting;
            _log.Debug("[QuoteActionServer.ctor] Host on {Host}:{Port}",setting.Host,setting.Port);
            
            _server = new Server()
            {
                Services =
                {
                    QuoteService.GRPC.QuoteService.BindService(new QuoteAction(conn,logger))
                },
                Ports =
                {
                    new ServerPort(setting.Host,setting.Port,ServerCredentials.Insecure)
                }
            };
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
