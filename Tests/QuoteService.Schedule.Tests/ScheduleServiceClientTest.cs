using System;
using System.Collections.Generic;
//using Castle.Core.Logging;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Moq;
using QuoteResearch.HealthCheck;
using QuoteResearch.Service.ScheduleService;
using QuoteResearch.Service.Share.Type;
using Serilog;
using Xunit;

namespace QuoteService.Schedule.Tests
{
    public class ScheduleServiceClientTest
    {
        Mock<ILogger> mockIlog = new Mock<Serilog.ILogger>();
        [Theory]
        [InlineData(HealthCheckResponse.Types.ServingStatus.NotServing, false)]
        [InlineData(HealthCheckResponse.Types.ServingStatus.Unknown, false)]
        [InlineData(HealthCheckResponse.Types.ServingStatus.Serving, true)]
        public void IsScheduleServiceOnline_ReturnScheduleServiceOnline(
            HealthCheckResponse.Types.ServingStatus status,
            bool expected)
        {
            HealthCheckResponse healthResponse = new HealthCheckResponse() {Status = status};
            
            
            var mockScheduleClient = new Mock<ScheduleService.ScheduleServiceClient>();
            var mockHealthClient = new Mock<Health.HealthClient>();
            mockHealthClient.Setup(hc => hc.Check(
                It.IsAny<HealthCheckRequest>(),
                It.IsAny<CallOptions>()
            )).Returns(healthResponse);
            var scheduleClientAction =
                new ScheduleServiceClientAction(mockIlog.Object, mockScheduleClient.Object, mockHealthClient.Object);
            Assert.Equal(scheduleClientAction.IsScheduleServiceOnline, expected);
        }

        public static IEnumerable<object[]> UpdateTimeTestData =>
            new List<object[]>
            {
                new object[] { new DateTime(2019, 1, 1, 12, 0, 0), new DateTime(2019, 1, 1, 12, 0, 0), false },
                new object[] { new DateTime(2019, 1, 1, 9, 0, 0), new DateTime(2019, 1, 1, 12, 0, 0), false },
                new object[] { new DateTime(2019, 1, 1, 13, 0, 0), new DateTime(2019, 1, 1, 12, 0, 0), true },
            };
        [Theory]
        [MemberData(nameof(UpdateTimeTestData))]
        public void IsLastSyncDate_ReturnLastDayKeepAsync(
            DateTime serverTime, 
            DateTime localTime, 
            bool expected)
        {
            var mockScheduleClient = new Mock<ScheduleService.ScheduleServiceClient>();
            mockScheduleClient.Setup(sc => sc.GetScheduleUpdateTime(
                It.IsAny<EmptyRequest>(),
                It.IsAny<CallOptions>()
                )).Returns(Timestamp.FromDateTime(serverTime.ToUniversalTime()));
            var mockHealthClient = new Mock<Health.HealthClient>();
            var scheduleClientAction =
                new ScheduleServiceClientAction(mockIlog.Object, mockScheduleClient.Object, mockHealthClient.Object){LastUpdateTime = Timestamp.FromDateTime(localTime.ToUniversalTime())};
            Assert.Equal(scheduleClientAction.IsLastSyncDate, expected);
        }

        //[Fact]
        //public void UpdateScheduleList_Fail_IfUpdateTimeKeepSync()
        //{
        //    Timestamp ServerLastUpdateTime = Timestamp.FromDateTime(new DateTime(2019,01,01,12,0,0));
        //    HealthCheckResponse healthResponse = new HealthCheckResponse(){Status = HealthCheckResponse.Types.ServingStatus.Serving};

        //    var mockScheduleClient = new Mock<ScheduleService.ScheduleServiceClient>();
        //    mockScheduleClient.Setup(sc => sc.GetScheduleUpdateTime(
        //        It.IsAny<EmptyRequest>(),
        //        It.IsAny<CallOptions>()
        //        )).Returns(ServerLastUpdateTime);

        //    var mockHealthClient = new Mock<Health.HealthClient>();
        //    mockHealthClient.Setup(hc => hc.Check(
        //        It.IsAny<HealthCheckRequest>(),
        //        It.IsAny<CallOptions>()
        //    )).Returns(healthResponse);

        //    var scheduleClientAction = new ScheduleServiceClientAction(mockScheduleClient.Object, mockHealthClient.Object);
        //    Assert.False(scheduleClientAction.UpdateScheduleList().Result);
        //}



    }
}