using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using QuoteResearch.HealthCheck;
using QuoteResearch.Service.ScheduleService;
using QuoteResearch.Service.Share.Type;
using Serilog;

namespace QuoteService.Schedule
{
    public class ScheduleServiceClientAction
    {
        private ScheduleService.ScheduleServiceClient _scheduleClient;
        private Health.HealthClient _healthClient;
        private ILogger _log;
        private GetQuoteScheduleListResponse _lastResponse;

        public ScheduleService.ScheduleServiceClient ScheduleClient
        {
            get => _scheduleClient;
            set => _scheduleClient = value;
        }

        public Health.HealthClient HealthClient
        {
            get => _healthClient;
            set => _healthClient = value;
        }
        public Timestamp LastUpdateTime { get; set; }
        public List<QuoteSchedule> ScheduleList { get; set; }

        public ScheduleServiceClientAction(ILogger log, ScheduleService.ScheduleServiceClient scheduleClient ,Health.HealthClient healthClient)
        {
            _log = log;
            ScheduleClient = scheduleClient ?? throw new ArgumentNullException(nameof(scheduleClient));
            HealthClient = healthClient ?? throw new ArgumentNullException(nameof(healthClient));
            ScheduleList = new List<QuoteSchedule>();
        }

        public void InitScheduleList()
        {
            LoadLastResponseFromFile();
            UpdateScheduleList();
        }

        private void LoadLastResponseFromFile()
        {
            string path = @"schedule.json";
            if (File.Exists(path))
            {
                using (FileStream fs = File.OpenRead(path))
                {
                    _lastResponse = GetQuoteScheduleListResponse.Parser.ParseFrom(fs);
                }
            }
            else
            {
                var dt = new DateTime(year:2019,month:1,day:1);
                var ts = Timestamp.FromDateTime(dt);
                _lastResponse = new GetQuoteScheduleListResponse(){UpdateTime = ts};
                WriteResponseToFile();
            }

        }

        public Task UpdateScheduleList()
        {
            return Task.Run(() =>
            {
                if (!IsScheduleServiceOnline) return;
                if(!IsLastSyncDate) return;
                var getScheduleListResponse = _scheduleClient.GetScheduleListAsync(new EmptyRequest());
                _lastResponse = getScheduleListResponse.ResponseAsync.Result;
                ScheduleList = _lastResponse.Schedule.ToList();
                WriteResponseToFile();
            });
        }

        public bool IsScheduleServiceOnline
        {
            get
            {
                try
                {
                    return (_healthClient.Check(new HealthCheckRequest(), new CallOptions()).Status ==
                            HealthCheckResponse.Types.ServingStatus.Serving);
                }
                catch (Exception e)
                {
                    _log.Debug("[ScheduleServiceClientAction.IsScheduleServiceOnline]");
                    return false;
                }
            }
        }

        public bool IsLastSyncDate 
            => (_scheduleClient.GetScheduleUpdateTime(new EmptyRequest(), new CallOptions()) > LastUpdateTime);

        private void WriteResponseToFile()
        {
            string path = @"schedule.json";

            // Delete the file if it exists.
            if (File.Exists(path))
            {
                File.Delete(path);
            }
            //Create the file.
            using (FileStream fs = File.Create(path))
            {
                var info = _lastResponse.ToByteArray();
                fs.Write(info, 0, info.Length);
            }
        }

    }
}