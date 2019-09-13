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
        public TimeSpan OHLCInterval { get; set; } = TimeSpan.FromMinutes(30);

        public DataEventBroker<Tick> DataBroker { get; set; }
        public IQueueFanout QueueConn { get; set; }
        public OHLC LastOHLC { get; set; }
        private ILogger _logger;
        public Quote(QuoteInfo quoteInfo,ILogger logger)
        {
            QuoteInfo = quoteInfo;
            _logger = logger;
            InitTickBroker();
        }

        static OHLC Accumulate(OHLC state, float price, int volume, DateTime dt)
        {
            // Take the current values & apply the price update.    
            state.Open = state.Open ?? price;
            state.High = state.High.HasValue ? state.High > price ? state.High : price : price;
            state.Low = state.Low.HasValue ? state.Low < price ? state.Low : price : price;
            state.Close = price;
            state.Volume += volume;
            state.TicksCount += 1;
            state.LocalTime = Timestamp.FromDateTime(dt.ToUniversalTime());
            return state;
        }
        Task Publish(OHLC ohlc)
        {
            return Task.Run(() =>
            {
                LastOHLC = ohlc;
                //LastOHLC.LogOHLC();
                LastOHLC.ToString();
                Console.WriteLine(LastOHLC.SerializeToString_PB());
                //QueueConn?.Send(LastOHLC.SerializeToString_PB());
            });
        }

        public void InitTickBroker()
        {
            _logger.Debug("[Quote.InitTickBroker()] {symbol}",QuoteInfo.Symbol);
            DataBroker = new DataEventBroker<Tick>();
            DataBroker
                .WindowByTimestamp(x => x.LocalTime.ToDateTime().Ticks, OHLCInterval)
                .SelectMany(window => window.Aggregate(new OHLC(),
                    (state, tick) => Accumulate(state, tick.DealPrice, tick.DealQty, tick.LocalTime.ToDateTime())))
                .Subscribe(async ohlc => await Publish(ohlc));
        }

        public void Dispose()
        {
            DataBroker.Close();
            QueueConn.Dispose();
        }
    }
}