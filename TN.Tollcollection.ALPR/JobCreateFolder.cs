using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using cm;
using Quartz;
using Quartz.Impl;

namespace TN.Tollcollection.ALPR
{
    /// <summary>
    /// Tự động tạo thư mục theo đường dẫn và thời gian .
    /// </summary>
    class JobCreateFolder
    {
        private static IScheduler scheduler;

        /// <summary>
        /// Bắt đầu công việc tạo thư mục.
        /// </summary>
        public static void Start()
        {
            try
            {
                scheduler = StdSchedulerFactory.GetDefaultScheduler();
                scheduler.Start();
                //Job capture frames
                
                IJobDetail job = JobBuilder.Create<CreateFolder>().Build();
                ITrigger restartTrigger = TriggerBuilder.Create()
                    .StartNow()
                    .WithDailyTimeIntervalSchedule(x => x
                        .StartingDailyAt(new TimeOfDay(23, 59)) // Chạy lại vào 23:59 hàng ngày
                        .OnEveryDay())
                    .Build();
                scheduler.ScheduleJob(job, restartTrigger);

            }
            catch (Exception ex)
            {
                Utilities.WriteErrorLog("restartJob_Start", ex.ToString());
            }
        }
        public static void Stop()
        {
            try
            {
                scheduler?.Shutdown();
            }
            catch (Exception ex)
            {
                Utilities.WriteErrorLog("restartJob_Stop", ex.ToString());
            }
        }
    }

    class CreateFolder : IJob
    {
        public void Execute(IJobExecutionContext context)
        {
            try
            {
                // Lấy thời gian hiện tại
                DateTime now = DateTime.Now;

                // tên thư mục bao gồm đường dẫn và thời gian hiện tại
                var folderLpnTmp = $@"{AppSettings.MediaFolderLpn}/{now.ToString(@"/yyyy/MM")}";
                var folderLaneTmp = $@"{AppSettings.MediaFolderLane}/{now.ToString(@"/yyyy/MM")}";

                // lấy thời gian hiện tại
                var today = now.Day;

                // j được duyệt trong ngày hiện tại với ngày hôm sau
                for (int j = today; j < today + 2; j++)
                {
                    // i được duyệt nếu nhỏ hơn 24h
                    for (int i = 0; i < 24; i++)
                    {
                        // gồm đường dẫn thư mục/ngày/giờ
                        var folderLpn = $@"{folderLpnTmp}/{j:D2}/{i:D2}";
                        var folderLane = $@"{folderLaneTmp}/{j:D2}/{i:D2}";


                        // Kiểm tra nếu thư mục chưa tồn tại thì tạo thư mục
                        // Check exist folder LPN
                        if (!Directory.Exists(folderLpn))
                            Directory.CreateDirectory(folderLpn);

                        // Check exist folder LANE
                        if (!Directory.Exists(folderLane))
                            Directory.CreateDirectory(folderLane);
                    }
                }

                Utilities.WriteOperationLog("CreateFolder", $"Created folder done");
            }
            catch (Exception e)
            {
                Utilities.WriteOperationLog("CreateFolder", $"Created folder error: {e.Message}");
            }
        }
    }
}
