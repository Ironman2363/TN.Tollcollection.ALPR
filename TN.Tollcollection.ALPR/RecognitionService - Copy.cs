using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Drawing;
using System.Text.RegularExpressions;
using gx;
using cm;

namespace ARHRecogitionService
{
    public class RecognitionService
    {
        FileSystemWatcher _lpnImageWatcher;
        List<LaneInfo> laneConfigs;
        SocketServer _server;
        string _healthCheckMsg = "msg.healthcheck()";

        // Creates the ANPR object
        cmAnpr anpr;
        // Creates the image object
        gxImage image;

        public RecognitionService()
        {

        }

        public void Start()
        {
            try
            {
                byte[] healthCheckBytes = Encoding.UTF8.GetBytes(_healthCheckMsg);

                string[] hostStrs = AppSettings.LaneConfig.Split(';');
                laneConfigs = new List<LaneInfo>();
                foreach (string d in hostStrs)
                {
                    string[] components = d.Split('|');
                    if (components.Length == 3)
                    {
                        string lane = components[0];
                        string pcIp = components[1];
                        string cameraIp = components[2];
                        SyncSocketClient client = new SyncSocketClient(pcIp, AppSettings.ServerPort, 5000, true, true, 20 * 1000, healthCheckBytes, "ARH");
                        client.Start();
                        LaneInfo info = new LaneInfo(lane, pcIp, cameraIp, client);
                        laneConfigs.Add(info);
                    }
                }

                _server = new SocketServer(AppSettings.ServerIP, AppSettings.ServerPort, true, 20 * 1000, 20 * 1000, healthCheckBytes);
                _server.ClientConnected += new SocketServer.ClientConnectedHandler(SocketListener_ConnectDone);
                _server.DataReceived += new SocketServer.DataReceivedHandler(SocketListener_DataReceived);
                _server.ClientDisconnected += new SocketServer.ClientDisconnectedHandler(SocketListener_ClientDisconnected);
                _server.Start();


                _lpnImageWatcher = new FileSystemWatcher(AppSettings.LpnMediaFolder, "*.jpg");
                _lpnImageWatcher.IncludeSubdirectories = true;
                _lpnImageWatcher.Created += new FileSystemEventHandler(LpnImageCreated);
                _lpnImageWatcher.EnableRaisingEvents = true;
            }
            catch (Exception ex)
            {
                Utilities.WriteErrorLog("RecognitionService_Start", ex.ToString());
            }
        }

        public void Stop()
        {
            try
            {
                _server.Stop();
                foreach (LaneInfo laneInfo in laneConfigs)
                {
                    try
                    {
                        laneInfo.SocketClient?.Stop();
                    }
                    catch
                    {

                    }
                }
            }
            catch (Exception ex)
            {
                Utilities.WriteErrorLog("RecognitionService_Stop", ex.ToString());
            }
        }

        public void LpnImageCreated(object sender, FileSystemEventArgs e)
        {
            try
            {
                Utilities.WriteOperationLog("LpnImageCreated", $"File created: {e.ChangeType}, Name: {e.Name}, Path: {e.FullPath}");
                FileInfo file = new FileInfo(e.FullPath);
                if (file == null)
                {
                    Utilities.WriteOperationLog("LpnImageCreated", $"File not found: {e.FullPath}");
                    return;
                }

                //while (Utilities.IsFileLocked(file))
                //{
                //    await Task.Delay(100).ConfigureAwait(false);
                //}

                // Loads the sample image
                // Creates the ANPR object
                anpr = new cmAnpr("default");
                // Creates the image object
                image = new gxImage("default");
                image.Load(e.FullPath);

                // Finds the first plate and displays it
                string lpn = "";

                if (anpr.FindFirst(image))
                {
                    lpn = anpr.GetText();
                }

                Utilities.WriteOperationLog("LpnImageCreated", $"{file.Name} - {lpn}");

                string[] nameComps = file.Name.Split('_');
                if (nameComps.Length > 0)
                {
                    string lane = nameComps[0].Trim();
                    LaneInfo laneInfo = laneConfigs.Where(l => l.Id == lane).FirstOrDefault();
                    if (!string.IsNullOrEmpty(lane) && laneInfo != null)
                    {
                        {
                            string message = $"event.lpn({lpn},{e.FullPath.Replace(AppSettings.LpnMediaFolder, "")})";
                            byte[] data = Encoding.UTF8.GetBytes(message);
                            laneInfo.SocketClient.Send(data);
                            Utilities.WriteOperationLog("LpnImageCreated", $"Send to client {laneInfo.PcIpAddress} - {message}");
                        }
                    }
                }

                int resultix = 0;
                bool found = anpr.FindFirst(image);
                while (found)
                {
                    // Prints result
                    resultix++;
                    Utilities.WriteOperationLog("LpnImageCreated", $"{file.Name} - Result #{resultix}: '{anpr.GetText()}'");
                    // Find other plates
                    found = anpr.FindNext();
                }
            }
            catch (Exception ex)
            {
                Utilities.WriteErrorLog("LpnImageCreated", ex.ToString());
            }
        }

        private void SocketListener_ConnectDone(SocketServer listener, string clientIp, bool status)
        {
            try
            {
                Utilities.WriteOperationLog("SocketListener_ConnectDone", $"{clientIp} connected!");
            }
            catch (Exception ex)
            {
                Utilities.WriteErrorLog("SocketListener_ConnectDone", ex.ToString());
            }
        }

        private void SocketListener_DataReceived(SocketServer listener, string clientIp, byte[] data, int byteCount)
        {
            try
            {
                string[] messages = Encoding.UTF8.GetString(data, 0, byteCount).Trim().Split(')');

                foreach (string message in messages)
                {
                    if (string.IsNullOrEmpty(message))
                        continue;

                    string msg = message + ")";
                    if (msg == _healthCheckMsg)
                        continue;

                    Utilities.WriteOperationLog("SocketListener_DataReceived", $"Received from {clientIp}: {msg}");
                    if (msg.StartsWith("camera.capture"))
                    {
                        // download image
                        LaneInfo laneInfo = laneConfigs.Where(l => l.PcIpAddress == clientIp).FirstOrDefault();
                        if (laneInfo != null)
                        {
                            Task.Run(() => DownloadImage(laneInfo)).ConfigureAwait(false);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Utilities.WriteErrorLog("SocketListener_DataReceived", ex.ToString());
            }
        }

        private void SocketListener_ClientDisconnected(SocketServer listener, string clientIp)
        {
            try
            {
                Utilities.WriteOperationLog("SocketListener_ClientDisconnected", $"{clientIp} disconnected!");
            }
            catch (Exception ex)
            {
                Utilities.WriteErrorLog("SocketListener_ClientDisconnected", ex.ToString());
            }
        }

        private async Task DownloadImage(LaneInfo laneInfo)
        {
            try
            {
                DateTime now = DateTime.Now;
                string imageFullPath = AppSettings.LpnMediaFolder + DateTime.Now.ToString(@"/yyyy/MM/dd/HH/") + $"{laneInfo.Id}_{now.ToString("yyyyMMddHHmmssfff")}.jpg";
                string folder = Path.GetDirectoryName(imageFullPath);
                if (!Directory.Exists(folder))
                    Directory.CreateDirectory(folder);

                using (WebClient webClient = new WebClient())
                {
                    int retry = 0;
                    bool success = false;

                    do
                    {
                        try
                        {
                            string cameraLink = $"http://{laneInfo.CameraIpAddress}/scapture";
                            byte[] data = await webClient.DownloadDataTaskAsync(cameraLink);
                            using (MemoryStream mem = new MemoryStream(data))
                            {
                                Image image = Image.FromStream(mem);
                                image.Save(imageFullPath, System.Drawing.Imaging.ImageFormat.Jpeg);
                                success = true;
                            }
                        }
                        catch
                        {
                            await Task.Delay(100).ConfigureAwait(false);
                        }
                        finally
                        {
                            retry++;
                        }
                    }
                    while (!success && retry < 3);
                }
            }
            catch (Exception ex)
            {
                Utilities.WriteErrorLog("DownloadImage", ex.ToString());
            }
        }

    }
}
