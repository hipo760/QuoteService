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

            Post("/Connect", parameters =>
            {
                return fcmAPI.Connect().Result
                    ? HttpStatusCode.Accepted
                    : HttpStatusCode.BadRequest;
            });

            Post("/Reconnect", parameters =>
            {
                return fcmAPI.Reconnect().Result
                    ? HttpStatusCode.Accepted
                    : HttpStatusCode.BadRequest;
            });

            Post("/Disconnect", _ => { return fcmAPI.Disconnect(); });


            Get("/Quote", _ => String.Join(",", fcmAPI.QuotesList.ToArray()));

            Post("/Quote/{exchange}/{symbol}", parameters =>
            {

                var exchange = (string) parameters.exchange;
                var symbol = (string) parameters.symbol;
                return fcmAPI.AddQuote(exchange, symbol).Result
                    ? HttpStatusCode.Accepted
                    : HttpStatusCode.BadRequest;

            });
            Delete("/Quote/{exchange}/{symbol}", parameters =>
            {
                var exchange = (string) parameters.exchange;
                var symbol = (string) parameters.symbol;
                return fcmAPI.RemoveQuote(exchange, symbol).Result
                    ? HttpStatusCode.Accepted
                    : HttpStatusCode.BadRequest;

            });
            Delete("/Quote", parameters => { return fcmAPI.RemoveAllQuotes(); });
        }
    }
}