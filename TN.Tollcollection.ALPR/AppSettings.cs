using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;

namespace TN.Tollcollection.ALPR
{
    /// <summary>
    /// Class cài đặt những thông số cho phần mềm
    /// </summary>
    public class AppSettings
    {
        // Thông tin hiển thị của phần mềm
        public static string ServiceName = ConfigurationManager.AppSettings["ServiceName"];
        public static string ServiceDisplayName = ConfigurationManager.AppSettings["ServiceDisplayName"];
        public static string ServiceDescription = ConfigurationManager.AppSettings["ServiceDescription"];

        // Thư mục chứa ảnh
        public static string MediaFolderLane = ConfigurationManager.AppSettings["MediaFolderLane"];
        public static string MediaFolderLpn = ConfigurationManager.AppSettings["MediaFolderLpn"];

        // Thông tin mặc định của 1 làn xe
        /// <summary>
        /// Thông tin mặc định của 1 làn xe
        /// </summary>
        public static string LaneConfig = ConfigurationManager.AppSettings["LaneConfig"];

        // Địa chỉ và Cổng của ALPR để client kết nối đến
        public static string ServerIp = ConfigurationManager.AppSettings["ServerIp"];
        public static int ServerPort = Convert.ToInt32(ConfigurationManager.AppSettings["ServerPort"]);

        // Cổng của client để server gửi dữ liệu về
        public static int ClientPort = Convert.ToInt32(ConfigurationManager.AppSettings["ClientPort"]);

        // URL API PLATE RECOGNIZER
        public static string ApiPlateRecognizerEntity = ConfigurationManager.AppSettings["ApiPlateRecognizerEntity"];
        public static string ApiPlateRecognizerEntity1 = ConfigurationManager.AppSettings["ApiPlateRecognizerEntity1"];

        public static string TokenPP = ConfigurationManager.AppSettings["TokenPP"];

        // MODE DETECT
        public static int ModeDetect = Convert.ToInt32(ConfigurationManager.AppSettings["ModeDetect"]);

        // URL CHỤP ẢNH BIỂN SỐ
        public static string CaptureLpn = ConfigurationManager.AppSettings["CaptureLpn"];

        // URL CHỤP ẢNH LÀN
        public static string HikCaptureLane = ConfigurationManager.AppSettings["HikCaptureLane"];
        public static string AxisCaptureLane = ConfigurationManager.AppSettings["AxisCaptureLane"];

        
        public static List<string> ConfigCaptureLane = ConfigurationManager.AppSettings["ConfigCaptureLane"].Split(';').ToList();

        // Chế độ giả lập
        public static int ModeSimulator = Convert.ToInt32(ConfigurationManager.AppSettings["ModeSimulator"]);

        // Chế độ trả về vị trí biển và MMC
        public static int ModeInfo = Convert.ToInt32(ConfigurationManager.AppSettings["ModeInfo"]);

        public static int ModeApi = Convert.ToInt32(ConfigurationManager.AppSettings["ModeApi"]);


        // Loại camera
        public static int TypeCamera = Convert.ToInt32(ConfigurationManager.AppSettings["TypeCamera"]);

        public static int ModeFullImagePath = Convert.ToInt32(ConfigurationManager.AppSettings["ModeFullImagePath"]);
        public static int ProcessTimeout = Convert.ToInt32(ConfigurationManager.AppSettings["ProcessTimeout"]);
        public static int TimeOutLane = Convert.ToInt32(ConfigurationManager.AppSettings["TimeOutLane"]);
        public static int ModeProcessPlate = Convert.ToInt32(ConfigurationManager.AppSettings["ModeProcessPlate"]);
    }
}
