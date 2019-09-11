using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using QuoteData;

namespace FCMAPI
{
    /// <summary>
    /// Futures Commission Merchant Quote API.
    /// </summary>
    public interface IFCMAPIConnection: IDisposable
    {
        // Connection
        ConnectionStatus APIStatus { get;}
        Task<bool> Connect();
        Task<bool> Reconnect();
        Task Disconnect();

        // Quote Action
        List<Quote> QuotesList { get; }
        Task<bool> AddQuote(string exchange, string symbol);
        Task<bool> CloseQuote(string exchange, string symbol);
        Task<bool> RemoveQuote(string exchange, string symbol);
        Task RemoveAllQuotes();
    }
}