using System;
using System.IO;
using Polly;
using QuoteService.FCMAPI;
using Serilog;

namespace SKAPI
{
    public static class ExtensionMethods
    {
        internal static void ExecuteWaitingConnectionReadyPolicy(this ConnectionStatus connStatus,int retry, IFCMAPIConnection connection, ILogger logger)
        {
            var connStatusPolicy = Policy.HandleResult<ConnectionStatus>(status => status != ConnectionStatus.ConnectionReady)
                .WaitAndRetry(retry, retryAttempt => TimeSpan.FromSeconds(1));
            connStatusPolicy.Execute(() =>
            {
                logger.Debug("[SKAPIConnection.ExecuteConnectionReadyPolicy()] Server not ready, need more time...");
                return connection.APIStatus;
            });
        }
    }
}