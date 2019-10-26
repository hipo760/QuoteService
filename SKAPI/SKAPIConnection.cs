using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Remoting.Messaging;
using System.Threading;
using System.Threading.Tasks;
using QuoteService.FCMAPI;
using Google.Protobuf.WellKnownTypes;
using Microsoft.Extensions.Configuration;
using Polly;
using QuoteResearch.Service.Share.Type;
using QuoteService.Queue;
using QuoteService.Quote;
using QuoteService.QuoteData;
using Serilog;
using Serilog.Sinks.SystemConsole.Themes;
using SKCOMLib;


namespace SKAPI
{
    public partial class SKAPIConnection : IFCMAPIConnection
    {
        protected ILogger _logger;
        private SkapiWrapper _skapi;
        private DataEventBroker<ConnectionStatusEvent> _connStatusBroker;
        private IConfiguration _config;

        internal SKAPISetting _apiSetting;
        internal int _loginCode = -1;
        protected Dictionary<short, (short pageNo, DataEmitter quote)> _quoteDict;
        protected ConnectionStatus _skStatus;

        public SKAPIConnection(ILogger logger,SkapiWrapper skapi, DataEventBroker<ConnectionStatusEvent> connStatusBroker, IConfiguration config)
        {
            _logger = logger;
            _connStatusBroker = connStatusBroker;
            _config = config;
            _apiSetting = new SKAPISetting();
            config?.GetSection("SKAPISetting")?.Bind(_apiSetting);
            _logger.Debug("[SKAPIConnection()] Begin of constructor...");
            _quoteDict = new Dictionary<short, (short pageNo, DataEmitter quote)>();
            _skStatus = ConnectionStatus.NotConnected;
            _skapi = skapi;
            SubscribeSkapiEvent();
        }

        private void ReleaseSKCOMObject()
        {
            _logger.Debug("[SKAPIConnection.ReleaseSKCOMObject()] Release...");
            _skapi.OnConnectionEvent -= UpdateConnectionStatus;
            _skapi.OnNotifyTicksEvent -= SKAPIConnection_OnNotifyTicksEvent;
            _skapi.OnNotifyHistoryTicksEvent -= SKAPIConnection_OnNotifyHistoryTicksEvent;
            _skapi.SKQuoteLib_LeaveMonitor();
            Thread.Sleep(TimeSpan.FromMilliseconds(_apiSetting.SKServerLoadingTime));
            _skapi.ReleaseSkcomLib();
            _logger.Debug("[SKAPIConnection.ReleaseSKCOMObject()] Clear.");
            _skStatus = ConnectionStatus.NotConnected;
        }

        private void InitSKCOMLib()
        {
            _logger.Debug("[SKAPIConnection.InitSKCOMLib()] Init...");
            _skapi.InitSkcomLib();
            SubscribeSkapiEvent();
        }

        public void SubscribeSkapiEvent()
        {
            _skapi.OnConnectionEvent += UpdateConnectionStatus;
            _skapi.OnNotifyTicksEvent += SKAPIConnection_OnNotifyTicksEvent;
            _skapi.OnNotifyHistoryTicksEvent += SKAPIConnection_OnNotifyHistoryTicksEvent;
        }

        public void Dispose()
        {
            Disconnect().Wait();
        }

        

        #region Implementation of IFCMAPIConnection

        public List<Quote> QuotesList => _quoteDict.Values.ToList().Select(t=>t.quote.QuoteInfo).ToList();
        public ConnectionStatus APIStatus => _skStatus;

        public async Task InitAPI()
        {
            InitSKCOMLib();
        }

        public async Task<bool> Connect()
            => await Task<bool>.Run(() =>
            {
                _logger.Debug("[SKAPIConnection.Connect()] Login...");
                //_loginCode = _skCenter.SKCenterLib_Login(_apiSetting.ID, _apiSetting.Password);
                //_logger.Debug($"[SKAPIConnection.ExecuteRetryLoginPolicy()] {_skCenter.SKCenterLib_GetReturnCodeMessage(_loginCode)}");
                // Wait for reply message...
                ExecuteRetryLoginPolicy(3);
                int quoteServerCode = _skapi.SKQuoteLib_EnterMonitor();
                if (quoteServerCode != 0) return false;
                ExecuteRetryWaitingConnectionReadyPolicy(20);
                return APIStatus == ConnectionStatus.ConnectionReady;
            });

        public Task<bool> Reconnect()
        {
            return Task.Run(() =>
            {
                _logger.Debug("[SKAPIConnection.Reconnect()] ReConnecting....");
                // Save all quote.
                var itemsToRescribe = _quoteDict.Select(x => (x.Value.quote.QuoteInfo)).ToArray();
                RemoveAllQuotes().Wait();
                ReleaseSKCOMObject();
                InitSKCOMLib();
                var conn = Connect().Result;
                if (!conn) return conn;
                foreach (var item in itemsToRescribe)
                {
                    AddQuote(item).Wait();
                }
                return true;
            });
        }

        public async Task Disconnect()
            => await Task.Run(() =>
            {
                _logger.Debug("[SKAPIConnection.Disconnect()] Disconnect...");
                RemoveAllQuotes().Wait();
                ReleaseSKCOMObject();
                InitSKCOMLib();
            });

        public async Task<bool> AddQuote(Quote quote)
            => await Task<bool>.Run(() =>
            {
                _logger.Debug("[SKAPIConnection.AddQuote()] {exchange},{symbol}",quote.Exchange, quote.Symbol);
                short skIdx;
                try
                {
                    skIdx = _skapi.GetSkStockIdxBySymbol(quote.Symbol);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                    throw;
                }

                if (_quoteDict.Any(p => p.Value.quote.QuoteInfo.Symbol == quote.Symbol) || skIdx < 0) return false;
                short pageNo = (short)(_quoteDict.Count);
                _logger.Debug("[SKAPIConnection.AddQuoteRequest()] Add {symbol} to quote dict with key: {key}...",quote.Symbol,skIdx);
                _quoteDict.Add(skIdx, (pageNo, new DataEmitter(quote, _logger, _config)));

                switch (quote.Exchange)
                {
                    case "TAIFEX":
                        var addResult =  ExecuteRetryAddRequestTickPolicy(3, quote.Symbol, ref pageNo);
                        _logger.Debug($"[SKAPIConnection.AddQuoteRequest()] SKQuoteLib_RequestTicks got page no: {pageNo}...");
                        if (!addResult)
                        {
                            _quoteDict.Remove(skIdx);
                            return false;
                        }
                        return true;
                    default:
                        return false;
                }
            });

        public async Task<bool> CloseQuote(string exchange, string symbol)
            => await Task<bool>.Run(() =>
            {
                if (_quoteDict.All(p => p.Value.quote.QuoteInfo.Symbol != symbol)) return false;
                var item = _quoteDict.Values.FirstOrDefault(x => (x.quote.QuoteInfo.Symbol == symbol && x.quote.QuoteInfo.Exchange == exchange));
                item.quote.DataBroker.Close();
                return true;
            });

        public async Task<bool> RemoveQuote(Quote quote)
            => await Task<bool>.Run(() =>
            {
                int apiReturnCode = -1;
                short pageNo = 50;
                if (!CloseQuote(quote.Exchange, quote.Symbol).Result) return false;
                var item = _quoteDict.FirstOrDefault(x => (x.Value.quote.QuoteInfo.Symbol == quote.Symbol && x.Value.quote.QuoteInfo.Exchange == quote.Exchange)).Key;
                _quoteDict.Remove(item);
                _logger.Debug("[SKAPIConnection.RemoveQuoteRequest()] SKQuoteLib_RequestTicks...");
                if (quote.Exchange == "TAIFEX")
                {
                    apiReturnCode = _skapi.SKQuoteLib_RequestTicks(ref pageNo, quote.Symbol);
                }
                Thread.Sleep(_apiSetting.SKServerLoadingTime);
                return (apiReturnCode == 0);
            });

        public async Task RemoveAllQuotes()
        => await Task.Run(() =>
        {
            short pageNo = 50;
            foreach (var kv in _quoteDict)
            {
                _skapi.SKQuoteLib_RequestTicks(ref pageNo, kv.Value.quote.QuoteInfo.Symbol);
                kv.Value.quote.Dispose();
            }
            _quoteDict.Clear();
        });
        #endregion
    }
}