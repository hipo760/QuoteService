using System;

namespace QuoteService.Queue
{
    public interface IQueueFanout:IDisposable
    {
        void Send(string message);
    }
}