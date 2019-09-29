using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
        public QuoteAction(IFCMAPIConnection conn)
        {
            _conn = conn;
        }
        public override Task<QuoteResponse> AddQuote(QuoteRequest request, ServerCallContext context)
        {
            return Task<QuoteResponse>.Run(() => new QuoteResponse()
                {Result = _conn.AddQuote(request.Exchange, request.Symbol).Result ? 0 : -1});
        }

        public override Task<QuoteResponse> RemoveQuote(QuoteRequest request, ServerCallContext context)
        {
            return Task<QuoteResponse>.Run(() => new QuoteResponse()
                {Result = _conn.RemoveQuote(request.Exchange, request.Symbol).Result ? 0 : -1});
        }

        public override Task<QuoteServiceActionResponse> Reconnect(Empty request, ServerCallContext context)
        {
            return Task<QuoteServiceActionResponse>.Run(() => new QuoteServiceActionResponse()
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
            //try
            //{
            //    _server = new Server();
            //}
            //catch (Exception e)
            //{
            //    Console.WriteLine(e.Message);
            //    throw;
            //}
            _server = new Server()
            {
                Services =
                {
                    QuoteService.GRPC.QuoteService.BindService(new QuoteAction(conn))
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
