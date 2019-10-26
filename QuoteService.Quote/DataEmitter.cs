using System;
using System.Configuration;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Microsoft.Extensions.Configuration;
using QuoteResearch.Service.Share.Data.Stream;
using QuoteService.Queue;
using QuoteService.Queue.RabbitMQ;
using QuoteService.QuoteData;
using Serilog;
using QRType = QuoteResearch.Service.Share.Type;

namespace QuoteService.Quote
{
    //public class QuoteInfo
    //{
    //    public string ApiSource { get; set; }
    //    public string Exchange { get; set; }
    //    public string Symbol { get; set; }
    //}
    public class DataEmitter : IDisposable
    {
        public QRType.Quote QuoteInfo { get; set; }

        public string Name => QuoteInfo.Exchange + "." + QuoteInfo.Symbol;
        public TimeSpan OHLCInterval { get; set; } = TimeSpan.FromSeconds(1);
        public DataEventBroker<Tick> DataBroker { get; set; }
        //public QueueConnectionClient QueueConn { get; set; }
        public OHLC LastOHLC { get; set; }

        private ILogger _logger;
        private QueueConnectionClient _queueFanout;

        public DataEmitter(QRType.Quote quoteInfo,ILogger logger,IConfiguration config)//,GCPPubSubSetting setting)
        {
            QuoteInfo = quoteInfo;
            _logger = logger;
            _logger.Debug("[Quote.InitTickBroker()] {symbol} Create fanout on RabbitMQ...", QuoteInfo.Symbol);
            _queueFanout  = new QueueConnectionClient(new RabbitQueueService(logger,config));
            _queueFanout.FanoutPublisher.InitTopic(Name).Wait();
            _logger.Debug("[Quote.InitTickBroker()] {symbol} Create fanout on RabbitMQ...done", QuoteInfo.Symbol);
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
            state.TicksArr.Add(new TickElement(){DealPrice = price,DealQty = volume});
            //state.LocalTime = Timestamp.FromDateTime(dt.ToUniversalTime());
            state.LocalTime = dt;
            return state;
        }
        Task Publish(OHLC ohlc)
        {
            return Task.Run(() =>
            {
                LastOHLC = ohlc;
                _queueFanout.FanoutPublisher.Send(LastOHLC.ToByteArray());
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
                    (state, tick) => Accumulate(state, tick)))
                .Subscribe(async ohlc => await Publish(ohlc));
        }

        public void Dispose()
        {
            _logger.Debug("[Quote.Dispose()] {symbol} Complete data broker.", QuoteInfo.Symbol);
            DataBroker.Close();
            _logger.Debug("[Quote.Dispose()] {symbol} Release queue resources", QuoteInfo.Symbol);
            //_queueFanout.Dispose();
        }
    }
}