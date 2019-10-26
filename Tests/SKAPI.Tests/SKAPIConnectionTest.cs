using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Moq;
using QuoteService.FCMAPI;
using QuoteService.QuoteData;
using Serilog;
using SKCOMLib;
using Xunit;


namespace SKAPI.Tests
{
    public class SKAPIConnectionTest
    {
        Mock<ILogger> logger = new Mock<ILogger>();
        Mock<DataEventBroker<ConnectionStatusEvent>> connStatusBroker = new Mock<DataEventBroker<ConnectionStatusEvent>>();
        Mock<IConfiguration> config = new Mock<IConfiguration>();

        [Fact]
        public void Connect_ConnectionStatus_Ready_WhenSKCOMRaise3003()
        {
            // Arrange
            var skApi = new Mock<SkapiWrapper>();

            skApi.Setup(c => c.SKCenterLib_Login(
                It.IsAny<string>(),
                It.IsAny<string>()
            )).Returns(0);

            skApi.Setup(q => q.SKQuoteLib_EnterMonitor()).Returns(0); 
            
            skApi.SetupAdd(q => q.OnConnectionEvent += It.IsAny<_ISKQuoteLibEvents_OnConnectionEventHandler>());
            skApi.SetupRemove(q => q.OnConnectionEvent -= It.IsAny<_ISKQuoteLibEvents_OnConnectionEventHandler>());
            skApi.SetupAdd(q => q.OnNotifyTicksEvent += It.IsAny<_ISKQuoteLibEvents_OnNotifyTicksEventHandler>());
            skApi.SetupRemove(q => q.OnNotifyTicksEvent -= It.IsAny<_ISKQuoteLibEvents_OnNotifyTicksEventHandler>());
            skApi.SetupAdd(q => q.OnNotifyHistoryTicksEvent += It.IsAny<_ISKQuoteLibEvents_OnNotifyHistoryTicksEventHandler>());
            skApi.SetupRemove(q => q.OnNotifyHistoryTicksEvent -= It.IsAny<_ISKQuoteLibEvents_OnNotifyHistoryTicksEventHandler>());

            // Act
            SKAPIConnection conn = new SKAPIConnection(
                logger.Object,
                skApi.Object,
                connStatusBroker.Object,
                config.Object
                );
            var connectResult = Task.Run(async () => await conn.Connect().ConfigureAwait(false));
            skApi.Raise(q => q.OnConnectionEvent += null, 3001, 3001);
            Thread.Sleep(TimeSpan.FromSeconds(5));
            skApi.Raise(q => q.OnConnectionEvent += null, 3003, 3003);

            // Assert
            conn.APIStatus.Should().Be(ConnectionStatus.ConnectionReady, "3003");
            connectResult.Result.Should().Be(true);
        }

        [Fact]
        public void Connect_ConnectionStatus_NotConnect_LoginFailed()
        {
            // Arrange
            var skApi = new Mock<SkapiWrapper>();

            skApi.Setup(c => c.SKCenterLib_Login(
                It.IsAny<string>(),
                It.IsAny<string>()
            )).Returns(-1);

            // Act
            SKAPIConnection conn = new SKAPIConnection(
                logger.Object,
                skApi.Object,
                connStatusBroker.Object,
                config.Object
            );
            var connectResult = Task.Run(async () => await conn.Connect().ConfigureAwait(false));
            // Assert
            conn.APIStatus.Should().Be(ConnectionStatus.NotConnected, "Login failed.");
            connectResult.Result.Should().Be(false);
        }
        [Fact]
        public void Connect_ConnectionStatus_NotConnect_SolaceServerFailed()
        {
            // Arrange
            var skApi = new Mock<SkapiWrapper>();

            skApi.Setup(c => c.SKCenterLib_Login(
                It.IsAny<string>(),
                It.IsAny<string>()
            )).Returns(0);

            skApi.Setup(q => q.SKQuoteLib_EnterMonitor()).Returns(-1);

            // Act
            SKAPIConnection conn = new SKAPIConnection(
                logger.Object,
                skApi.Object,
                connStatusBroker.Object,
                config.Object
            );
            var connectResult = Task.Run(async () => await conn.Connect().ConfigureAwait(false));
            // Assert
            conn.APIStatus.Should().Be(ConnectionStatus.NotConnected, "SolaceServerFailed");
            connectResult.Result.Should().Be(false);
        }

        [Fact]
        public void Reconnect_Failed_WhenConnectFailed()
        {
            // Arrange
            var skApi = new Mock<SkapiWrapper>();
            skApi.Setup(c => c.SKCenterLib_Login(
                It.IsAny<string>(),
                It.IsAny<string>()
            )).Returns(0);

            skApi.Setup(q => q.SKQuoteLib_EnterMonitor()).Returns(-1);
            
            // Act
            SKAPIConnection conn = new SKAPIConnection(
                logger.Object,
                skApi.Object,
                connStatusBroker.Object,
                config.Object
            );
            var connectResult = Task.Run(async () => await conn.Reconnect().ConfigureAwait(false));
            
            // Assert
            connectResult.Result.Should().Be(false);
        }

    }
}
