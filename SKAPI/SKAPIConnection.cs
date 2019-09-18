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
        SKCenterLib _skCenter;
        SKReplyLib _skReply;
        SKQuoteLib _skQuotes;
        internal SKAPISetting _apiSetting;
        internal GCPPubSubSetting _queueSetting;
        internal int _loginCode = -1;

        protected Dictionary<short, (short pageNo, Quote quote)> _quoteDict;
        protected ConnectionStatus _skStatus;
        //protected DataEventBroker<MessageEvent> _messagEventBroker;

        public SKAPIConnection(ILogger logger, SKAPISetting apiSetting, GCPPubSubSetting queueSetting)
        {
            _logger = logger;
            //_messagEventBroker = messageEventBroker;
            _apiSetting = apiSetting;
            _queueSetting = queueSetting;
            _logger.Debug("[SKAPIConnection()] Begin of constructor...");
            _quoteDict = new Dictionary<short, (short pageNo, Quote quote)>();
            InitSKCOMLib();
            Connect().Wait();
        }

        protected Task _skQuotes_OnNotifyTicks(
            short smarketno, short sindex, int nptr, 
            int ndate, int ntimehms, int ntimemillismicros, 
            int nbid, int nask, 
            int nclose, int nqty, 
            int nsimulate)
        {
            //_quoteDict[sindex]?.DataBroker.Close();
            return Task.Run(()=> Publish(sindex, ndate, ntimehms, ntimemillismicros, nclose, nqty));
        }

        protected Task _skQuotes_OnNotifyHistoryTicks(
            short smarketno, short sstockidx, int nptr,
            int ndate, int ntimehms, int ntimemillismicros,
            int nbid, int nask,
            int nclose, int nqty,
            int nsimulate)
        {
            return Task.Run(() => Publish(sstockidx, ndate, ntimehms, ntimemillismicros, nclose, nqty));
        }

        private void Publish(short sstockidx, int ndate, int ntimehms, int ntimemillismicros, int nclose, int nqty)
        {
            if (!_quoteDict.ContainsKey(sstockidx)) return;
            var dt = DateTime.ParseExact(
                $"{ndate} {ntimehms.ToString().PadLeft(6, '0')}.{ntimemillismicros.ToString().PadLeft(6, '0')}",
                "yyyyMMdd HHmmss.ffffff",
                System.Globalization.CultureInfo.InvariantCulture);
            nclose = nclose / 100;
            _quoteDict[sstockidx].quote?.DataBroker.Publish(new Tick()
            {
                DealPrice = (float) nclose,
                DealQty = nqty,
                LocalTime = dt.ToUniversalTime().ToTimestamp()
            });
        }

        private async Task SkQuotes_OnConnection(int nkind, int ncode)
        {
            await Task.Run(() => {
                _logger.Debug("[SKAPIConnection.SkQuotes_OnConnection()] {nkind}",nkind);
                switch (nkind)
                {
                    case 3001:
                        _skStatus = ConnectionStatus.Connecting;
                        break;
                    case 3003:
                        _skStatus = ConnectionStatus.ConnectionReady;
                        break;
                    case 3002:
                    case 3021:
                    case 3022:
                        _skStatus = ConnectionStatus.ConnectionError;
                        break;
                    default:
                        _skStatus = ConnectionStatus.NotConnected;
                        break;
                }
            });
        }

        private short GetSKStockIdx(string symbol)
        {
            var refStock = new SKSTOCK();
            var code = _skQuotes.SKQuoteLib_GetStockByNo(symbol, ref refStock);
            return (code == 0) ? refStock.sStockIdx : (short)-1;
        }

        private void ReleaseSKCOMObject()
        {
            _logger.Debug("[SKAPIConnection.ReleaseSKCOMObject()] Release...");
            
            if (_skReply != null) Marshal.ReleaseComObject(_skReply);
            // 2.13.18
            //_skReply.OnReplyMessage -= SkReply_OnReplyMessage;
            _skQuotes.SKQuoteLib_LeaveMonitor();
            _skQuotes.OnConnection -= async (kind, code) => await SkQuotes_OnConnection(kind, code);
            Thread.Sleep(TimeSpan.FromSeconds(5));
            _skQuotes.OnNotifyTicks
                -= async (no, index, ptr, date, timehms, timemillismicros, bid, ask, close, qty, simulate)
                    => await _skQuotes_OnNotifyTicks(no, index, ptr, date, timehms, timemillismicros, bid, ask, close, qty,
                        simulate);
            _skQuotes.OnNotifyHistoryTicks
                -= async (no, idx, ptr, date, timehms, timemillismicros, bid, ask, close, qty, simulate)
                    => await _skQuotes_OnNotifyHistoryTicks(no, idx, ptr, date, timehms, timemillismicros, bid, ask,
                        close, qty, simulate);
            if (_skQuotes != null) Marshal.ReleaseComObject(_skQuotes);
            if (_skCenter != null) Marshal.ReleaseComObject(_skCenter);
            _skStatus = ConnectionStatus.NotConnected;
        }

        private void InitSKCOMLib()
        {
            _logger.Debug("[SKAPIConnection.InitSKCOMLib()] Init...");
            _skCenter = new SKCenterLib();
            _skReply = new SKReplyLib();
            _skQuotes = new SKQuoteLib();
            _skQuotes.OnConnection += async (kind, code) => await SkQuotes_OnConnection(kind, code);
            _skQuotes.OnNotifyTicks 
                += async (no, index, ptr, date, timehms, timemillismicros, bid, ask, close, qty, simulate) 
                    => await _skQuotes_OnNotifyTicks(no, index, ptr, date, timehms, timemillismicros, bid, ask, close, qty,
                    simulate);
            _skQuotes.OnNotifyHistoryTicks
                += async (no, idx, ptr, date, timehms, timemillismicros, bid, ask, close, qty, simulate) 
                    => await _skQuotes_OnNotifyHistoryTicks(no, idx, ptr, date, timehms, timemillismicros, bid, ask,
                        close, qty, simulate);
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
        
        public async Task<bool> Reconnect()
            => await Task.Run(() =>
            {
                _logger.Debug("[SKAPIConnection.Reconnect()] ReConnecting....");
                ReleaseSKCOMObject();
                InitSKCOMLib();
                var conn = Connect().Result;
                if (!conn) return conn;
                UpdateQuoteDictKey();
                foreach (var kv in _quoteDict)
                {
                    var quote = kv.Value.quote;
                    short pgNo = kv.Value.pageNo;
                    quote.InitTickBroker();
                    if (quote.QuoteInfo.Exchange=="TAIFEX")
                    {
                        if (!ExecuteRetryRemoveRequestTickPolicy(5, quote.QuoteInfo.Symbol)) return false;
                        if (!ExecuteRetryAddRequestTickPolicy(3, quote.QuoteInfo.Symbol, ref pgNo)) return false;
                    }
                }
                return true;
            });

        private void UpdateQuoteDictKey()
        {
            var itemsToRefresh = _quoteDict.Where(f => f.Key != GetSKStockIdx(f.Value.quote.QuoteInfo.Symbol)).ToArray();

            foreach (var item in itemsToRefresh)
            {
                var oldkey = item.Key;
                var newkey = GetSKStockIdx(item.Value.quote.QuoteInfo.Symbol);
                var quote = item.Value.quote;
                short pgNo = item.Value.pageNo;

                _quoteDict.Add(newkey, (pgNo, quote));
                _quoteDict.Remove(oldkey);
            }
        }

        public async Task Disconnect()
            => await Task.Run(() =>
            {
                _logger.Debug("[SKAPIConnection.Disconnect()] Disconnect....");
                _quoteDict.Clear();
                ReleaseSKCOMObject();
            });

        public async Task<bool> AddQuote(string exchange, string symbol)
            => await Task<bool>.Run(() =>
            {
                short pageNo = (short)(_quoteDict.Count);
                var skIdx = GetSKStockIdx(symbol);
                if (_quoteDict.Any(p => p.Value.quote.QuoteInfo.Symbol == symbol) || skIdx < 0) return false;
                _logger.Debug("[SKAPIConnection.AddQuoteRequest()] Add to quote dict...");
                _quoteDict.Add(skIdx, 
                    (pageNo,
                    new Quote(new QuoteInfo() {
                    ApiSource = "Capital",
                    Exchange = exchange,
                    Symbol = symbol
                },_logger, _queueSetting))
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
                var skIdx = GetSKStockIdx(symbol);
                if (_quoteDict.All(p => p.Value.quote.QuoteInfo.Symbol != symbol) || skIdx < 0) return false;
                _quoteDict[skIdx].quote.DataBroker.Close();
                return true;
            });

        public async Task<bool> RemoveQuote(string exchange, string symbol)
            => await Task<bool>.Run(() =>
            {
                int apiReturnCode = -1;
                if (!CloseQuote(exchange, symbol).Result) return false;
                _quoteDict.Remove(GetSKStockIdx(symbol));
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
                kv.Value.quote.Dispose();
            }
            _quoteDict.Clear();
        });
        #endregion
    }
}