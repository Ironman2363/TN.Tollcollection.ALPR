using System;
using System.IO;
using TN.Tollcollection.ALPR.LaneConfig;

namespace TN.Tollcollection.ALPR.Detect
{
    /// <summary>
    /// Tổng hợp 2 phương pháp nhận dạng
    /// Chia phương pháp theo mode để tùy chỉnh việc nhân dạng biển số
    /// </summary>
    class DetectControl
    {
        /// <summary>
        /// Hàm điều khiển việc nhận dạng biển số theo phương pháp nào tùy vào người dùng cài đặt ở Appconfig
        /// Mode 0 : Chỉ nhận dạng bằng ARH
        /// Mode 1 : Chỉ nhận dạng bằng PP
        /// Mode 2 : Nhận dạng bằng cả ARH và PP nhưng vẫn ưu tiên nhận dạng bằng ARH trước
        /// </summary>
        /// <param name="path">Đường dẫn file ảnh cần nhận dạng biển số</param>
        /// <param name="includeMMC">Cờ xác định xem có bao gồm MMC hay không</param>
        /// <param name="laneInfo">Thông tin về làn đường</param>
        /// <param name="result">Kết quả nhận dạng biển số được trả về thông qua tham chiếu</param>
        /// <returns>Thông tin biển số được trả về</returns>
        public static string DetectPlateWithModeNew(string path, bool includeMMC, LaneInfo laneInfo, ref string result)
        {
            try
            {
                // Nếu file đường dẫn nhận dạng biển số tồn tại
                if (File.Exists(path))
                {
                    string plate = "";

                    switch (AppSettings.ModeDetect)
                    {
                        case 0:
                            if (ARHDetect.AnprEntity == null && AppSettings.ModeDetect != 1)
                            {
                                ARHDetect.InitialARH();
                            }
                            plate = ARHDetect.GetPlateNumberNew(path, ARHDetect.AnprEntity?.anpr, ARHDetect.AnprEntity?.gxImage).ToString();
                            break;
                        case 1:
                            plate = PPDetect.GetPlateNumberNew(path, includeMMC, laneInfo);
                            break;
                        case 2:
                            if (ARHDetect.AnprEntity == null && AppSettings.ModeDetect != 1)
                            {
                                ARHDetect.InitialARH();
                            }
                            plate = ARHDetect.GetPlateNumberNew(path, ARHDetect.AnprEntity?.anpr, ARHDetect.AnprEntity?.gxImage).ToString();
                            if (string.IsNullOrEmpty(plate))
                            {
                                plate = PPDetect.GetPlateNumberNew(path, includeMMC, laneInfo);
                            }
                            break;
                        default:
                            Utilities.WriteErrorLog("[ DETECT_PLATE_WITH_MODE ]", "[ Please set up the mode as instructed ! ]");
                            break;
                    }
                    var file = new FileInfo(path);

                    Utilities.WriteOperationLog("[ DETECT_PLATE_WITH_MODE ]", $"[ {file.Name} - {plate} ]");
                    result = plate;
                    return plate;
                }
            }
            catch (Exception e)
            {
                Utilities.WriteErrorLog("[ DETECT_PLATE_WITH_MODE ]", $"[ {e.ToString()} ]");
            }

            result = "";
            return "";
        }
    }
}
