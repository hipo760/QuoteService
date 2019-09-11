using System;

namespace Queue
{
    public interface IQueueFanout:IDisposable
    {
        void Send(string message);
    }
}