using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Polly;
using QuoteService.FCMAPI;
using QuoteService.GRPC;

namespace SKAPI
{
    public partial class SKAPIConnection
    {
        // Policy

        private void ExecuteRetryLoginPolicy(int retry)
        {
            var connStatusPolicy = Policy.HandleResult<int>(status => (status != 0 && status != 2003))
                .WaitAndRetry(retry, retryAttempt => TimeSpan.FromSeconds(1));
            connStatusPolicy.Execute(() =>
            {
                _logger.Debug("[SKAPIConnection.ExecuteRetryLoginPolicy()] Wait for reply message");
                _loginCode = _skCenter.SKCenterLib_Login(_apiSetting.ID, _apiSetting.Password);
                _logger.Debug($"[SKAPIConnection.ExecuteRetryLoginPolicy()] {_skCenter.SKCenterLib_GetReturnCodeMessage(_loginCode)}");
                return _loginCode;
            });
        }

        private bool ExecuteRetryAddRequestTickPolicy(int retry, string symbol, ref short pageNo)
        {
            int apiReturnCode = -1;
            short tickPage = pageNo;
            var connStatusPolicy = Policy.HandleResult<int>(page => (page < 0 || page > 50 || apiReturnCode != 0))
                .WaitAndRetry(retry, retryAttempt => TimeSpan.FromSeconds(1));
            var returnPageNo = connStatusPolicy.Execute(() =>
            {
                //tickPage += (short)1;
                _logger.Debug($"[SKAPIConnection.ExecuteRetryAddRequestTickPolicy()] SKQuoteLib_RequestTicks...retry {retry} pageNo {tickPage}" );
                apiReturnCode = _skQuotes.SKQuoteLib_RequestTicks(ref tickPage, symbol);
                _logger.Debug($"[SKAPIConnection.ExecuteRetryAddRequestTickPolicy()] SKQuoteLib_RequestTicks {apiReturnCode}, {_skCenter.SKCenterLib_GetReturnCodeMessage(apiReturnCode)}...");
                _logger.Debug($"[SKAPIConnection.ExecuteRetryAddRequestTickPolicy()] SKQuoteLib_RequestTicks got page no: {tickPage}...");
                Thread.Sleep(_apiSetting.SKServerLoadingTime);
                return tickPage;
            });
            pageNo = tickPage;
            if (apiReturnCode != 0 || returnPageNo < 0) return false;
            return true;
        }

        private bool ExecuteRetryRemoveRequestTickPolicy(int retry, string symbol)
        {
            int apiReturnCode = -1;
            short tickPage = 50;
            var connStatusPolicy = Policy.HandleResult<int>(page => (page < 0 || page > 50 || apiReturnCode != 0))
                .WaitAndRetry(retry, retryAttempt => TimeSpan.FromSeconds(1));
            var returnPageNo = connStatusPolicy.Execute(() =>
            {
                _logger.Debug($"[SKAPIConnection.ExecuteRetryRemoveRequestTickPolicy()] Use page 50 to cancel SKQuoteLib_RequestTicks");
                apiReturnCode = _skQuotes.SKQuoteLib_RequestTicks(ref tickPage, symbol);
                _logger.Debug($"[SKAPIConnection.ExecuteRetryRemoveRequestTickPolicy()] SKQuoteLib_RequestTicks {apiReturnCode}, {_skCenter.SKCenterLib_GetReturnCodeMessage(apiReturnCode)}...");
                _logger.Debug($"[SKAPIConnection.ExecuteRetryRemoveRequestTickPolicy()] SKQuoteLib_RequestTicks got page no: {tickPage}...");
                Thread.Sleep(_apiSetting.SKServerLoadingTime);
                return tickPage;
            });
            if (apiReturnCode != 0 || returnPageNo < 0) return false;
            return true;
        }

        private void ExecuteWaitingConnectionReadyPolicy(int retry)
        {
            var connStatusPolicy = Policy
                .HandleResult<ConnectionStatus>(status => status != ConnectionStatus.ConnectionReady)
                .WaitAndRetry(retry, retryAttempt => TimeSpan.FromSeconds(1));
            connStatusPolicy.Execute(() =>
            {
                _logger.Debug("[SKAPIConnection.ExecuteConnectionReadyPolicy()] Server not ready, need more time...");
                return APIStatus;
            });
        }
    }
}
