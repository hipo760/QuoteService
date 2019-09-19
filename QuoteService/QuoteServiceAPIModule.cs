using System;
using Nancy;
using QuoteService.FCMAPI;

namespace QuoteService
{
    public class QuoteServiceAPIModule : NancyModule
    {
        private static readonly Object obj = new Object();
        public QuoteServiceAPIModule(IFCMAPIConnection fcmAPI)
        {

            Get("/", _ => "Quote Service");

            Get("/Connect/Status", _ => fcmAPI.APIStatus.ToString());

            Post("/Connect",  parameters =>
            {
                lock (obj)
                {
                    return fcmAPI.Connect().Result
                        ? HttpStatusCode.Accepted
                        : HttpStatusCode.BadRequest;
                }
                
            });

            Post("/Reconnect",  parameters =>
            {
                lock (obj)
                {
                    return fcmAPI.Reconnect().Result
                        ? HttpStatusCode.Accepted
                        : HttpStatusCode.BadRequest;
                }
            });

            Post("/Disconnect", _ =>
            {
                lock (obj)
                {
                    return fcmAPI.Disconnect();
                }
            });


            Get("/Quote", _ => String.Join(",", fcmAPI.QuotesList.ToArray()));

            Post("/Quote/{exchange}/{symbol}",  parameters =>
            {
                lock(obj)
                {
                    var exchange = (string) parameters.exchange;
                    var symbol = (string) parameters.symbol;
                    return fcmAPI.AddQuote(exchange, symbol).Result
                        ? HttpStatusCode.Accepted
                        : HttpStatusCode.BadRequest;
                }
            });
            Delete("/Quote/{exchange}/{symbol}",  parameters =>
            {
                lock (obj)
                {
                    var exchange = (string) parameters.exchange;
                    var symbol = (string) parameters.symbol;
                    return fcmAPI.RemoveQuote(exchange, symbol).Result
                        ? HttpStatusCode.Accepted
                        : HttpStatusCode.BadRequest;
                }
            });
            Delete("/Quote", parameters =>
            {
                lock (obj)
                {
                    return fcmAPI.RemoveAllQuotes();
                }
            });
        }
    }
}