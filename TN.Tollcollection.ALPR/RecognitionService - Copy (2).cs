using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using TN.Tollcollection.ALPR.Detect;
using TN.Tollcollection.ALPR.Entity;
using TN.Tollcollection.ALPR.LaneConfig;

namespace TN.Tollcollection.ALPR
{
    /// <summary>
    /// Class chứa các hàm liên quan tới việc xử lý hình ảnh và biển số thông qua tin nhắn từ client gửi lên
    /// </summary>
    public class RecognitionService
    {
        // List các thông tin về làn xe
        private List<LaneInfo> _laneConfigs;

        // Server socket ALPR
        private SocketServer _server;
        private string _lane = "";

        // Tin nhắn duy trì kết nối giữa Server và Client
        private string _healthCheckMsg = "msg.healthcheck()";

        /// <summary>
        /// Start Services
        /// </summary>
        public void Start()
        {
            try
            {
                byte[] healthCheckBytes = Encoding.UTF8.GetBytes(_healthCheckMsg);

                var path = Utilities.CreateFile();
                string[] hostStrs = Utilities.ReadFile(path);

                _laneConfigs = new List<LaneInfo>();
                foreach (string d in hostStrs)
                {
                    string[] components = d.Split('|');
                    if (components.Length == 6)
                    {
                        _lane = components[0];
                        string ipPcLane = components[1];
                        string ipCamLpn = components[2];
                        string ipCamLane = components[3];
                        string userCamLane = components[4];
                        string passCamLane = components[5];

                        SyncSocketClient client = new SyncSocketClient(ipPcLane, AppSettings.ClientPort, 5000, true, true, 20 * 1000, healthCheckBytes, "ARH");
                        client.Start();
                        LaneInfo info = new LaneInfo(_lane, ipPcLane, ipCamLpn, ipCamLane, userCamLane, passCamLane, client);
                        _laneConfigs.Add(info);

                    }

                }


                _server = new SocketServer(AppSettings.ServerIp, AppSettings.ServerPort, true, 20 * 1000, 20 * 1000, healthCheckBytes);
                _server.ClientConnected += SocketListener_ConnectDone;
                _server.DataReceived += SocketListener_DataReceived;
                _server.ClientDisconnected += SocketListener_ClientDisconnected;
                _server.Start();

                CheckDirectory(AppSettings.MediaFolderLpn, AppSettings.MediaFolderLane);
            }
            catch (Exception ex)
            {
                Utilities.WriteErrorLog("RecognitionService_Start", ex.ToString());
            }
        }

        /// <summary>
        /// Stop Services
        /// </summary>
        public void Stop()
        {
            try
            {
                _server.Stop();
                foreach (LaneInfo laneInfo in _laneConfigs)
                {
                    try
                    {
                        laneInfo.SocketClient?.Stop();
                    }
                    catch (Exception ex)
                    {
                        Utilities.WriteErrorLog("Stop_Lane", ex.ToString());
                    }
                }
            }
            catch (Exception ex)
            {
                Utilities.WriteErrorLog("RecognitionService_Stop", ex.ToString());
            }
        }

        /// <summary>
        /// Xử lý các ký tự bị nhận dạng nhầm trong biển số
        /// </summary>
        /// <param name="tempLpn">Biển số xe</param>
        /// <returns></returns>
        private string ProcessLpn(string tempLpn)
        {
            if (!string.IsNullOrEmpty(tempLpn))
            {
                string lpn = tempLpn.ToUpper().Trim();

                if (lpn.Length < 3)
                    return lpn;

                // replace O by 0
                lpn = lpn.Replace("O", "0");

                StringBuilder sb = new StringBuilder(lpn);
                // replace 0 in index [2] by C
                if (sb[2] == '0')
                    sb[2] = 'C';

                // replace 8 in index [2] by B
                if (sb[2] == '8')
                    sb[2] = 'B';

                // replace 4 or 1 in index [2] by A
                if (sb[2] == '1' || sb[2] == '4')
                    sb[2] = 'A';

                // replace A in index [1] by 4
                if (sb[1] == 'A')
                    sb[2] = '4';

                // replace B in index [0] by 8
                if (sb[0] == 'B')
                    sb[0] = '8';

                // replace B in index [1] by 8
                if (sb[1] == 'B')
                    sb[1] = '8';

                // replace Q in index [0] by 8
                if (sb[0] == 'Q')
                    sb[0] = '8';

                // replace Q in index [1] by 8
                if (sb[1] == 'Q')
                    sb[1] = '8';

                lpn = sb.ToString();
                lpn = ConvertSpecialCharacters(lpn);
                return lpn;
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// Chuyển các ký tự đặc biệt về chữ
        /// </summary>
        /// <param name="str">Chuỗi có chứa ký tự đặc biệt</param>
        /// <returns></returns>
        private string ConvertSpecialCharacters(string str)
        {
            StringBuilder sb = new StringBuilder();
            str = str.Replace("&", "and");
            str = str.Normalize(NormalizationForm.FormKD);
            for (int i = 0; i <= str.Length - 1; i++)
            {
                if (char.GetUnicodeCategory(str[i]) != UnicodeCategory.NonSpacingMark && !char.IsPunctuation(str[i]) && !char.IsSymbol(str[i]))
                {
                    sb.Append(str[i]);
                }
            }
            return sb.ToString();
        }

        /// <summary>
        /// Thông báo có client kết nối tới thành công
        /// </summary>
        /// <param name="listener">Listener của Server</param>
        /// <param name="clientIp">Địa chỉ IP của máy Client</param>
        /// <param name="status">Trạng thái kết nối</param>
        private void SocketListener_ConnectDone(SocketServer listener, string clientIp, bool status)
        {
            try
            {
                Console.WriteLine(clientIp);
                Utilities.WriteOperationLog("SocketListener_ConnectDone", $"{clientIp} connected!");
            }
            catch (Exception ex)
            {
                Utilities.WriteErrorLog("SocketListener_ConnectDone", ex.ToString());
            }
        }

        /// <summary>
        /// Hàm lắng nghe các message được gửi lên từ client
        /// Nếu có message hợp lệ sẽ tiến hành thực thi các nhiệm vụ
        /// </summary>
        /// <param name="listener">Listener của Server</param>
        /// <param name="clientIp">Địa chỉ IP máy client</param>
        /// <param name="data">Dữ liệu nhận được của client</param>
        /// <param name="byteCount">Số byte nhận</param>
        private void SocketListener_DataReceived(SocketServer listener, string clientIp, byte[] data, int byteCount)
        {
            try
            {
                string originalMsg = Encoding.UTF8.GetString(data, 0, byteCount);
                string[] messages = originalMsg.Trim().Split(')');

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
                        Console.WriteLine($"{DateTime.Now:yyyy-MM-dd hh:mm:ss.fff} - {msg}");
                        var laneInfo = _laneConfigs.FirstOrDefault(l => l.IpPcLane == clientIp);
                        if (laneInfo != null)
                        {
                            Utilities.WriteOperationLog("camera.capture()", $"IpPcLane: {laneInfo.IpPcLane}, Msg = {msg}");
                            Task.Run(() => DownloadImageNew(laneInfo)).ConfigureAwait(false);
                        }
                        else
                        {
                            Utilities.WriteOperationLog("camera.capture()", $"IpPcLane: null, Msg = {msg}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Utilities.WriteErrorLog("SocketListener_DataReceived", ex.Message);
                Utilities.WriteErrorLog("SocketListener_DataReceived", ex.StackTrace);
            }
        }

        /// <summary>
        /// Ghi log lại những client đã ngắt kết nối
        /// </summary>
        /// <param name="listener">Listener của Server</param>
        /// <param name="clientIp">Địa chỉ IP máy client</param>
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

        /// <summary>
        /// Lưu lại image từ mảng byte trả về của các camera
        /// </summary>
        /// <param name="data">Dữ liệu hình ảnh trả về khi chụp của camera Làn và camera biển số</param>
        /// <param name="part"></param>
        private void SaveImage(byte[] data, string part)
        {
            using (MemoryStream mem = new MemoryStream(data))
            {
                Image image = Image.FromStream(mem);
                image.Save(part, System.Drawing.Imaging.ImageFormat.Jpeg);
            }
        }

        /// <summary>
        /// Kiểm tra xem thư mục chứa ảnh Làn và ảnh Biển số có tồn tại hay không!
        /// Nếu chưa tồn tại thì tiến hành tạo thư mục đó theo đường dẫn
        /// </summary>
        /// <param name="pathLpn">Đường dẫn thư mục ảnh Biển số</param>
        /// <param name="pathLane">Đường dẫn thư mục ảnh Làn</param>
        private void CheckDirectory(string pathLpn, string pathLane)
        {
            // Check exist folder LPN
            string folderLpn = Path.GetDirectoryName(pathLpn);
            if (!Directory.Exists(folderLpn))
                Directory.CreateDirectory(folderLpn);

            // Check exist folder LANE
            string folderLane = Path.GetDirectoryName(pathLane);
            if (!Directory.Exists(folderLane))
                Directory.CreateDirectory(folderLane);
        }
        #region DoALPR

        /// <summary>
        /// TEST BY Mr.TungDaoMinh
        /// </summary>
        private string SeedData(int num, string lane)
        {
            var dl = new ArrayList
            {
                $@"48A0196|D:\DATA_GSTP\TEMP\Lpn\2020\07\23\09\{lane}_Lpn_20200723110221168.jpg|D:\DATA_GSTP\TEMP\Lane\2020\07\23\09\{lane}_Lane_20200723110221168.jpg",
                $@"47A34485|D:\DATA_GSTP\TEMP\Lpn\2020\07\23\09\{lane}_Lpn_20200723110609999.jpg|D:\DATA_GSTP\TEMP\Lane\2020\07\23\09\{lane}_Lane_20200723110609999.jpg",
                $@"82C05359|D:\DATA_GSTP\TEMP\Lpn\2020\07\23\09\{lane}_Lpn_20200723110610078.jpg|D:\DATA_GSTP\TEMP\Lane\2020\07\23\09\{lane}_Lane_20200723110610078.jpg",
                $@"47C11241|D:\DATA_GSTP\TEMP\Lpn\2020\07\23\09\{lane}_Lpn_20200723110629668.jpg|D:\DATA_GSTP\TEMP\Lane\2020\07\23\09\{lane}_Lane_20200723110629668.jpg"
            };
            return dl[num].ToString();
        }


        /// <summary>
        /// Chuyển ảnh sang dạng byte để nhận dạng biển số bằng PP
        /// </summary>
        /// <param name="imageIn">Ảnh được truyền vào</param>
        /// <returns></returns>
        public byte[] ImageToByteArray(Image imageIn)
        {
            using (var ms = new MemoryStream())
            {
                imageIn.Save(ms, imageIn.RawFormat);
                return ms.ToArray();
            }
        }

        /// <summary>
        /// Tải ảnh về từ Camera Biển số và Camera làn khi gửi lệnh chụp ảnh qua HTTP cho 2 camera
        /// </summary>
        /// <param name="laneInfo">Thông tin về lản xe bao gồm tên, ip máy tính và ip của các camera</param>
        /// <returns></returns>
        private async Task DownloadImageNew(LaneInfo laneInfo)
        {
            byte[] dataAxis = null;
            byte[] dataArh = null;
            try
            {
                DateTime now = DateTime.Now;
                #region Test
                //string imageFullPathLpn = @"D:\DATA_GSTP\TEMP\Lpn\2020\11\20\14\L4_Lpn_20201120143555153.jpg";
                //string imageFullPathLane = @"D:\DATA_GSTP\TEMP\Lane\2020\11\20\14\L4_Lane_20201120143555153.jpg";
                //CheckDirectory(imageFullPathLpn, imageFullPathLane);
                //dataArh = ImageToByteArray(Image.FromFile(imageFullPathLpn));
                //dataAxis = ImageToByteArray(Image.FromFile(imageFullPathLane));
                #endregion

                string imageFullPathLpn = AppSettings.MediaFolderLpn.Replace(@"Lpn\", @"TEMP\Lpn\") + DateTime.Now.ToString(@"/yyyy/MM/dd/HH/") + $"{laneInfo.Id}_Lpn_{now:yyyyMMddHHmmssfff}.jpg";
                string imageFullPathLane = AppSettings.MediaFolderLane.Replace(@"Lane\", @"TEMP\Lane\") + DateTime.Now.ToString(@"/yyyy/MM/dd/HH/") + $"{laneInfo.Id}_Lane_{now:yyyyMMddHHmmssfff}.jpg";
                CheckDirectory(imageFullPathLpn, imageFullPathLane);
                bool success = false;
                using (WebClient webClient = new WebClient())
                {
                    int retry = 0;
                    //do
                    //{
                        //try
                        //{
                            if (AppSettings.ModeSimulator == 1)
                            {
                                //Seed Data
                                var data = SeedData(0, laneInfo.Id);
                                imageFullPathLpn = data.Split('|')[1];
                                imageFullPathLane = data.Split('|')[2];
                                dataArh = ImageToByteArray(Image.FromFile(imageFullPathLpn));
                                dataAxis = ImageToByteArray(Image.FromFile(imageFullPathLane));
                            }
                            else
                            {
                                try
                                {
                                    if (AppSettings.PingCamera == 1)
                                    {
                                        var ping = Utilities.PingStatus(laneInfo.IpCameraLpn);
                                        if (ping)
                                        {
                                            if (!File.Exists(imageFullPathLpn))
                                            {
                                                // link camera ARH
                                                //string cameraLinkArh = $"http://{laneInfo.IpCameraLpn}:84/scapture";

                                                string cameraLinkArh = AppSettings.CaptureLpn.Replace("{IpCameraLpn}",
                                                    $"{laneInfo.IpCameraLpn}");

                                                // Download and save image
                                                dataArh = await webClient.DownloadDataTaskAsync(cameraLinkArh);
                                                SaveImage(dataArh, imageFullPathLpn);
                                            }
                                        }
                                        else
                                        {
                                            Utilities.WriteErrorLog("Capture ARH", "Không kết nối được tới camera ARH");
                                        }
                                    }
                                    else
                                    {
                                        if (!File.Exists(imageFullPathLpn))
                                        {
                                            string cameraLinkArh = AppSettings.CaptureLpn.Replace("{IpCameraLpn}",
                                                $"{laneInfo.IpCameraLpn}");

                                            // Download and save image
                                            dataArh = await webClient.DownloadDataTaskAsync(cameraLinkArh);
                                            SaveImage(dataArh, imageFullPathLpn);
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Utilities.WriteErrorLog("Capture ARH", ex.ToString());
                                }

                                try
                                {
                                    if (!File.Exists(imageFullPathLane))
                                    {
                                        string cameraLinkLane = String.Empty;

                                        if (AppSettings.PingCamera == 1)
                                        {
                                            
                                           var ping = Utilities.PingStatus(laneInfo.IpCameraLane);
                                            if (ping)
                                            {


                                                
                                                if (AppSettings.TypeCamera == (int)TypeCamera.AXIS)
                                                {
                                                    cameraLinkLane = AppSettings.AxisCaptureLane.Replace("{IpCameraLane}",
                                                        $"{laneInfo.IpCameraLane}");
                                                }

                                                else if (AppSettings.TypeCamera == (int)TypeCamera.HIKVISON)
                                                {
                                                    cameraLinkLane = AppSettings.HikCaptureLane.Replace("{IpCameraLane}",
                                                        $"{laneInfo.IpCameraLane}");
                                                }

                                                // Credentials camera AXIS
                                                webClient.Credentials = new NetworkCredential(laneInfo.UserCamLane,
                                                    laneInfo.PassCamLane);
                                                // Download and save image
                                                dataAxis = await webClient.DownloadDataTaskAsync(cameraLinkLane);
                                                SaveImage(dataAxis, imageFullPathLane);
                                            }
                                            else
                                            {
                                                Utilities.WriteErrorLog("Capture AXIS", "Không kết nối được tới camera AXIS");
                                            }
                                        }
                                        else
                                        {

                                            if (AppSettings.TypeCamera == (int)TypeCamera.AXIS)
                                            {
                                                cameraLinkLane = AppSettings.AxisCaptureLane.Replace("{IpCameraLane}",
                                                    $"{laneInfo.IpCameraLane}");
                                            }

                                            else if (AppSettings.TypeCamera == (int)TypeCamera.HIKVISON)
                                            {
                                                cameraLinkLane = AppSettings.HikCaptureLane.Replace("{IpCameraLane}",
                                                    $"{laneInfo.IpCameraLane}");
                                            }

                                            // Credentials camera AXIS
                                            webClient.Credentials = new NetworkCredential(laneInfo.UserCamLane,
                                                laneInfo.PassCamLane);
                                            // Download and save image
                                            dataAxis = await webClient.DownloadDataTaskAsync(cameraLinkLane);
                                            SaveImage(dataAxis, imageFullPathLane);
                                        }
                                    }

                                }
                                catch (Exception ex)
                                {
                                   
                                    Utilities.WriteErrorLog("Capture AXIS", ex.ToString());
                                }

                            }

                            //if (File.Exists(imageFullPathLane) && File.Exists(imageFullPathLpn))
                            //{
                            //    success = true;
                            //}
                        //}
                        //catch
                        //{
                        //    await Task.Delay(100).ConfigureAwait(false);
                        //}
                        //finally
                        //{
                        //    retry++;
                        //}
                    //}
                    //while (!success && retry < 3);
                }

                DoALPRNew(imageFullPathLpn, dataArh, imageFullPathLane, dataAxis);

            }
            catch (Exception ex)
            {
                Utilities.WriteErrorLog("DownloadImage", ex.ToString());
            }
        }

        /// <summary>
        /// Tiến hành lấy ảnh đã được tải về
        /// Nhận dạng biển số
        /// Đổi tên ảnh thêm tọa độ biển số vào tên ảnh biển số
        /// Gửi message bao gồm [ biển số, đường dẫn ảnh biển số, đường dẫn ảnh làn xe ] về cho client
        /// </summary>
        /// <param name="lpnPath">Đường dẫn ảnh biển số</param>
        /// <param name="dataArh">Dữ liệu ảnh biển số</param>
        /// <param name="lanePath">Đường dẫn ảnh làn</param>
        /// <param name="dataAxis">Dữ liệu ảnh làn</param>
        public void DoALPRNew(string lpnPath, byte[] dataArh, string lanePath, byte[] dataAxis)
        {
            //Console.WriteLine("In DoALPR New");
            try
            {

                FileInfo fileLpn = new FileInfo(lpnPath);
                FileInfo fileLane = new FileInfo(lanePath);
                string plate = string.Empty;
                string plateAndLocationAndMMCLpn = string.Empty;
                string plateAndLocationAndMMCLane = string.Empty;
                if (fileLpn.Exists)
                {
                    plateAndLocationAndMMCLpn = DetectControl.DetectPlateWithModeNew(lpnPath);
                }
                if (fileLane.Exists)
                {
                    plateAndLocationAndMMCLane = DetectControl.DetectPlateWithModeNew(lanePath);
                }

                var transLpn = plateAndLocationAndMMCLpn?.Split('|');
                var transLane = plateAndLocationAndMMCLane?.Split('|');

                switch (AppSettings.ModeInfo)
                {
                    case 0:
                        if (transLpn != null) plateAndLocationAndMMCLpn = $"{transLpn[0]}|0x0|0x0|0x0|0x0";
                        if (transLane != null) plateAndLocationAndMMCLane = $"{transLane[0]}|0x0|0x0|0x0|0x0";
                        break;
                    case 1:
                        if (transLpn != null)
                            plateAndLocationAndMMCLpn =
                                $"{transLpn[0]}|{transLpn[1]}|{transLpn[2]}|{transLpn[3]}|{transLpn[4]}";
                        if (transLane != null)
                            plateAndLocationAndMMCLane =
                                $"{transLane[0]}|{transLane[1]}|{transLane[2]}|{transLane[3]}|{transLane[4]}";
                        break;
                    case 2:
                        {
                            if (transLpn != null && transLpn.Length > 5)
                            {
                                plateAndLocationAndMMCLpn = $"{transLpn[0]}|0x0|0x0|0x0|0x0|{transLpn[5]}|{transLpn[6]}|{transLpn[7]}|{transLpn[8]}";
                            }

                            if (transLane != null && transLane.Length > 5)
                            {
                                plateAndLocationAndMMCLane = $"{transLane[0]}|0x0|0x0|0x0|0x0|{transLane[5]}|{transLane[6]}|{transLane[7]}|{transLane[8]}";

                            }

                            break;
                        }
                    case 3:
                        {
                            if (transLpn != null && transLpn.Length > 5)
                            {
                                plateAndLocationAndMMCLpn =
                                    $"{transLpn[0]}|{transLpn[1]}|{transLpn[2]}|{transLpn[3]}|{transLpn[4]}|{transLpn[5]}|{transLpn[6]}|{transLpn[7]}|{transLpn[8]}";
                            }

                            if (transLane != null && transLane.Length > 5)
                            {
                                if (transLpn != null)
                                    plateAndLocationAndMMCLane =
                                        $"{transLane[0]}|{transLpn[1]}|{transLpn[2]}|{transLpn[3]}|{transLpn[4]}|{transLane[5]}|{transLane[6]}|{transLane[7]}|{transLane[8]}";
                            }

                            break;
                        }
                }

                string MMC;
                string location;
                if (!string.IsNullOrEmpty(plateAndLocationAndMMCLpn))
                {
                    var arr = plateAndLocationAndMMCLpn.Split('|');

                    plate = ProcessLpn(arr[0]) ?? "";

                    if (!string.IsNullOrEmpty(plateAndLocationAndMMCLane))
                    {
                        var arrLane = plateAndLocationAndMMCLane.Split('|');

                        MMC = arrLane.Length > 5 ? $"{arrLane[5]}-{arrLane[6]}|{arrLane[8]}-{TranslateColor(arrLane[7])}" : "-|-";

                        if (Utilities.CheckPlateFormat(plate))
                        {
                            location = $"_{arr[1]}_{arr[2]}_{arr[3]}_{arr[4]}.jpg";
                            lpnPath = lpnPath.Replace(".jpg", location);
                        }
                        else
                        {

                            lpnPath = lpnPath.Replace(".jpg", $"_0x0_0x0_0x0_0x0.jpg");

                            var data = ProcessLpn(arrLane[0]);
                            plate = ProcessLpn(data) ?? "";
                        }

                    }
                    else
                    {
                        MMC = "-|-";
                    }

                }
                else
                {
                    if (!string.IsNullOrEmpty(plateAndLocationAndMMCLane))
                    {
                        var arrLane = plateAndLocationAndMMCLane.Split('|');
                        if (Utilities.CheckPlateFormat(plate))
                        {
                            location = $"_{arrLane[1]}_{arrLane[2]}_{arrLane[3]}_{arrLane[4]}.jpg";
                            lpnPath = lpnPath.Replace(".jpg", location);
                        }
                        else
                        {
                            lpnPath = lpnPath.Replace(".jpg", $"_0x0_0x0_0x0_0x0.jpg");
                        }
                        MMC = arrLane.Length > 5 ? $"{arrLane[5]}-{arrLane[6]}|{arrLane[8]}-{TranslateColor(arrLane[7])}" : "-|-";
                        var data = ProcessLpn(arrLane[0]);
                        plate = ProcessLpn(data) ?? "";
                    }
                    else
                    {
                        lpnPath = lpnPath.Replace(".jpg", $"_0x0_0x0_0x0_0x0.jpg");
                        MMC = "-|-";
                        plate = "";
                    }
                }

                lpnPath = lpnPath.Replace(@"TEMP\Lpn\", @"Lpn\");
                lanePath = lanePath.Replace(@"TEMP\Lane\", @"Lane\");

                CheckDirectory(lpnPath, lanePath);

                if (fileLpn.Exists) SaveImage(dataArh, lpnPath);
                if (fileLane.Exists) SaveImage(dataAxis, lanePath);

                string[] nameLanes = fileLane.Name.Split('_');
                string[] nameComps = fileLpn.Name.Split('_');
                if (nameComps.Length > 0 && nameLanes.Length > 0)
                {
                    string laneLpn = nameComps[0].Trim();
                    string laneL = nameLanes[0].Trim();

                    if (String.Equals(laneL, laneLpn, StringComparison.CurrentCultureIgnoreCase))
                    {
                        LaneInfo laneInfo = _laneConfigs.FirstOrDefault(l => l.Id == laneLpn);
                        if (!string.IsNullOrEmpty(laneLpn) && laneInfo != null)
                        {
                            string message = $"event.lpn({plate},{lpnPath.Replace(AppSettings.MediaFolderLpn, "")},{lanePath.Replace(AppSettings.MediaFolderLane, "")},{MMC})";
                            Console.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss.ffffff} -- {message}");
                            byte[] data = Encoding.UTF8.GetBytes(message);
                            laneInfo.SocketClient.Send(data);
                            Utilities.WriteOperationLog("LpnImageCreated", $"Send to client {laneInfo.IpPcLane} - {message}");
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Utilities.WriteErrorLog("DoANPR", e.ToString());
            }
        }

        private static string TranslateColor(string color)
        {
            if (color == MapColor.avocado.ToString())
            {
                return TranslateColors.Avocado;
            }
            else if (color == MapColor.beige.ToString())
            {
                return TranslateColors.Beige;
            }
            else if (color == MapColor.blue.ToString())
            {
                return TranslateColors.Blue;
            }
            else if (color == MapColor.brown.ToString())
            {
                return TranslateColors.Brown;
            }
            else if (color == MapColor.cherry.ToString())
            {
                return TranslateColors.Cherry;
            }
            else if (color == MapColor.black.ToString())
            {
                return TranslateColors.Black;
            }
            else if (color == MapColor.chlorophyll.ToString())
            {
                return TranslateColors.Chlorophyll;
            }
            else if (color == MapColor.cinnamon.ToString())
            {
                return TranslateColors.Cinnamon;
            }
            else if (color == MapColor.eggplant.ToString())
            {
                return TranslateColors.Eggplant;
            }
            else if (color == MapColor.emerald.ToString())
            {
                return TranslateColors.Emerald;
            }
            else if (color == MapColor.gillyflower.ToString())
            {
                return TranslateColors.Gillyflower;
            }
            else if (color == MapColor.gold.ToString())
            {
                return TranslateColors.Gold;
            }
            else if (color == MapColor.grape.ToString())
            {
                return TranslateColors.Grape;
            }
            else if (color == MapColor.green.ToString())
            {
                return TranslateColors.Green;
            }
            else if (color == MapColor.lavender.ToString())
            {
                return TranslateColors.Lavender;
            }
            else if (color == MapColor.white.ToString())
            {
                return TranslateColors.White;
            }
            else if (color == MapColor.limon.ToString())
            {
                return TranslateColors.Limon;
            }
            else if (color == MapColor.sky.ToString())
            {
                return TranslateColors.Sky;
            }
            else if (color == MapColor.torquoise.ToString())
            {
                return TranslateColors.Torquoise;
            }
            else if (color == MapColor.yellow.ToString())
            {
                return TranslateColors.Yellow;
            }
            else if (color == MapColor.melon.ToString())
            {
                return TranslateColors.Melon;
            }
            else if (color == MapColor.sunflower.ToString())
            {
                return TranslateColors.Sunflower;
            }
            else if (color == MapColor.orange.ToString())
            {
                return TranslateColors.Orange;
            }
            else if (color == MapColor.tangerine.ToString())
            {
                return TranslateColors.Tangerine;
            }
            else if (color == MapColor.violet.ToString())
            {
                return TranslateColors.Violet;
            }
            else if (color == MapColor.pink.ToString())
            {
                return TranslateColors.Pink;
            }
            else if (color == MapColor.salmon.ToString())
            {
                return TranslateColors.Salmon;
            }
            else if (color == MapColor.sliver.ToString())
            {
                return TranslateColors.Sliver;
            }
            else if (color == MapColor.plum.ToString())
            {
                return TranslateColors.Plum;
            }
            else if (color == MapColor.orchid.ToString())
            {
                return TranslateColors.Orchid;
            }
            else if (color == MapColor.grey.ToString())
            {
                return TranslateColors.Grey;
            }
            else if (color == MapColor.red.ToString())
            {
                return TranslateColors.Red;
            }
            else
            {
                return color;
            }



        }
        #endregion
    }
}
