using System;
using System.Threading.Tasks;

namespace QuoteService.Queue
{
    //public interface IQueueRequestConnection : IDisposable
    //{
    //    //Task Send(string message);
    //}
    public interface IQueueFanoutPublisher : IDisposable
    {
        Task InitTopic(string topicID);
        Task Send(byte[] message);
    }

    public interface IQueueFanoutReceiver : IDisposable
    {
        event EventHandler<byte[]> ReceivedMessageEvent;
        Task InitListening(string topicID);
    }

    public interface IQueueService
    {
        Task<IQueueFanoutPublisher> GetQueueFanoutPublisher();
        Task<IQueueFanoutReceiver> GetQueueFanoutReceiver();
    }

    public class QueueConnectionClient:IDisposable
    {
        private IQueueFanoutPublisher _fanoutPublisher;
        private IQueueFanoutReceiver _fanoutReceiver;

        public IQueueFanoutReceiver FanoutReceiver
        {
            get => _fanoutReceiver;
            set => _fanoutReceiver = value;
        }

        public IQueueFanoutPublisher FanoutPublisher
        {
            get => _fanoutPublisher;
            set => _fanoutPublisher = value;
        }

        //public IQueueRequestConnection RequestConn
        //{
        //    get => _requestConn;
        //    set => _requestConn = value;
        //}

        //private IQueueRequestConnection _requestConn;

        public QueueConnectionClient(IQueueService queueService)
        {
            _fanoutPublisher = queueService.GetQueueFanoutPublisher().Result;
            _fanoutReceiver = queueService.GetQueueFanoutReceiver().Result;

            //_requestConn = queueService.GetQueueRequestConnection().Result;
        }

        public void Dispose()
        {
            _fanoutPublisher?.Dispose();
            //_requestConn?.Dispose();
        }
    }
}