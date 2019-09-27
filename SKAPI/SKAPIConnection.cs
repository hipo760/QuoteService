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
        private DataEventBroker<ConnectionStatusEvent> _connStatusBroker;
        private IConfiguration _config;

        internal SKAPISetting _apiSetting;
        internal int _loginCode = -1;
        protected Dictionary<short, (short pageNo, Quote quote)> _quoteDict;
        protected ConnectionStatus _skStatus;

        private SKCenterLib _skCenter;
        private SKReplyLib _skReply;
        private SKQuoteLib _skQuotes;

        public SKAPIConnection(ILogger logger, DataEventBroker<ConnectionStatusEvent> connStatusBroker, IConfiguration config)
        {
            _logger = logger;
            _connStatusBroker = connStatusBroker;
            _config = config;
            
            _apiSetting = new SKAPISetting();
            config.GetSection("SKAPISetting").Bind(_apiSetting);
            
            _logger.Debug("[SKAPIConnection()] Begin of constructor...");
            _quoteDict = new Dictionary<short, (short pageNo, Quote quote)>();
            _skStatus = ConnectionStatus.NotConnected;
            InitSKCOMLib();
        }


        private short GetSKStockIdx(string symbol)
        {
            _logger.Debug("[SKAPIConnection.GetSKStockIdx()] {symbol}",  symbol);
            SKSTOCK refStock = new SKSTOCK();
            _logger.Debug("[SKAPIConnection.SKQuoteLib_GetStockByNo()] {symbol}", symbol);
            var code = _skQuotes.SKQuoteLib_GetStockByNo(symbol, ref refStock);
            _logger.Debug("[SKAPIConnection.SKQuoteLib_GetStockByNo()] {code}", code);
            return (code == 0) ? refStock.sStockIdx : (short)-1;
        }

        private void ReleaseSKCOMObject()
        {
            _logger.Debug("[SKAPIConnection.ReleaseSKCOMObject()] Release...");

            //OnConnectionEvent -= (kind, code) => ConnectionEventHandler(kind, code);
            //OnNotifyTicksEvent
            //    -= (no, index, ptr, date, timehms, timemillismicros, bid, ask, close, qty, simulate)
            //        => TickHandler(index, date, timehms, timemillismicros, close, qty, simulate);
            //OnNotifyHistoryTicksEvent
            //    -= (no, index, ptr, date, timehms, timemillismicros, bid, ask, close, qty, simulate)
            //        => TickHandler(index, date, timehms, timemillismicros, close, qty, simulate);

            OnConnectionEvent -= UpdateConnectionStatus;
            OnNotifyTicksEvent -= SKAPIConnection_OnNotifyTicksEvent;
            OnNotifyHistoryTicksEvent -= SKAPIConnection_OnNotifyHistoryTicksEvent;

            if (_skReply != null) Marshal.ReleaseComObject(_skReply);
            // 2.13.18
            //_skReply.OnReplyMessage -= SkReply_OnReplyMessage;
            
            _skQuotes.SKQuoteLib_LeaveMonitor();
            Thread.Sleep(TimeSpan.FromMilliseconds(_apiSetting.SKServerLoadingTime));
            

            
            if (_skQuotes != null) Marshal.ReleaseComObject(_skQuotes);
            if (_skCenter != null) Marshal.ReleaseComObject(_skCenter);
            _logger.Debug("[SKAPIConnection.ReleaseSKCOMObject()] Clear.");
            _skStatus = ConnectionStatus.NotConnected;
        }

        private void InitSKCOMLib()
        {
            _logger.Debug("[SKAPIConnection.InitSKCOMLib()] Init...");

            _skCenter = new SKCenterLib();
            _skReply = new SKReplyLib();
            _skQuotes = new SKQuoteLib();

            //OnConnectionEvent += (kind, code) => ConnectionEventHandler(kind,code);
            //OnNotifyTicksEvent
            //    += (no, index, ptr, date, timehms, timemillismicros, bid, ask, close, qty, simulate)
            //        => TickHandler(index, date, timehms, timemillismicros, close, qty, simulate);
            //OnNotifyHistoryTicksEvent
            //    += (no, index, ptr, date, timehms, timemillismicros, bid, ask, close, qty, simulate)
            //        => TickHandler(index, date, timehms, timemillismicros, close, qty, simulate);


            OnConnectionEvent += UpdateConnectionStatus;
            OnNotifyTicksEvent += SKAPIConnection_OnNotifyTicksEvent;
            OnNotifyHistoryTicksEvent += SKAPIConnection_OnNotifyHistoryTicksEvent;

            // 2.13.18
            //_skReply.OnReplyMessage += SkReply_OnReplyMessage;
        }

        

        private void SkReply_OnReplyMessage(string bstrUserID, string bstrMessage, out short sConfirmCode)
        {
            _logger.Debug($"[SKAPIConnection.SkReply_OnReplyMessage()] {bstrUserID}, {bstrMessage}");
            sConfirmCode = -1;
        }

        public void Dispose()
        {
            Disconnect().Wait();
        }

        

        #region Implementation of IFCMAPIConnection

        public List<string> QuotesList => _quoteDict.Values.ToList().Select(t=>t.quote.Name).ToList();
        public ConnectionStatus APIStatus => _skStatus;

        public async Task<bool> Connect()
            => await Task<bool>.Run(() =>
            {
                _logger.Debug("[SKAPIConnection.Connect()] Login...");
                //_loginCode = _skCenter.SKCenterLib_Login(_apiSetting.ID, _apiSetting.Password);
                //_logger.Debug($"[SKAPIConnection.ExecuteRetryLoginPolicy()] {_skCenter.SKCenterLib_GetReturnCodeMessage(_loginCode)}");
                // Wait for reply message...
                ExecuteRetryLoginPolicy(3);
                int quoteServerCode = _skQuotes.SKQuoteLib_EnterMonitor();
                if (quoteServerCode != 0) return false;
                ExecuteWaitingConnectionReadyPolicy(5);
                return APIStatus == ConnectionStatus.ConnectionReady;
            });

        public Task<bool> Reconnect()
        {
            //lock (obj)
            //{
            return Task.Run(() =>
            {
                _logger.Debug("[SKAPIConnection.Reconnect()] ReConnecting....");
                // Save all quote.
                var itemsToRescribe = _quoteDict
                    .Select(x => (x.Value.quote.QuoteInfo.Exchange, x.Value.quote.QuoteInfo.Symbol)).ToArray();
                RemoveAllQuotes().Wait();
                ReleaseSKCOMObject();
                InitSKCOMLib();
                var conn = Connect().Result;
                if (!conn) return conn;
                foreach (var item in itemsToRescribe)
                {
                    AddQuote(item.Exchange, item.Symbol).Wait();
                }
                return true;
            });
            //}
        }


        public async Task Disconnect()
            => await Task.Run(() =>
            {
                _logger.Debug("[SKAPIConnection.Disconnect()] Disconnect...");
                RemoveAllQuotes().Wait();
                ReleaseSKCOMObject();
                InitSKCOMLib();
            });

        public async Task<bool> AddQuote(string exchange, string symbol)
            => await Task<bool>.Run(() =>
            {
                _logger.Debug("[SKAPIConnection.AddQuote()] {exchange},{symbol}",exchange,symbol);
                short skIdx;
                try
                {
                    skIdx = GetSKStockIdx(symbol);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                    throw;
                }
                

                if (_quoteDict.Any(p => p.Value.quote.QuoteInfo.Symbol == symbol) || skIdx < 0) return false;
                short pageNo = (short)(_quoteDict.Count);
                _logger.Debug("[SKAPIConnection.AddQuoteRequest()] Add {symbol} to quote dict with key: {key}...",symbol,skIdx);
                _quoteDict.Add(skIdx, 
                    (pageNo,
                    new Quote(
                        new QuoteInfo() {ApiSource = "Capital", Exchange = exchange, Symbol = symbol},
                        _logger,
                        _config)
                    )
                );

                switch (exchange)
                {
                    case "TAIFEX":
                        var addResult =  ExecuteRetryAddRequestTickPolicy(3, symbol, ref pageNo);
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

        public async Task<bool> RemoveQuote(string exchange, string symbol)
            => await Task<bool>.Run(() =>
            {
                int apiReturnCode = -1;
                if (!CloseQuote(exchange, symbol).Result) return false;
                var item = _quoteDict.FirstOrDefault(x => (x.Value.quote.QuoteInfo.Symbol == symbol && x.Value.quote.QuoteInfo.Exchange == exchange)).Key;
                _quoteDict.Remove(item);
                _logger.Debug("[SKAPIConnection.RemoveQuoteRequest()] SKQuoteLib_RequestTicks...");
                if (exchange == "TAIFEX")
                {
                    apiReturnCode = _skQuotes.SKQuoteLib_RequestTicks(50, symbol);
                }
                Thread.Sleep(_apiSetting.SKServerLoadingTime);
                return (apiReturnCode == 0);
            });

        public async Task RemoveAllQuotes()
        => await Task.Run(() =>
        {
            foreach (var kv in _quoteDict)
            {
                _skQuotes.SKQuoteLib_RequestTicks(50, kv.Value.quote.QuoteInfo.Symbol);
                kv.Value.quote.Dispose();
            }
            _quoteDict.Clear();
        });
        #endregion
    }
}