using System;
using System.Threading.Tasks;

namespace QuoteService.Queue
{
    public interface IQueueRequestConnection : IDisposable
    {
        //Task Send(string message);
    }
    public interface IQueueFanoutConnection : IDisposable
    {
        Task InitTopic(string topicID);
        Task Send(string message);
    }

    public interface IQueueService
    {
        Task<IQueueFanoutConnection> GetQueueFanoutConnection();
        //Task<IQueueRequestConnection> GetQueueRequestConnection();
    }

    public class QueueConnectionClient:IDisposable
    {
        private IQueueFanoutConnection _fanoutConn;

        public IQueueFanoutConnection FanoutConn
        {
            get => _fanoutConn;
            set => _fanoutConn = value;
        }

        public IQueueRequestConnection RequestConn
        {
            get => _requestConn;
            set => _requestConn = value;
        }

        private IQueueRequestConnection _requestConn;

        public QueueConnectionClient(IQueueService queueService)
        {
            _fanoutConn = queueService.GetQueueFanoutConnection().Result;
            //_requestConn = queueService.GetQueueRequestConnection().Result;
        }

        public void Dispose()
        {
            _fanoutConn?.Dispose();
            _requestConn?.Dispose();
        }
    }
}