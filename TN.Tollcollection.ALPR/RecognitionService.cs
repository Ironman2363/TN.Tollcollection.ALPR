using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
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


        // Key và Value
        private Dictionary<string, string> _dataLaneCapture = new Dictionary<string, string>();

        /// <summary>
        /// Start Services
        /// </summary>
        public void Start()
        {
            
            try
            {


                // lấy ra key và value đó là tên làn và kiểu camera
                
                _dataLaneCapture = Utilities.GetTypeCam();

                byte[] healthCheckBytes = Encoding.UTF8.GetBytes(_healthCheckMsg);
                // Danh sách cấu hình host các làn và được phân tách bởi dấu ';'
                string[] hostStrs = AppSettings.LaneConfig.Split(';');
                // Danh sách hostStrs sau khi được phân tách bởi dấu ';'
                //"L2|192.168.2.74||123.25.243.161|root|trinam@123"
                //"L3|192.168.2.96|123.25.243.161|123.25.243.161|root|trinam@123"
                //"L4|192.168.1.55|123.25.243.161|123.25.243.161|root|trinam@123"

                // Khởi tạo đối tượng danh sách LaneInfo
                _laneConfigs = new List<LaneInfo>();
                // Duyệt qua mỗi mục trong danh sách hostStrs
                //"L2|192.168.2.74||123.25.243.161|root|trinam@123"
                foreach (string d in hostStrs)
                {
                    // Sau khi được duyệt khởi tạo chuỗi và phân tách các phần tử ra bởi dấu '|'
                    string[] components = d.Split('|');

                    if (components.Length == 6)
                    {
                        // Nếu độ dài của components bằng 6 thì liệt kê các phần tử của mảng components

                        _lane = components[0]; // tên làn
                        string ipPcLane = components[1]; // địa chỉ Ip của làn
                        string ipCamLpn = components[2];// địa chỉ Ip của camera biển số
                        string ipCamLane = components[3];// địa chỉ Ip của camera làn
                        string userCamLane = components[4];// tài khoản camera của làn
                        string passCamLane = components[5];// mật khẩu camera của làn

                        // Tạo một đối tượng SyncSocketClient sử dụng Constructor có các tham số
                        // Đối tượng này được sử dụng để kết nối và giao tiếp thông tin qua socket
                        //Mở cổng lắng nghe từ server
                        SyncSocketClient client = new SyncSocketClient(
                            ipPcLane,                      // Tham số 1: Địa chỉ IP của máy tính hoặc thiết bị đích
                            AppSettings.ClientPort,        // Tham số 2: Số cổng mà SyncSocketClient sẽ sử dụng để kết nối
                            5000,                          // Tham số 3: Thời gian timeout (5 giây trong trường hợp này)
                            true,                          // Tham số 4: Cờ kích hoạt một tính năng cụ thể của SyncSocketClient
                            true,                          // Tham số 5: Cờ kích hoạt một tính năng khác của SyncSocketClient
                            20 * 1000,                     // Tham số 6: Thời gian giữa các lần kiểm tra sức khỏe (20 giây)
                            healthCheckBytes,              // Tham số 7: Dữ liệu sử dụng trong quá trình kiểm tra sức khỏe
                            "ARH"                          // Tham số 8: Chuỗi định danh hoặc mã loại của đối tượng SyncSocketClient
                        );


                        // Chạy đối tượng SyncSocketClient để bắt đầu kết nối và thực hiện 
                        client.Start();

                        LaneInfo info = new LaneInfo(_lane, ipPcLane, ipCamLpn, ipCamLane, userCamLane, passCamLane, client);


                        _laneConfigs.Add(info);

                    }
                }


                // _server mở kết nối để nhận dữ liệu từ máy tính làn
                _server = new SocketServer(AppSettings.ServerIp, AppSettings.ServerPort, true, 20 * 1000, 20 * 1000, healthCheckBytes);
                // _server.ClientConnected là thông báo tới khi kết nối thành công
                _server.ClientConnected += SocketListener_ConnectDone;
                /// Hàm lắng nghe các message được gửi lên từ client
                /// Nếu có message hợp lệ sẽ tiến hành thực thi các nhiệm vụ
                _server.DataReceived += SocketListener_DataReceived;
                /// Ghi log lại những client đã ngắt kết nối
                _server.ClientDisconnected += SocketListener_ClientDisconnected;
                // Chạy đối tượng _server để bắt đầu kết nối và thực hiện 
                _server.Start();

                /// Kiểm tra xem thư mục chứa ảnh Làn và ảnh Biển số có tồn tại hay không!
                /// Nếu chưa tồn tại thì tiến hành tạo thư mục đó theo đường dẫn
                CheckDirectory(AppSettings.MediaFolderLpn, AppSettings.MediaFolderLane);



                try
                {
                    // Chạy JobCreateFolder để thực hiện tạo các thư mục
                    JobCreateFolder.Start();
                    Utilities.WriteDebugLog("JobCreatedFolder", $"Start at: {DateTime.Now}");
                }
                catch (Exception e)
                {
                    Utilities.WriteErrorLog("JobCreatedFolder", $"Error: {e.Message}");
                }
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

                JobCreateFolder.Stop();
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
            // Kiểm tra xem biển số xe có null hoặc trống hay không
            if (!string.IsNullOrEmpty(tempLpn))
            {
                // Viết hoa hết các kí tự và loại bỏ khoảng trắng
                string lpn = tempLpn.ToUpper().Trim();

                // Nếu biển số trả về mà có dộ dài <3 thì trả về biển số hiện tại
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

                // Sau khi chuyển đổi các kí tự bị định dạng sai thì chuyển về chuỗi
                lpn = sb.ToString();
                // Chuyển đổi các kí tự đặc biệt chứa dấu trong biển số về chữ
                lpn = ConvertSpecialCharacters(lpn);
                // Ghi log ra biển trước khi định dạng và sau khi định dạng
                Utilities.WriteDebugLog("Plate begin / after:", $"Biển trước: {tempLpn} | Biển trước: {lpn}");

                // Trả về biển số đã được định dạng
                return lpn;
            }
            else
            {
                return null;
            }
        }


        /// <summary>
        /// Phương thức xử lý chuỗi biển số để chuyển đổi các ký tự và đảm bảo định dạng chuẩn.
        /// </summary>
        /// <param name="tempPlate">Biển số xe truyền vào cần xử lý.</param>
        /// <returns>Chuỗi biển số đã được xử lý.</returns>
        public string FixPlate(string tempPlate)
        {
            //Utilities.WriteDebugLog("Plate Begin: ", $"{tempPlate}");

            // Kiểm tra xem biển số có giá trị hay không
            if (!String.IsNullOrEmpty(tempPlate))
            {

                // Chuyển đổi biển số về chữ in hoa và loại bỏ khoảng trắng ở hai đầu
                var plate = tempPlate.ToUpper().Trim();
                plate = plate.Replace("O", "0");

                // Tạo đối tượng StringBuilder để xử lý chuỗi biển số
                StringBuilder sb = new StringBuilder(plate);

                // Biển quân đội (nếu độ dài < 7)
                if (plate.Length < 7)
                {
                    // Duyệt qua từng ký tự trong biển số
                    foreach (var charx in plate)
                    {
                        var index = plate.LastIndexOf(charx);

                        // Kiểm tra xem ký tự có phải là chữ cái và có vị trí >= 2 không
                        if (Char.IsLetter(charx) && index >= 2)
                        {
                            // Chuyển đổi ký tự theo quy tắc nếu là chữ cái từ vị trí thứ 3 trở đi
                            if (charx == 'B' || charx == 'Q') sb[index] = '8';
                            if (charx == 'D' || charx == 'N' || charx == 'M' || charx == 'U') sb[index] = '0';
                            if (charx == 'Z') sb[index] = '2';
                            if (charx == 'G' || charx == 'C') sb[index] = '6';
                            if (charx == 'E') sb[index] = '3';
                            if (charx == 'A') sb[index] = '4';
                            if (charx == 'J') sb[index] = '7';
                            if (charx == 'I' || charx == 'L' || sb[0] == 'T') sb[index] = '1';
                        }
                        else
                        {
                            // Chuyển đổi ký tự ngược lại nếu không phải là chữ cái từ vị trí thứ 3 trở đi
                            if (charx == '8') sb[index] = 'B';
                            if (charx == '6') sb[index] = 'G';
                            if (charx == '1') sb[index] = 'T';
                            if (charx == '4') sb[index] = 'A';
                            if (charx == '0') sb[index] = 'Q';
                        }
                    }
                    // Chuyển đổi các ký tự đặc biệt trong chuỗi kết quả
                    plate = ConvertSpecialCharacters(sb.ToString());
                }
                // Biển thường (nếu độ dài >= 7)
                else
                {
                    // Kiểm tra và chuyển đổi ký tự đầu tiên
                    if (sb[0] == 'B') sb[0] = '8';
                    if (sb[0] == 'S') sb[0] = '5';
                    if (sb[0] == 'Z') sb[0] = '2';
                    if (sb[0] == 'G') sb[0] = '6';
                    if (sb[0] == 'Q') sb[0] = '8';
                    if (sb[0] == 'A') sb[0] = '4';
                    if (sb[0] == 'J') sb[0] = '7';
                    if (sb[0] == '0' && Convert.ToInt16(sb[1]) > 5) { sb[0] = '8'; }
                    if (sb[0] == '0' && Convert.ToInt16(sb[1]) < 5) { sb[0] = '5'; }
                    if (sb[0] == 'I' || sb[0] == 'L' || sb[0] == 'T') sb[0] = '1';

                    // Kiểm tra và chuyển đổi ký tự thứ hai
                    if (sb[1] == 'B' || sb[1] == '0')
                    {
                        sb[1] = '8';
                    }
                    else if (sb[1] == 'G')
                    {
                        sb[1] = '6';
                    }
                    else if (sb[1] == 'L' || sb[1] == 'I' || sb[1] == 'T')
                    {
                        sb[1] = '1';
                    }
                    else if (sb[1] == 'Q')
                    {
                        sb[1] = '8';
                    }
                    else if (sb[1] == 'A')
                    {
                        sb[1] = '4';
                    }
                    else if (sb[1] == 'Z')
                    {

                        sb[1] = '2';
                    }
                    else if (sb[1] == 'E')
                    {
                        sb[1] = '3';
                    }
                    else if (sb[1] == 'D')
                    {
                        sb[1] = '0';
                    }
                    else if (sb[1] == 'F')
                    {
                        sb[1] = '7';
                    }
                    else if (sb[1] == 'C')
                    {
                        sb[1] = '6';
                    }
                    else if (sb[1] == 'O')
                    {
                        sb[1] = '8';
                    }

                    // Kiểm tra và chuyển đổi ký tự thứ ba

                    if (sb[2] == '8' || sb[2] == '3')
                    {
                        sb[2] = 'B';
                    }
                    else if (sb[2] == '4' && sb[1] == '1')
                    {
                        sb[2] = 'A';
                    }
                    else if (sb[2] == '1')
                    {
                        sb[2] = 'I';
                    }
                    else if (sb[2] == '6' || sb[2] == '9' || sb[2] == '5')
                    {
                        sb[2] = 'G';
                    }
                    else if (sb[2] == '3')
                    {
                        sb[2] = 'E';
                    }
                    else if (sb[2] == '2')
                    {
                        sb[2] = 'Z';
                    }
                    else if (sb[2] == '7')
                    {
                        sb[2] = 'P';
                    }
                    //else if (sb[2] == '0' || sb[2] == 'O')
                    //{
                    //    sb[2] = 'G';
                    //}

                    // Duyệt lại từng ký tự trong biển số để chuyển đổi theo quy tắc
                    foreach (var charx in plate)
                    {
                        var index = plate.LastIndexOf(charx);
                        // Kiểm tra xem ký tự có phải là chữ cái và có vị trí > 2 không
                        if (Char.IsLetter(charx) && index > 2)
                        {
                            // Chuyển đổi ký tự theo quy tắc nếu là chữ cái từ vị trí thứ 3 trở đi
                            if (charx == 'B' || charx == 'R') sb[index] = '8';
                            if (charx == 'D') sb[index] = '0';
                            if (charx == 'Z') sb[index] = '2';
                            if (charx == 'G') sb[index] = '6';
                            if (charx == 'E') sb[index] = '3';
                            if (charx == 'Q') sb[index] = '0';
                            if (charx == 'A') sb[index] = '4';
                            if (charx == 'C') sb[index] = '6';
                            if (charx == 'N') sb[index] = '0';
                            if (charx == 'M') sb[index] = '0';
                            if (charx == 'U') sb[index] = '0';
                            if (charx == 'I' || charx == 'L') sb[index] = '1';
                        }
                    }
                    // Kiểm tra đặc biệt với trường hợp ký tự thứ 3 là 'L'
                    if (sb[2] == 'L')
                    {
                        var subStr = tempPlate.Substring(3, tempPlate.Length - 3);
                        if (subStr.Length > 5)
                        {
                            sb[3] = 'D';
                        }
                    }
                    // Chuyển đổi các ký tự đặc biệt trong chuỗi kết quả
                    plate = ConvertSpecialCharacters(sb.ToString());
                }

                Utilities.WriteDebugLog("Plate begin / after:", $"Biển trước: {tempPlate} | Biển trước: {plate}");

                //Utilities.WriteDebugLog("Pate After: ", $"{plate}");
                // Trả về biển số đã được định dạng
                return plate;
            }
            else
            {
                return "";
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

            // Chuyển kí tự & về kí tự and
            str = str.Replace("&", "and");

            // Dùng để chuẩn hóa chuỗi bằng cách bỏ dấu
            str = str.Normalize(NormalizationForm.FormKD);
            for (int i = 0; i <= str.Length - 1; i++)
            {
                // Bỏ qua các ký tự dấu và ký tự đặc biệt
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
                Utilities.WriteOperationLog("SocketListener_ConnectDone", $"{clientIp} connected!");
            }
            catch (Exception ex)
            {
                Utilities.WriteErrorLog("SocketListener_ConnectDone", ex.ToString());
            }
        }


        private static DateTime requestTime;


        /// <summary>
        /// Hàm lắng nghe các message được gửi lên từ client
        /// Nếu có message hợp lệ sẽ tiến hành thực thi các nhiệm vụ
        /// </summary>
        /// <param name="listener">Listener của Server</param>
        /// <param name="clientIp">Địa chỉ IP máy client</param>
        /// <param name="data">Dữ liệu nhận được của client</param>
        /// <param name="byteCount">Số byte nhận</param>
        /// 
        private void SocketListener_DataReceived(SocketServer listener, string clientIp, byte[] data, int byteCount)
        {
            try
            {
                // Chuyển đổi dữ liệu nhận được từ mảng byte thành chuỗi UTF-8.
                string originalMsg = Encoding.UTF8.GetString(data, 0, byteCount);

                // Tách các thông điệp trong chuỗi nhận được dựa trên ký tự ')'.
                string[] messages = originalMsg.Trim().Split(')');
                // Duyệt qua từng thông điệp trong danh sách các thông điệp.
                foreach (string message in messages)
                {
                    // Bỏ qua các thông điệp rỗng hoặc null.
                    if (string.IsNullOrEmpty(message))
                        continue;

                    // Thêm ký tự ')' vào cuối mỗi thông điệp để tạo ra chuỗi hoàn chỉnh.
                    string msg = message + ")";

                    // Bỏ qua thông điệp kiểm tra sức khỏe nếu nó trùng với giá trị kiểm tra sức khỏe đã đặt trước đó.
                    if (msg == _healthCheckMsg)
                        continue;

                    // Ghi log thông báo đã nhận được từ client.
                    Utilities.WriteOperationLog("SocketListener_DataReceived", $"Received from {clientIp}: {msg}");

                    // Kiểm tra nếu thông điệp bắt đầu bằng "camera.capture".
                    if (msg.StartsWith("camera.capture"))
                    {
                        // Ghi log bắt đầu quá trình chụp ảnh từ camera.
                        requestTime = DateTime.Now;
                        Utilities.WriteDebugLog("START ANPR (1) :", $"ClientIp: {clientIp} | Capture in {requestTime:yyyy/MM/dd HH:mm:ss.ffffff}");

                        // Tìm thông tin về làn từ danh sách _laneConfigs dựa trên địa chỉ IP của client.
                        var laneInfo = _laneConfigs.FirstOrDefault(l => l.IpPcLane == clientIp);

                        // Nếu thông tin về làn tồn tại, thực hiện tải ảnh thông qua một task độc lập.
                        if (laneInfo != null)
                        {
                            Utilities.WriteOperationLog("camera.capture()", $"IpPcLane: {laneInfo.IpPcLane}, Msg = {msg}");

                            // Sử dụng Task.Run để thực hiện DownloadImage trong một task độc lập.
                            Task.Run(() => DownloadImage(laneInfo)).ConfigureAwait(false);
                        }
                        else
                        {
                            // Ghi log nếu không tìm thấy thông tin về làn.
                            Utilities.WriteOperationLog("camera.capture()", $"IpPcLane: null, Msg = {msg}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Ghi log lỗi nếu có lỗi xảy ra trong quá trình xử lý dữ liệu nhận được.
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
            if (!Directory.Exists(pathLpn))
                Directory.CreateDirectory(pathLpn);

            // Check exist folder LANE
            if (!Directory.Exists(pathLane))
                Directory.CreateDirectory(pathLane);
        }

        private void CheckDirectory(string folder)
        {
            var path = Path.GetDirectoryName(folder);
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
        }

        #region DoALPR

        /// <summary>
        /// TEST
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
        private async Task DownloadImage(LaneInfo laneInfo)
        {
            try
            {
                // Tạo một đối tượng Stopwatch để đo thời gian thực hiện của một phần của mã.
                Stopwatch stopwatchDownloadImg = new Stopwatch();
                // Lấy thời gian hiện tại
                DateTime now = DateTime.Now;

                #region Test
                //string imageFullPathLpn = @"D:\DATA_GSTP\TEMP\Lpn\2020\11\20\14\L4_Lpn_20201120143555153.jpg";
                //string imageFullPathLane = @"D:\DATA_GSTP\TEMP\Lane\2020\11\20\14\L4_Lane_20201120143555153.jpg";
                //CheckDirectory(imageFullPathLpn, imageFullPathLane);
                //dataArh = ImageToByteArray(Image.FromFile(imageFullPathLpn));
                //dataAxis = ImageToByteArray(Image.FromFile(imageFullPathLane));
                #endregion
                // Xây dựng đường dẫn thư mục cho lưu trữ ảnh từ quá trình nhận diện biển số xe (LPN).
                var folderLpn = $@"{AppSettings.MediaFolderLpn}\{now:yyyy\\MM\\dd\\HH}";

                // Xây dựng đường dẫn thư mục cho lưu trữ ảnh từ camera theo làn đường.
                var folderLane = $@"{AppSettings.MediaFolderLane}\{now:yyyy\\MM\\dd\\HH}";

                // Tạo đường dẫn đầy đủ cho ảnh từ quá trình nhận diện biển số xe (LPN).
                string imageFullPathLpn = $@"{folderLpn}\{laneInfo.Id}_Lpn_{now:yyyyMMddHHmmssfff}.jpg";

                // Tạo đường dẫn đầy đủ cho ảnh từ camera theo làn đường.
                string imageFullPathLane = $@"{folderLane}\{laneInfo.Id}_Lane_{now:yyyyMMddHHmmssfff}.jpg";


                //CheckDirectory(folderLpn, folderLane);

                Utilities.WriteDebugLog("START ANPR (2.1 - DOWN-IMG) :", $"ClientIp: {laneInfo.IpPcLane} | Lane: {laneInfo.Id}");

                // Bắt đầu đo thời gian cho quá trình tải ảnh.
                stopwatchDownloadImg.Start();

                // Kiểm tra nếu ứng dụng đang chạy ở chế độ giả lập (ModeSimulator = 1).
                if (AppSettings.ModeSimulator == 1)
                {
                    // Mô phỏng dữ liệu: Seed Data
                    var data = SeedData(0, laneInfo.Id);

                    // Lấy đường dẫn đầy đủ cho ảnh từ dữ liệu mô phỏng.
                    imageFullPathLpn = data.Split('|')[1];
                    imageFullPathLane = data.Split('|')[2];
                }

                else
                {
                    // Khởi động chạy lại thời gian tải ảnh
                    stopwatchDownloadImg.Restart();
                    // Nếu địa chỉ IP camera biển số không null hoặc trống
                    if (!String.IsNullOrEmpty(laneInfo.IpCameraLpn))
                    {
                        try
                        {
                            //if (!File.Exists(imageFullPathLpn))
                            //{

                            // Thay thế {IpCameraLpn} của AppSettings.CaptureLpn bằng IP của camera biển số
                            string cameraLinkArh = AppSettings.CaptureLpn.Replace("{IpCameraLpn}",
                                $"{laneInfo.IpCameraLpn}");


                            // Download and save image
                            //await DownloadImageByUrl(cameraLinkArh, imageFullPathLpn);
                            Task.Run(() => DownloadImageByUrl(cameraLinkArh, imageFullPathLpn, laneInfo, "", "", true));
                            //}
                        }
                        catch (Exception ex)
                        {
                            Utilities.WriteErrorLog("Capture ARH", ex.ToString());
                        }
                    }
                    stopwatchDownloadImg.Stop();
                    Utilities.WriteDebugLog("START ANPR (2.2 - DOWN-IMG) :", $"ClientIp: {laneInfo.IpPcLane} | Lane: {laneInfo.Id} | Total Time ARH : {stopwatchDownloadImg.ElapsedMilliseconds}");

                    // Khởi động chạy lại thời gian tải ảnh
                    stopwatchDownloadImg.Restart();
                    // Nếu IP camera làn mà null hoặc trống.

                    if (!String.IsNullOrEmpty(laneInfo.IpCameraLane))
                    {


                        // Lấy ra cameraLinkLane
                        try
                        {
                            //if (!File.Exists(imageFullPathLane))
                            //{
                            // Khai báo cameraLinkLane bằng chuỗi rỗng
                            string cameraLinkLane = String.Empty;

                            // Nếu _dataLaneCapture khác null và  _dataLaneCapture.TryGetValue(L2,0) type là kiểu camera
                            if (_dataLaneCapture != null && _dataLaneCapture.TryGetValue(laneInfo.Id, out var type))
                            {
                                // Convert về kiểu int
                                var typeCam = Convert.ToInt32(type);

                                if (typeCam == (int)TypeCamera.AXIS)
                                {

                                    // Thay thế {IpCameraLane} của AppSettings.AxisCaptureLane bằng IP của camera làn
                                    cameraLinkLane = AppSettings.AxisCaptureLane.Replace("{IpCameraLane}",
                                        $"{laneInfo.IpCameraLane}");
                                }
                                else if (typeCam == (int)TypeCamera.HIKVISON)
                                {
                                    // Thay thế {IpCameraLane} của AppSettings.HikCaptureLane bằng IP của camera làn
                                    cameraLinkLane = AppSettings.HikCaptureLane.Replace("{IpCameraLane}",
                                        $"{laneInfo.IpCameraLane}");
                                }
                            }

                            // Các trường hợp còn lại
                            else
                            {
                                if (AppSettings.TypeCamera == (int)TypeCamera.AXIS)
                                {
                                    // Thay thế {IpCameraLane} của AppSettings.AxisCaptureLane bằng IP của camera làn
                                    cameraLinkLane = AppSettings.AxisCaptureLane.Replace("{IpCameraLane}",
                                        $"{laneInfo.IpCameraLane}");
                                }

                                else if (AppSettings.TypeCamera == (int)TypeCamera.HIKVISON)
                                {
                                    // Thay thế {IpCameraLane} của AppSettings.HikCaptureLane bằng IP của camera làn
                                    cameraLinkLane = AppSettings.HikCaptureLane.Replace("{IpCameraLane}",
                                        $"{laneInfo.IpCameraLane}");
                                }
                            }


                            // Download and save image
                            //Task.Run(() => DownloadImageByUrl(cameraLinkLane, imageFullPathLane, laneInfo.UserCamLane, laneInfo.PassCamLane));
                            //Thực hiện dowload hình ảnh
                            await DownloadImageByUrl(cameraLinkLane, imageFullPathLane, laneInfo, laneInfo.UserCamLane, laneInfo.PassCamLane, false);

                            //}

                        }
                        catch (Exception ex)
                        {

                            Utilities.WriteErrorLog("Capture AXIS", ex.ToString());
                        }
                    }

                    stopwatchDownloadImg.Stop();
                    Utilities.WriteDebugLog("START ANPR (2.3 - DOWN-IMG) :", $"ClientIp: {laneInfo.IpPcLane} | Lane: {laneInfo.Id} | Total Time Axis : {stopwatchDownloadImg.ElapsedMilliseconds}");
                }
                // Thực hiện quá trình nhận dạng biển số
                DoALPRNew(laneInfo, imageFullPathLpn, imageFullPathLane);
            }
            catch (Exception ex)
            {
                Utilities.WriteErrorLog("DownloadImage", ex.ToString());
            }
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="url">Đường dẫn URL của hình ảnh cần tải về</param>
        /// <param name="savePath">Đường dẫn tới vị trí mà hình ảnh sẽ được lưu trữ.</param>
        /// <param name="laneInfo">Thông tin về lản xe bao gồm tên, ip máy tính và ip của các camera</param>
        /// <param name="userName">Tài khoản</param>
        /// <param name="password">Mật khẩu</param>
        /// <param name="isArh">Biểu thị xem đây có phải là tải về từ hệ thống ARH hay không</param>
        /// <returns></returns>
        async Task DownloadImageByUrl(string url, string savePath, LaneInfo laneInfo, string userName = "", string password = "", bool isArh = false)
        {
            try
            {
                Utilities.WriteDebugLog("START ANPR (3.0 - DOWN-IMG) :", $"ClientIp: {laneInfo.IpPcLane} | Lane: {laneInfo.Id} | Download Img Start: {DateTime.Now:yyyy/MM/dd HH:mm:ss.ffffff} | Url: {url}");

                var time = Stopwatch.StartNew();
                byte[] data = null;

                using (WebClient webClient = new WebClient())
                {
                    if (!string.IsNullOrEmpty(userName))
                    {

                        webClient.Credentials = new NetworkCredential(userName, password);
                    }

                    // Download and save image

                    if (isArh)
                    {
                        data = await webClient.DownloadDataTaskAsync(url);
                        time.Stop();
                        Utilities.WriteDebugLog("START ANPR (3.1 - ARH) :", $"ClientIp: {laneInfo.IpPcLane} | Lane: {laneInfo.Id} | Download Img ARH Byte: {time.ElapsedMilliseconds} ms");
                    }
                    else
                    {
                        var task = webClient.DownloadDataTaskAsync(url);
                        if (await Task.WhenAny(task, Task.Delay(AppSettings.TimeOutLane)) == task)
                        {
                            data = task.Result;
                            time.Stop();
                            Utilities.WriteDebugLog("START ANPR (4.1 - AXIS) :", $"ClientIp: {laneInfo.IpPcLane} | Lane: {laneInfo.Id} | Download Img AXIS Byte: {time.ElapsedMilliseconds} ms");
                        }
                        else
                        {
                            data = null;
                            Utilities.WriteDebugLog("START ANPR (4.1 - AXIS) :", $"ClientIp: {laneInfo.IpPcLane} | Lane: {laneInfo.Id} | Download Img AXIS Byte Time Out {AppSettings.TimeOutLane} ms");
                        }

                    }

                    time.Restart();

                    if (data != null)
                    {
                        try
                        {
                            SaveImage(data, savePath);
                        }
                        catch (Exception)
                        {
                            CheckDirectory(savePath);
                            SaveImage(data, savePath);
                        }

                        time.Stop();

                        if (isArh)
                        {
                            Utilities.WriteDebugLog("START ANPR (3.2 - ARH) :", $"ClientIp: {laneInfo.IpPcLane} | Lane: {laneInfo.Id} | Save Img ARH Byte: {time.ElapsedMilliseconds} ms");
                            Utilities.WriteDebugLog("START ANPR (3.3 - ARH) :", $"ClientIp: {laneInfo.IpPcLane} | Lane: {laneInfo.Id} | Download Img ARH End: {url}");
                        }
                        else
                        {
                            Utilities.WriteDebugLog("START ANPR (4.2 - AXIS) :", $"ClientIp: {laneInfo.IpPcLane} | Lane: {laneInfo.Id} | Save Img AXIS Byte: {time.ElapsedMilliseconds} ms");
                            Utilities.WriteDebugLog("START ANPR (4.3 - AXIS) :", $"ClientIp: {laneInfo.IpPcLane} | Lane: {laneInfo.Id} | Download Img AXIS End: {url}");
                        }
                    }
                    else
                    {
                        if (isArh)
                        {
                            Utilities.WriteDebugLog("START ANPR (3.2 - ARH) :", $"ClientIp: {laneInfo.IpPcLane} | Lane: {laneInfo.Id} | Save Img ARH Byte Data Null : Time Out");
                        }
                        else
                        {
                            Utilities.WriteDebugLog("START ANPR (4.2 - AXIS) :", $"ClientIp: {laneInfo.IpPcLane} | Lane: {laneInfo.Id} | Save Img AXIS Byte Data Null : Time Out");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Utilities.WriteErrorLog("DownloadImageByUrl", ex.ToString());
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
        /// <param name="laneInfo">Thông tin về 1 làn xe khi kết nối tới services thông qua Socket TCP/IP</param>
        public void DoALPRNew(LaneInfo laneInfo, string lpnPath, string lanePath)
        {
            try
            {
                // Khai báo chuỗi gán mặc định là rỗng
                string plate = string.Empty;
                string refStr = string.Empty;
                // Khai báo chuỗi gán mặc định bằng "_"
                string plateAndLocationAndMMCLpn = "-";
                string plateAndLocationAndMMCLane = "-";


                // Trả về true hoặc false tùy thuộc vào AppSettings.ModeInfo == 2 hoặc AppSettings.ModeInfo == 3 thì true còn lại thì false
                bool includeMMC = (AppSettings.ModeInfo == 2 || AppSettings.ModeInfo == 3) ? true : false;

                
                Utilities.WriteDebugLog("START ANPR (5 - DOANPR):", $"ClientIp: {laneInfo.IpPcLane} | Lane: {laneInfo.Id} | Start DoALPRNew in: {DateTime.Now:yyyy/MM/dd HH:mm:ss.ffffff} | 1-MMC-{includeMMC}");

                //var lpnTask = Task.Run(() => DetectControl.DetectPlateWithModeNew(lpnPath, ref plateAndLocationAndMMCLpn));

                // Trả về thông tin biển số từ luồng mới
                Task.Run(() => DetectControl.DetectPlateWithModeNew(lanePath, includeMMC, laneInfo, ref plateAndLocationAndMMCLane));
                // Trả về thông tin biển số từ phương pháp nhận dạng của LPNn gồm  Biển số, Vị trí, Thẻ MMC(Thẻ đa phương tiện)
                plateAndLocationAndMMCLpn = DetectControl.DetectPlateWithModeNew(lpnPath, false, laneInfo, ref refStr);
                // Thiết lập thời gian chờ ban đầu
                int waitTime = 0;
                // Chờ đến khi có kết quả hoặc đã vượt quá thời gian chờ tối đa
                while (plateAndLocationAndMMCLane == "-" && waitTime < AppSettings.ProcessTimeout)
                {
                    // Tăng thời gian chờ
                    waitTime += 10;
                    // Tạo một task bất đồng bộ để tránh blocking luồng chính
                    var t = Task.Run(async delegate
                    {
                        await Task.Delay(10).ConfigureAwait(false);
                    });
                    t.Wait();
                }
                // Nếu không có kết quả từ phương pháp nhận dạng của LPN, gán giá trị rỗng
                if (plateAndLocationAndMMCLpn == "-")
                    plateAndLocationAndMMCLpn = "";
                // Nếu không có kết quả từ phương pháp nhận dạng của Lane, gán giá trị rỗng
                if (plateAndLocationAndMMCLane == "-")
                    plateAndLocationAndMMCLane = "";

                // Ghi log debug với thông tin về thời gian chờ và kết quả của cả hai biển số
                Utilities.WriteDebugLog("START ANPR (5.2 - DOANPR):", $"ClientIp: {laneInfo.IpPcLane} | Lane: {laneInfo.Id} | DoALPRNew: {DateTime.Now:yyyy/MM/dd HH:mm:ss.ffffff} | 2-{waitTime}ms  |  Lpn: {plateAndLocationAndMMCLpn} \t Lane: {plateAndLocationAndMMCLane}");

                // Phân tách và xử lý kết quả của biển số
                var transLpn = plateAndLocationAndMMCLpn?.Split('|');
                var transLane = plateAndLocationAndMMCLane?.Split('|');

                switch (AppSettings.ModeInfo)
                {
                    case 0:
                        try
                        {
                            if (transLpn != null && transLpn.Length >= 1) plateAndLocationAndMMCLpn = $"{transLpn[0]}|0x0|0x0|0x0|0x0";
                            if (transLane != null && transLane.Length >= 1) plateAndLocationAndMMCLane = $"{transLane[0]}|0x0|0x0|0x0|0x0";
                        }
                        catch (Exception e)
                        {
                            Utilities.WriteErrorLog("MODE (0) NOT LOCATION PLATE & MMC", e.ToString());
                        }
                        break;
                    case 1:
                        try
                        {

                            if (transLpn != null && transLpn.Length >= 5)
                                plateAndLocationAndMMCLpn =
                                    $"{transLpn[0]}|{transLpn[1]}|{transLpn[2]}|{transLpn[3]}|{transLpn[4]}";
                            if (transLane != null && transLane.Length >= 5)
                                plateAndLocationAndMMCLane =
                                    $"{transLane[0]}|{transLane[1]}|{transLane[2]}|{transLane[3]}|{transLane[4]}";

                        }
                        catch (Exception e)
                        {
                            Utilities.WriteErrorLog("MODE (1) LOCATION", e.ToString());
                        }

                        break;
                    case 2:

                        try
                        {
                            if (transLpn != null && transLpn.Length >= 9)
                            {
                                plateAndLocationAndMMCLpn = $"{transLpn[0]}|0x0|0x0|0x0|0x0|{transLpn[5]}|{transLpn[6]}|{transLpn[7]}|{transLpn[8]}";
                            }

                            if (transLane != null && transLane.Length >= 9)
                            {
                                plateAndLocationAndMMCLane = $"{transLane[0]}|0x0|0x0|0x0|0x0|{transLane[5]}|{transLane[6]}|{transLane[7]}|{transLane[8]}";

                            }
                        }
                        catch (Exception e)
                        {
                            Utilities.WriteErrorLog("MODE (2) MMC", e.ToString());
                        }

                        break;

                    case 3:

                        try
                        {
                            if (transLpn != null && transLpn.Length >= 10)
                            {
                                plateAndLocationAndMMCLpn =
                                    $"{transLpn[0]}|{transLpn[1]}|{transLpn[2]}|{transLpn[3]}|{transLpn[4]}|{transLpn[5]}|{transLpn[6]}|{transLpn[7]}|{transLpn[8]}";
                            }

                            if (transLane != null && transLane.Length >= 10)
                            {
                                if (transLpn != null)
                                    plateAndLocationAndMMCLane =
                                        $"{transLane[0]}|{transLpn[1]}|{transLpn[2]}|{transLpn[3]}|{transLpn[4]}|{transLane[5]}|{transLane[6]}|{transLane[7]}|{transLane[8]}";
                            }
                        }
                        catch (Exception e)
                        {
                            Utilities.WriteErrorLog("MODE (3) PLATE & MMC", e.ToString());
                        }

                        break;
                }

                string MMC = "-|-";
                string location = "";
                string type = "";
                try
                {
                    var arr = plateAndLocationAndMMCLpn?.Split('|');
                    var arrLane = plateAndLocationAndMMCLane?.Split('|');

                    if (AppSettings.ModeProcessPlate == 0)
                    {
                        plate = arr?.Length > 0 ? ProcessLpn(arr[0]) : ""; // Fix plate OLD
                    }
                    else
                    {
                        plate = arr?.Length > 0 ? FixPlate(arr[0]) : "";
                    }

                    location = arr?.Length >= 5 ? $"{arr[1]}_{arr[2]}_{arr[3]}_{arr[4]}" : "";

                    if (string.IsNullOrEmpty(plate))
                    {
                        if (AppSettings.ModeProcessPlate == 0)
                        {
                            plate = arrLane?.Length > 0 ? ProcessLpn(arrLane[0]) : ""; // Fix plate OLD
                        }
                        else
                        {
                            plate = arrLane?.Length > 0 ? FixPlate(arrLane[0]) : "";
                        }
                    }

                    try
                    {
                        if (transLane != null && transLane.Length > 0)
                        {
                            type = Utilities.ConvertTypeCar(transLane[9]);
                        }

                        MMC = arrLane?.Length >= 9 ? $"{arrLane[5]}-{arrLane[6]}|{arrLane[8]}-{TranslateColor(arrLane[7])}|{type}" : "-|-";
                    }
                    catch
                    {
                        if (transLane != null && transLane.Length > 0)
                        {
                            type = Utilities.ConvertTypeCar(transLane[9]);
                        }

                        MMC = $"-|-|{type}";
                    }
                }
                catch (Exception e)
                {
                    Utilities.WriteErrorLog("DoAlprNew_process_plate_mmc_location", e.ToString());
                }

                if (AppSettings.ModeFullImagePath == 0)
                {
                    lpnPath = lpnPath.Replace(AppSettings.MediaFolderLpn, "");
                    lanePath = lanePath.Replace(AppSettings.MediaFolderLane, "");
                }

                string message = $"event.lpn({plate},{lpnPath},{lanePath},{MMC},{location})";

                Utilities.WriteDebugLog("START ANPR (5.3 - DOANPR):", $"ClientIp: {laneInfo.IpPcLane} | Lane: {laneInfo.Id} | Plate: {plate} | Return Message : {DateTime.Now:yyyy/MM/dd HH:mm:ss.ffffff} | {message}");

                laneInfo.SocketClient.Send(message);

                Utilities.WriteDebugLog("START ANPR (5.4 - DOANPR):", $"ClientIp: {laneInfo.IpPcLane} | Lane: {laneInfo.Id} | Plate: {plate} | TOTAL TIME CAPTURE => SEND : {DateTime.Now.Subtract(requestTime).TotalMilliseconds} ms");

                Utilities.WriteOperationLog("LpnImageCreated", $"Send to client {laneInfo.IpPcLane} - {message}");
            }
            catch (Exception e)
            {
                Utilities.WriteErrorLog("DoALPR", e.ToString());
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
                return TranslateColors.Silver;
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
