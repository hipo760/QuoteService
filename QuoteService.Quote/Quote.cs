using System;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Google.Protobuf.WellKnownTypes;
using QuoteService.Queue;
using QuoteService.QuoteData;
using Serilog;

namespace QuoteService.Quote
{
    public class QuoteInfo
    {
        public string ApiSource { get; set; }
        public string Exchange { get; set; }
        public string Symbol { get; set; }
    }
    public class Quote : IDisposable
    {
        public QuoteInfo QuoteInfo { get; set; }

        public string Name => QuoteInfo.Exchange + "." + QuoteInfo.Symbol;
        public TimeSpan OHLCInterval { get; set; } = TimeSpan.FromSeconds(1);
        public DataEventBroker<Tick> DataBroker { get; set; }
        //public QueueConnectionClient QueueConn { get; set; }
        public OHLC LastOHLC { get; set; }

        private ILogger _logger;
        private QueueConnectionClient _queueFanout;

        public Quote(QuoteInfo quoteInfo,ILogger logger,GCPPubSubSetting setting)
        {
            QuoteInfo = quoteInfo;
            _logger = logger;
            _logger.Debug("[Quote.InitTickBroker()] {symbol} Create fanout of the GCP PubSub...", QuoteInfo.Symbol);
            _queueFanout  = new QueueConnectionClient(new GCPPubSubService(setting.ProjectID,_logger));
            _queueFanout.FanoutConn.InitTopic(Name).Wait();
            _logger.Debug("[Quote.InitTickBroker()] {symbol} Create fanout of the GCP PubSub...done", QuoteInfo.Symbol);
            InitTickBroker();
        }

        static OHLC Accumulate(OHLC state, Tick tick)
        {
            var price = tick.DealPrice;
            var volume = tick.DealQty;
            // var dt = tick.LocalTime.ToDateTime();
            var dt = tick.LocalTime;
            // Take the current values & apply the price update.    
            state.Open = state.Open ?? price;
            state.High = state.High.HasValue ? state.High > price ? state.High : price : price;
            state.Low = state.Low.HasValue ? state.Low < price ? state.Low : price : price;
            state.Close = price;
            state.Volume += volume;
            state.TicksCount += 1;
            state.TicksArr.Add(tick);
            //state.LocalTime = Timestamp.FromDateTime(dt.ToUniversalTime());
            state.LocalTime = dt;
            return state;
        }
        Task Publish(OHLC ohlc)
        {
            return Task.Run(() =>
            {
                LastOHLC = ohlc;
                //LastOHLC.LogOHLC();
                var ohlcstr = LastOHLC.SerializeToString_PB();
                //Console.WriteLine(ohlcstr);
                _queueFanout.FanoutConn.Send(ohlcstr);
                //QueueConn?.Send(LastOHLC.SerializeToString_PB());
            });
        }

        public void InitTickBroker()
        {
            _logger.Debug("[Quote.InitTickBroker()] {symbol}",QuoteInfo.Symbol);
            DataBroker = new DataEventBroker<Tick>();
            //DataBroker
            //    .WindowByTimestamp(x => x.LocalTime.ToDateTime().Ticks, OHLCInterval)
            //    .SelectMany(window => window.Aggregate(new OHLC(),
            //        (state, tick) => Accumulate(state, tick.DealPrice, tick.DealQty, tick.LocalTime.ToDateTime())))
            //    .Subscribe(async ohlc => await Publish(ohlc));
            DataBroker
                .WindowByTimestamp(x => x.LocalTime.ToDateTime().Ticks, OHLCInterval)
                .SelectMany(window => window.Aggregate(new OHLC(),
                    (state, tick) => Accumulate(state, tick)))
                .Subscribe(async ohlc => await Publish(ohlc));
        }

        public void Dispose()
        {
            DataBroker.Close();
            _queueFanout.Dispose();
        }
    }
}