using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Text;

namespace TN.Tollcollection.ALPR
{
    /// <summary>
    /// Class chứa các hàm liên quan tới String, file
    /// </summary>
    public class Utilities
    {
        //private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger("TN.ARHRecognitionService");

        public static bool ActiveOperationLog = true;
        public static bool ActiveDebugLog = true;

        /// <summary>
        /// Chuyển đổi thời gian theo định dạng
        /// </summary>
        /// <param name="datetime"></param>
        /// <returns></returns>
        public static long ConvertToUnixTime(DateTime datetime)
        {
            DateTime sTime = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

            return (long)(datetime - sTime).TotalSeconds;
        }

        public static void StartLog(bool activeOperationLog, bool activeDebugLog)
        {
            ActiveOperationLog = activeOperationLog;
            ActiveDebugLog = activeDebugLog;
        }

        /// <summary>
        /// Đóng chương trình ghi log
        /// </summary>
        public static void CloseLog()
        {
            foreach (log4net.Appender.IAppender app in log.Logger.Repository.GetAppenders())
            {
                app.Close();
            }
        }

        /// <summary>
        /// Ghi log chạy lỗi
        /// </summary>
        /// <param name="logtype"></param>
        /// <param name="logcontent"></param>
        public static void WriteErrorLog(string logtype, string logcontent)
        {
            try
            {
                log.Error($"{logtype} \t {logcontent}");
            }
            catch
            {
                // ignored
            }
        }

        /// <summary>
        /// Ghi log chạy thành công
        /// </summary>
        /// <param name="logtype"></param>
        /// <param name="logcontent"></param>
        public static void WriteOperationLog(string logtype, string logcontent)
        {
            if (!ActiveOperationLog)
                return;

            try
            {
                log.Info($"{logtype} \t {logcontent}");
            }
            catch
            {
                // ignored
            }
        }

        /// <summary>
        /// Ghi log vào file khi chạy Debug
        /// </summary>
        /// <param name="logtype"></param>
        /// <param name="logcontent"></param>
        public static void WriteDebugLog(string logtype, string logcontent)
        {
            if (!ActiveDebugLog)
                return;

            try
            {
                log.Debug($"{logtype} \t {logcontent}");
            }
            catch
            {
                // ignored
            }
        }

        /// <summary>
        /// Kiểm tra xem file có đang được sử dụng bởi thread nào đó không
        /// </summary>
        /// <param name="file">File truyền vào</param>
        /// <returns></returns>
        public static bool IsFileLocked(FileInfo file)
        {
            FileStream stream = null;

            try
            {
                stream = file.Open(FileMode.Open, FileAccess.Read, FileShare.None);
            }
            catch (IOException)
            {
                //the file is unavailable because it is:
                //still being written to
                //or being processed by another thread
                //or does not exist (has already been processed)
                try
                {
                    if (stream != null)
                        stream.Close();
                }
                catch
                {

                }
                return true;
            }
            finally
            {
                if (stream != null)
                    stream.Close();
            }

            //file is not locked
            return false;
        }

        /// <summary>
        /// Lấy các bit từ 1 mảng Byte và ghép thành chuỗi
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public static string GetBitStr(byte[] data)
        {
            BitArray bits = new BitArray(data);

            string strByte = string.Empty;
            for (int i = 0; i <= bits.Count - 1; i++)
            {
                if (i % 8 == 0)
                {
                    strByte += " ";
                }
                strByte += (bits[i] ? "1" : "0");
            }

            return strByte;
        }


        /// <summary>
        /// Kiểm tra xem drive đã được kết nối sẵn sàng chưa
        /// </summary>
        /// <param name="serverName"></param>
        /// <returns></returns>
        public static bool IsDriveReady(string serverName)
        {
            bool bReturnStatus = false;

            // ***  SET YOUR TIMEOUT HERE  ***
            int timeout = 5;    // 5 seconds


            var pingSender = new System.Net.NetworkInformation.Ping();
            var options = new System.Net.NetworkInformation.PingOptions();

            options.DontFragment = true;

            // Enter a valid ip address
            string ipAddressOrHostName = serverName;
            string data = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
            byte[] buffer = Encoding.ASCII.GetBytes(data);
            try
            {
                var reply = pingSender.Send(ipAddressOrHostName, timeout, buffer, options);

                if (reply != null && reply.Status == System.Net.NetworkInformation.IPStatus.Success)
                    bReturnStatus = true;
            }
            catch (Exception)
            {
                bReturnStatus = false;
            }
            return bReturnStatus;
        }


        /// <summary>
        /// Kiểm tra xem file có đang được sử dụng bởi thread nào đó không
        /// </summary>
        /// <param name="filePath">Đường dẫn file</param>
        /// <returns></returns>
        public static bool IsFileLocked(string filePath)
        {
            FileInfo file = new FileInfo(filePath);
            FileStream stream = null;

            try
            {
                stream = file.Open(FileMode.Open, FileAccess.Read, FileShare.None);
            }
            catch (IOException)
            {
                try
                {
                    stream?.Close();
                }
                catch
                {
                    // ignored
                }

                return true;
            }
            finally
            {
                stream?.Close();
            }
            return false;
        }

        /// <summary>
        /// Tạo file chứa thông tin về làn nếu chưa tồn tại
        /// Ghi dữ liệu mẫu được lấy từ appconfig vào file mẫu
        /// </summary>
        /// <returns></returns>
        public static string CreateFile()
        {
            try
            {
                var folderPath = $@"{Path.GetDirectoryName(Assembly.GetEntryAssembly()?.Location)}\LaneInfo";
                var filename = $@"\LaneInfoConfig.txt";
                if (!Directory.Exists(folderPath))
                {
                    Directory.CreateDirectory(folderPath);
                }

                var path = $"{folderPath}{filename}";
                if (File.Exists(path))
                {
                    IsFileLocked(path);
                }
                else
                {
                    
                    var dataConfig = AppSettings.LaneConfig.Split(';');

                    using (StreamWriter sw = File.CreateText(path))
                    {
                        foreach (var data in dataConfig)
                        {
                            sw.WriteLine($"{data}");
                        }
                        sw.Close();
                    }
                }
                return path;
            }
            catch (Exception e)
            {
                WriteErrorLog("[UTILITIES.CreateFile]", $"ERROR: {e}");
                return null;
            }
        }

        /// <summary>
        /// Đọc thông tin làn từ file txt
        /// </summary>
        /// <param name="path">Đường dẫn file</param>
        /// <returns></returns>
        public static string[] ReadFile(string path)
        {
            IsFileLocked(path);
            var data = File.ReadAllLines(path);
            IsFileLocked(path);
            return data;
        }

        /// <summary>
        /// Kiểm tra kết nối tới camera
        /// </summary>
        /// <param name="ping"></param>
        /// <param name="timeOut"></param>
        /// <returns></returns>
        public static bool PingStatus(string ping, int timeOut = 100)
        {
            
            try
            {
                var pingResult = new Ping().Send(ping, timeOut);
                return pingResult?.Status.ToString() == "Success";
            }
            catch (Exception ex)
            {
                Utilities.WriteErrorLog("[Functions_PingStatus]", $"[{ex}]");
                return false;
            }
        }

        /// <summary>
        /// Kiểm tra định dạng biển số xe xem có đúng không
        /// </summary>
        /// <param name="plateNumber">Biển số xe</param>
        /// <returns></returns>
        public static bool CheckPlateFormat(string plateNumber)
        {
            var check = false;
            if (plateNumber == null)
            {
                check = false;
            }
            var plate = plateNumber.ToUpper();
            switch (plate.Length)
            {
                case 6 when char.IsLetter(plate[0]) && char.IsLetter(plate[1]) && char.IsDigit(plate[2]) && char.IsDigit(plate[3]) && char.IsDigit(plate[4]) && char.IsDigit(plate[5]):
                case 7 when char.IsDigit(plate[0]) && char.IsDigit(plate[1]) && char.IsLetter(plate[2]) && char.IsDigit(plate[3]) && char.IsDigit(plate[4]) && char.IsDigit(plate[5]) && char.IsDigit(plate[6]):
                case 8 when char.IsDigit(plate[0]) && char.IsDigit(plate[1]) && char.IsLetter(plate[2]) && char.IsDigit(plate[3]) && char.IsDigit(plate[4]) && char.IsDigit(plate[5]) && char.IsDigit(plate[6]) && char.IsDigit(plate[7]):
                case 8 when char.IsDigit(plate[0]) && char.IsDigit(plate[1]) && char.IsLetter(plate[2]) && char.IsLetter(plate[3]) && char.IsDigit(plate[4]) && char.IsDigit(plate[5]) && char.IsDigit(plate[6]) && char.IsDigit(plate[7]):
                case 9 when char.IsDigit(plate[0]) && char.IsDigit(plate[1]) && char.IsLetter(plate[2]) && char.IsLetter(plate[3]) && char.IsDigit(plate[4]) && char.IsDigit(plate[5]) && char.IsDigit(plate[6]) && char.IsDigit(plate[7]) && char.IsDigit(plate[8]):
                case 9 when char.IsDigit(plate[0]) && char.IsDigit(plate[1]) && char.IsDigit(plate[2]) && char.IsDigit(plate[3]) && char.IsDigit(plate[4]) && char.IsLetter(plate[5]) && char.IsLetter(plate[6]) && char.IsDigit(plate[7]) && char.IsDigit(plate[8]):
                    check = true;
                    break;
                default:
                    check = false;
                    break;
            }
            return check;
        }

        public static string ConvertTypeCar(string type)
        {
            var VNIType = "";
            try
            {
                if (string.IsNullOrEmpty(type))
                {
                    return VNIType;
                }
                else
                {
                    switch (type.ToLower())
                    {
                        case "suv":
                        case "sedan":
                            VNIType = type;
                            break;
                        case "van":
                            VNIType = "Xe Thùng";
                            break;
                        case "big truck":
                            VNIType = "Xe Tải Cỡ Lớn";
                            break;
                        case "bus":
                            VNIType = "Xe Khách";
                            break;
                        case "car":
                            VNIType = "Xe hơi";
                            break;
                        case "truck":
                            VNIType = "Xe Tải";
                            break;
                        case "coach":
                            VNIType = "Xe Khách";
                            break;
                        case "minibus":
                            VNIType = "Xe Buýt Nhỏ";
                            break;
                        case "camionnette":
                            VNIType = "Xe Tải Nhỏ";
                            break;
                        case "pickup truck":
                            VNIType = "Xe Bán Tải";
                            break;
                        case "tow truck":
                            VNIType = "Xe Kéo";
                            break;
                        case "street cleaner":
                            VNIType = "Xe Quét Đường";
                            break;
                        case "tractor trailer":
                            VNIType = "Xe Đầu Kéo";
                            break;
                        case "fuel truck":
                            VNIType = "Xe Chở Nhiên Liệu";
                            break;
                        case "garbage truck":
                            VNIType = "Xe Chở Rác";
                            break;
                        default:
                            VNIType = type;
                            break;
                    }
                }
            }
            catch
            {
                VNIType = type;
            }

            return VNIType;
        }

        /// <summary>
        /// Phương thức để lấy danh sách loại camera và các làn tương ứng từ cấu hình chụp ảnh của làn.
        /// </summary>
        /// <returns>Dictionary với key là tên làn và value là loại camera.</returns>
        public static Dictionary<string, string> GetTypeCam()
        {
            // Dictionary để lưu trữ thông tin về loại camera và các làn tương ứng
            var dic = new Dictionary<string, string>();

            // Kiểm tra xem cấu hình chụp ảnh của làn có null hay không
            if (AppSettings.ConfigCaptureLane == null)
            {
                // Nếu không có cấu hình, trả về null
                return null;
            }
            else
            {
                // Duyệt qua mỗi mục trong cấu hình chụp ảnh của làn
                // 0|L2,L3,L4,L5 0 là kiểu camera AXIS     | tên các làn sử dụng camera AXIS
                // 1|L1          1 là kiểu camera HIKVISON | tên các làn sử dụng camera HIKVISON

                foreach (var item in AppSettings.ConfigCaptureLane)
                {
                    var list = item.Split('|');
                    // Phân tách thông tin về loại camera và các làn sử dụng dấu '|'
                    // list = {0,(L2,L3,L4,L5)}
                    var listLane = list.LastOrDefault()?.Split(',');
                    // Lấy danh sách các làn sử dụng camera từ phần tử cuối cùng 
                    // listLane = {L2,L3,L4,L5}

                    // Duyệt qua mỗi làn và thêm thông tin vào Dictionary
                    foreach (var lane in listLane)
                    {
                        // Thêm vào Dictionary với key là tên làn và value là loại camera
                        // (L2,0)
                        dic.Add(lane, list.FirstOrDefault());
                    }
                }
                // Trả về Dictionary
                return dic;
            }
        }
    }
}
