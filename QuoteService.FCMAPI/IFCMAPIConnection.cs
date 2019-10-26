using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using QuoteService.QuoteData;
using QRType = QuoteResearch.Service.Share.Type;


namespace QuoteService.FCMAPI
{
    public enum ConnectionStatus
    {
        NotConnected,
        Connecting,
        ConnectionReady,
        ConnectionError,
        Unknown
    }    
    
    /// <summary>
    /// Futures Commission Merchant Quote API.
    /// </summary>
    public interface IFCMAPIConnection: IDisposable
    {
        // Connection
        ConnectionStatus APIStatus { get;}
        Task InitAPI();
        Task<bool> Connect();
        Task<bool> Reconnect();
        Task Disconnect();

        // Quote Action
        List<QRType.Quote> QuotesList { get; }
        Task<bool> AddQuote(QRType.Quote quote);
        Task<bool> RemoveQuote(QRType.Quote quote);
        Task RemoveAllQuotes();
    }
}