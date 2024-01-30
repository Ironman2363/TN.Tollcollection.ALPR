using Newtonsoft.Json;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using TN.Tollcollection.ALPR.Detect;
using TN.Tollcollection.ALPR.Entity;
using TN.Tollcollection.ALPR.LaneConfig;
using Topshelf;

namespace TN.Tollcollection.ALPR
{
    class Program
    {
        static void Main(string[] args)
        {
            try
            {


                TopshelfExitCode exitCode = HostFactory.Run(x =>
                {
                    x.Service<RecognitionService>(s =>
                    {
                        s.ConstructUsing(name => new RecognitionService());
                        s.WhenStarted(cb => cb.Start());
                        s.WhenStopped(cb => cb.Stop());
                        s.WhenShutdown(cb => cb.Stop());
                    });
                    x.RunAsLocalSystem();

                    //#if DEBUG
                    //                x.RunAsPrompt();
                    //#else
                    //                x.RunAsLocalSystem();                            
                    //#endif   

                    x.SetServiceName(AppSettings.ServiceName);
                    x.SetDisplayName(AppSettings.ServiceDisplayName);
                    x.SetDescription(AppSettings.ServiceDescription);

                });

            }


            catch (Exception ex)
            {
                Utilities.WriteErrorLog("Program_Main", ex.ToString());
            }
        }
    }
}
