using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Google.Protobuf.WellKnownTypes;
using Hangfire;
using Hangfire.MemoryStorage;
using Hangfire.SQLite;
using QuoteResearch.Service.Share.Type;
using QuoteService.FCMAPI;
using QuoteService.Queue;
using Serilog;

namespace QuoteService.Schedule
{
    
    public class QuoteScheduleSetting
    {
        public string ScheduleService { get; set; }
        public string ScheduleTopic { get; set; }
    }
    
    
    public class QuoteServiceSchedule
    {
        private IFCMAPIConnection _conn;
        private ILogger _log;
        private QueueConnectionClient _scheduleFanout;
        
        private BackgroundJobServer _jobServer;
        private ScheduleServiceClientAction _scheduleServiceClient;

        
        public QuoteServiceSchedule(IFCMAPIConnection conn, ILogger log, ScheduleServiceClientAction scheduleServiceClient, QueueConnectionClient queueFanout)
        {
            
            
            _conn = conn;
            _log = log;
            //_setting = setting;
            
            _log.Debug("[QuoteServiceSchedule.ctor()]");
            // Initial the job server.
            _log.Debug("[QuoteServiceSchedule.ctor()] Set memory storage.");
            GlobalConfiguration.Configuration.UseMemoryStorage();
            _log.Debug("[QuoteServiceSchedule.ctor()] Set background job server.");
            _jobServer = new BackgroundJobServer(new BackgroundJobServerOptions() { WorkerCount = 1 });
            // Restore the schedule.
            _scheduleServiceClient =
                scheduleServiceClient ?? throw new ArgumentNullException(nameof(scheduleServiceClient));
            
            _log.Debug("[QuoteServiceSchedule.ctor()] Restore the schedule...");
            RestoreSchedule();
            _log.Debug("[QuoteServiceSchedule.ctor()] Restore the schedule...done.");

            // Listen to the UpdateSchedule event.    
            _scheduleFanout = queueFanout ?? throw new ArgumentNullException(nameof(scheduleServiceClient));
            //_scheduleFanout.FanoutReceiver.InitListening(_setting.ScheduleTopic).Wait();
            _scheduleFanout.FanoutReceiver.ReceivedMessageEvent += FanoutReceiver_ReceivedMessageEvent;
        }

        private void AddQuoteSchedule(QuoteSchedule quoteSchedule)
        {
            var start = quoteSchedule.StartTime.ToDateTimeOffset().ToLocalTime();
            var end = quoteSchedule.EndTime.ToDateTimeOffset().ToLocalTime();

            if (DateTimeOffset.Now > end) return;

            start = start.AddMinutes(-10.0);
            end = end.AddMinutes(5.0);

            // Schedule the start of the quote.
            BackgroundJob.Schedule(() => _conn.AddQuote(null), start);
            // Schedule the end of the quote.
            BackgroundJob.Schedule(() => _conn.RemoveQuote(quoteSchedule.Quote), end);
        }

        private void FanoutReceiver_ReceivedMessageEvent(object sender, byte[] e)
        {
            var UpdateScheduleNotifyMessageTimestamp = Timestamp.Parser.ParseFrom(e);
            RestoreSchedule();
        }

        public void RestoreSchedule()
        {
            _log.Debug("[QuoteServiceSchedule.RestoreSchedule()] Update schedule list...");
            _scheduleServiceClient.UpdateScheduleList();
            _log.Debug("[QuoteServiceSchedule.RestoreSchedule()] Update schedule list...done.");
            _scheduleServiceClient.ScheduleList.ForEach(x => { AddQuoteSchedule(x); });
        }

    }
}
