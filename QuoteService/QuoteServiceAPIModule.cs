using Nancy;
using QuoteService.FCMAPI;

namespace QuoteService
{
    public class QuoteServiceAPIModule : NancyModule
    {
        public QuoteServiceAPIModule(IFCMAPIConnection fcmAPI)
        {
            Get("/", _ => "Quote Service");

            Get("/Connect/Status", _ => fcmAPI.APIStatus.ToString());

            Post("/Connect", async parameters =>
            {
                return await fcmAPI.Connect()
                    ? HttpStatusCode.Accepted
                    : HttpStatusCode.BadRequest;
            });

            Post("/Reconnect", async parameters =>
            {
                return await fcmAPI.Reconnect()
                    ? HttpStatusCode.Accepted
                    : HttpStatusCode.BadRequest;
            });

            Post("/Disconnect", _ => fcmAPI.Disconnect());


            Get("/Quote", _ => fcmAPI.QuotesList.ToArray());

            Post("/Quote/{exchange}/{symbol}", async parameters =>
            {
                var exchange = (string)parameters.exchange;
                var symbol = (string)parameters.symbol;
                return await fcmAPI.AddQuote(exchange, symbol)
                    ? HttpStatusCode.Accepted
                    : HttpStatusCode.BadRequest;
            });
            Delete("/Quote/{exchange}/{symbol}", async parameters =>
            {
                var exchange = (string)parameters.exchange;
                var symbol = (string)parameters.symbol;
                return await fcmAPI.RemoveQuote(exchange, symbol)
                    ? HttpStatusCode.Accepted
                    : HttpStatusCode.BadRequest;
            });
            Delete("/Quote", async parameters => await fcmAPI.RemoveAllQuotes());
        }
    }
}