using System;
using System.Threading.Tasks;
using Google.Protobuf.WellKnownTypes;
using QuoteService.FCMAPI;
using QuoteService.QuoteData;
using SKCOMLib;

namespace SKAPI
{
    public partial class SKAPIConnection
    {
        public event _ISKQuoteLibEvents_OnConnectionEventHandler OnConnectionEvent
        {
            add { _skQuotes.OnConnection += value; }
            remove { _skQuotes.OnConnection -= value; }
        }
        public event _ISKQuoteLibEvents_OnNotifyTicksEventHandler OnNotifyTicksEvent
        {
            add { _skQuotes.OnNotifyTicks += value; }
            remove { _skQuotes.OnNotifyTicks -= value; }
        }
        public event _ISKQuoteLibEvents_OnNotifyHistoryTicksEventHandler OnNotifyHistoryTicksEvent
        {
            add { _skQuotes.OnNotifyHistoryTicks += value; }
            remove { _skQuotes.OnNotifyHistoryTicks -= value; }
        }

        private Action<int, int> ConnectionEventHandler => UpdateConnectionStatus;


        private void UpdateConnectionStatus(int nkind, int ncode)
        {
            _logger.Debug("[SKAPIConnection.SkQuotes_OnConnection()] {nkind}", nkind);
            switch (nkind)
            {
                case 3001:
                    _skStatus = ConnectionStatus.Connecting;
                    break;
                case 3003:
                    _skStatus = ConnectionStatus.ConnectionReady;
                    break;
                case 3002:
                    _skStatus = ConnectionStatus.NotConnected;
                    break;
                case 3021:
                case 3022:
                    _skStatus = ConnectionStatus.ConnectionError;
                    break;
                default:
                    _skStatus = ConnectionStatus.Unknown;
                    break;
            }
            _connStatusBroker.Publish(ConnectionStatusEvent.GetEvent(_skStatus, _skCenter.SKCenterLib_GetReturnCodeMessage(nkind)));
        }


        Action<short, int, int, int, int, int, int> TickHandler =>
            async (sIndex, nDate, nTimehms, nTimemillismicros, nClose, nQty, nSimulate)
                => await PublishAsync(sIndex, nDate, nTimehms, nTimemillismicros, nClose, nQty);

        private void SKAPIConnection_OnNotifyTicksEvent(short sMarketNo, short sIndex, int nPtr, int nDate,
            int nTimehms, int nTimemillismicros, int nBid, int nAsk, int nClose, int nQty, int nSimulate)
            => Task.Run(async () => await PublishAsync(sIndex, nDate, nTimehms, nTimemillismicros, nClose, nQty));

        private void SKAPIConnection_OnNotifyHistoryTicksEvent(short sMarketNo, short sIndex, int nPtr, int nDate,
            int nTimehms, int nTimemillismicros, int nBid, int nAsk, int nClose, int nQty, int nSimulate)
            => Task.Run(async () => await PublishAsync(sIndex, nDate, nTimehms, nTimemillismicros, nClose, nQty));


        //Action<short, int, int, int, int, int, int> HistoryTickHandler =>
        //    async (sIndex, nDate, nTimehms, nTimemillismicros, nClose, nQty, nSimulate)
        //        => await Publish(sIndex, nDate, nTimehms, nTimemillismicros, nClose, nQty);

        private Task PublishAsync(short sstockidx, int ndate, int ntimehms, int ntimemillismicros, int nclose, int nqty)
        {
            return Task.Run(() =>
            {
                if (!_quoteDict.ContainsKey(sstockidx)) return;
                var dt = DateTime.ParseExact(
                    $"{ndate} {ntimehms.ToString().PadLeft(6, '0')}.{ntimemillismicros.ToString().PadLeft(6, '0')}",
                    "yyyyMMdd HHmmss.ffffff",
                    System.Globalization.CultureInfo.InvariantCulture);
                nclose = nclose / 100;
                _quoteDict[sstockidx].quote?.DataBroker.Publish(new Tick()
                {
                    DealPrice = (float)nclose,
                    DealQty = nqty,
                    LocalTime = dt.ToUniversalTime().ToTimestamp()
                });
            });
        }
        



    }
}