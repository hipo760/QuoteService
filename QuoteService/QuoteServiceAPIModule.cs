using Nancy;
using QuoteService.FCMAPI;

namespace QuoteService
{
    public class QuoteServiceAPIModule : NancyModule
    {
        public QuoteServiceAPIModule(IFCMAPIConnection fcmAPI)
        {
            Get("/", _ => "Quote Service");
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
            Post("/history/{exchange}/{symbol}", async parameters =>
            {
                var exchange = (string)parameters.exchange;
                var symbol = (string)parameters.symbol;
                return await fcmAPI.AddQuote(exchange, symbol)
                    ? HttpStatusCode.Accepted
                    : HttpStatusCode.BadRequest;
            });
            Delete("/history/{exchange}/{symbol}", async parameters =>
            {
                var exchange = (string)parameters.exchange;
                var symbol = (string)parameters.symbol;
                return await fcmAPI.RemoveQuote(exchange, symbol)
                    ? HttpStatusCode.Accepted
                    : HttpStatusCode.BadRequest;
            });
        }
    }
}