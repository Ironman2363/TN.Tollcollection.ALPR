using System;
using System.Diagnostics;
using System.Linq;
using Newtonsoft.Json;
using RestSharp;
using TN.Tollcollection.ALPR.Entity;
using TN.Tollcollection.ALPR.LaneConfig;

namespace TN.Tollcollection.ALPR.Detect
{
    /// <summary>
    /// Nhận dạng biển số bằng ParkPow (PP)
    /// </summary>
    public class PPDetect
    {
        /// <summary>
        /// Hàm nhận dạng biển số qua API của PP
        /// </summary>
        /// <param name="pathFile">Đường dẫn file ảnh cần nhận dạng biển số</param>
        /// <param name="includeMMC">Có bao gồm dữ liệu MMC hay không.</param>
        /// <param name="laneInfo">Thông tin làn đường.</param>
        /// <returns>Thông tin biển số và vị trí nếu có, ngược lại trả về chuỗi rỗng.</returns>
        internal static string GetPlateNumberNew(string pathFile, bool includeMMC, LaneInfo laneInfo)
        {
            var stopWatch = new Stopwatch();

            // Biến lưu trữ thông tin biển số và vị trí kèm theo dữ liệu MMC (Make, Model, Color).
            string plateAndLocationAndMmc = "";

            //var apiUrl;
            // Xây dựng URL gọi API dựa trên cấu hình ứng dụng và quyết định có bao gồm dữ liệu MMC hay không.
            string apiUrl = "";
            if (AppSettings.ModeApi == 0)
            {
                apiUrl = AppSettings.ApiPlateRecognizerEntity;
            }
            if (AppSettings.ModeApi == 1)
            {
                apiUrl = AppSettings.ApiPlateRecognizerEntity1;
            }
            string url = apiUrl;
            // Kiểm tra nếu cấu hình yêu cầu bao gồm dữ liệu MMC và URL chưa chứa tham số 'mmc=true'
            if (includeMMC && !url.Contains("mmc=true"))
                // Thêm tham số 'mmc=true' vào URL để yêu cầu dữ liệu MMC từ API (ParkPow)
                url += "&mmc=true";

            Utilities.WriteDebugLog("START ANPR (5.1.1 - PP):", $"ClientIp: {laneInfo.IpPcLane} | Lane: {laneInfo.Id} | Start PP in: {DateTime.Now:yyyy/MM/dd HH:mm:ss.ffffff} | 1-MMC-{includeMMC} | Path: {pathFile} | UrlPP: {url}");

            // Bắt đầu đồng hồ đếm thời gian.
            stopWatch.Start();

            // Tạo đối tượng RestClient để gọi API PP.

            // Tạo đối tượng RestClient để gửi yêu cầu HTTP đến một địa chỉ cụ thể.
            // Timeout được đặt thành -1 để bỏ qua giới hạn thời gian chờ, cho phép yêu cầu chờ đến khi nhận được phản hồi từ máy chủ mà không bị hủy.
            var client = new RestClient(url) { Timeout = -1 };
            // Tạo đối tượng RestRequest để định rõ yêu cầu HTTP và phương thức là POST.
            var request = new RestRequest(Method.POST);
            // Thêm header Authorization vào yêu cầu để xác thực.
            request.AddHeader("Authorization", AppSettings.TokenPP);

            // Thêm tệp tin (file) vào yêu cầu để gửi lên máy chủ.
            // "upload" là tên tham số mà máy chủ yêu cầu khi nhận dữ liệu từ file.
            // "pathFile" là đường dẫn đến tệp tin cần gửi.
            request.AddFile("upload", pathFile);

            // Thực hiện cuộc gọi API.
            var response = client.Execute(request);
            //Utilities.WriteOperationLog("[CallAPI]", $"[PathFile: {pathFile}] -- [{JsonConvert.SerializeObject(response)}]");

            // Gửi yêu cầu HTTP tới máy chủ và chờ đợi phản hồi.
            // Biến "response" lưu trữ phản hồi từ máy chủ sau khi gửi yêu cầu.
            var status = (int)response.StatusCode;

            // Kiểm tra mã trạng thái của phản hồi HTTP để xác định xem cuộc gọi API có thành công hay không.
            // Nếu mã trạng thái nằm ngoài khoảng từ 100 đến 300 (bao gồm cả 100 và 300), tức là không thành công,
            // trong trường hợp này, trả về một chuỗi rỗng để biểu thị rằng không có dữ liệu nào được nhận dạng.
            if (status <= 100 || status >= 300)
            {
                // Nếu cuộc gọi không thành công, trả về chuỗi rỗng.
                return "";
            }
            // Phân tích kết quả trả về từ API PP.
            // Giải mã nội dung của phản hồi từ API thành một đối tượng có kiểu là ANPRPlatePP

            // Chuyển đổi chuỗi JSON từ phản hồi thành đối tượng ANPRPlatePP bằng phương thức DeserializeObject của lớp JsonConvert
            ANPRPlatePP data = null; // Khai báo biến data ở ngoài phạm vi của các vòng điều kiện để đảm bảo truy cập trong toàn bộ phạm vi

            if (AppSettings.ModeApi == 0)
            {
                var dataMode0 = JsonConvert.DeserializeObject<ANPRPlatePP>(response.Content);
                data = dataMode0;
            }
            if (AppSettings.ModeApi == 1)
            {
                var dataMode1 = JsonConvert.DeserializeObject<ANPRPlatePPData>(response.Content);
                data = new ANPRPlatePP(dataMode1);
            }

            var plateRecognizer = data;

            stopWatch.Stop();

            // Ghi log debug về kết quả nhận dạng từ API PP.
            Utilities.WriteDebugLog("START ANPR (5.1.2 - PP):", $"ClientIp: {laneInfo.IpPcLane} | Lane: {laneInfo.Id} | Plate: {plateRecognizer.results?.FirstOrDefault()?.plate} | Path: {pathFile} | Total Time PP: {plateRecognizer.processing_time} ms | Total Time CallPP: {stopWatch.ElapsedMilliseconds} ms");

            // Bắt đầu đồng hồ đếm thời gian cho việc xử lý thông tin MMC.
            stopWatch.Restart();
            // Trích xuất thông tin biển số và vị trí nếu có.
            var results = plateRecognizer.results?.FirstOrDefault();
            Console.WriteLine(results);
            if (results != null)
            {
                plateAndLocationAndMmc = results.plate;
                // Xử lý thông tin MMC.

                try
                {
                    var makeModel = results.model_make;
                    var type = results.vehicle.type;
                    var color = results.color?.FirstOrDefault();
                    var box = results.box;
                    var location = $"{box?.xmin}x{box?.ymin}|{box?.xmax}x{box?.ymin}|{box?.xmax}x{box?.ymax}|{box?.xmin}x{box?.ymax}";
                    if (!string.IsNullOrEmpty(plateAndLocationAndMmc))
                    {
                        plateAndLocationAndMmc += $"|{location}";
                        if (makeModel?.Length > 2)
                        {
                            var make1 = makeModel[0];
                            var make2 = makeModel[1];
                            plateAndLocationAndMmc += $"|{make1?.make}|{make1?.model}|{color?.color}|{make2?.model}";
                        }
                        else
                        {
                            var make1 = makeModel?[0];
                            plateAndLocationAndMmc += $"|{make1?.make}|{make1?.model}|{color?.color}|";
                        }

                        plateAndLocationAndMmc += $"|{type}";
                    }
                }
                catch (System.Exception e)
                {
                    Utilities.WriteErrorLog("results.plate error", e.ToString());
                }
            }

            // Dừng đồng hồ đếm thời gian cho việc xử lý thông tin MMC.
            stopWatch.Stop();

            // Ghi log debug về thời gian xử lý thông tin MMC.
            Utilities.WriteDebugLog("START ANPR (5.1.3 - PP):", $"ClientIp: {laneInfo.IpPcLane} | Lane: {laneInfo.Id} | Data: \" {plateAndLocationAndMmc} \" | Time Processing MMC: {stopWatch.ElapsedMilliseconds} ms");


            // Trả về thông tin biển số và vị trí kèm theo thông tin MMC.
            return plateAndLocationAndMmc;
        }
    }
}