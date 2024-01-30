using System;
using System.Threading;
using System.Threading.Tasks;
using cm;
using gx;
using TN.Tollcollection.ALPR.Entity;

namespace TN.Tollcollection.ALPR.Detect
{
    /// <summary>
    /// Nhận dạng biển số từ ảnh bằng ARH
    /// </summary>
    class ARHDetect
    {
        // Xử lý 1 thread 1 lần - Các thread khác cho vào hàng đợi
        public static SemaphoreSlim semaphoreSlim = new SemaphoreSlim(1, 1);
        // Trạng thái khi bắt đầu scan key ARH đã được cắm vào hay chưa
        public static string ArhEngine = "";
        // Khai báo các thư viện nhận dạng của ARH để nó là duy nhất mà không cần phải khởi tạo nhiều lần
        public static ANPREntity AnprEntity;

        /// <summary>
        /// Khởi tạo kết nối với ARH để tiến hành nhận dạng biển số qua ảnh
        /// </summary>
        public static void InitialARH()
        {

            // Kiểm tra xem ứng dụng đang ở chế độ nhận dạng nào.
            if (AppSettings.ModeDetect != 1)
            {

                // Tạo đối tượng cmAnpr và gxImage.
                cmAnpr anpr = new cmAnpr("default");
                gxImage image = new gxImage("default");


                // Lấy thông tin động cơ ARH để kiểm tra giấy phép.
                ArhEngine = $"{anpr.GetProperty("anprname")}";


                // Nếu không tìm thấy giấy phép cho động cơ hiện tại, thông báo lỗi.
                if (!anpr.CheckLicenses4Engine("", 0))
                {
                    ArhEngine = "Cannot find licenses for the current engine !!!";
                    return;
                }

                // Khởi tạo đối tượng AnprEntity với thư viện nhận dạng và thư viện phân tích ảnh của ARH.
                AnprEntity = new ANPREntity(anpr, image);
            }
            else
            {
                // Nếu ở chế độ khác, chỉ khởi tạo đối tượng AnprEntity với giá trị null.
                AnprEntity = new ANPREntity (null, null);
            }
        }

        /// <summary>
        /// Hàm nhận dạng biển số qua ảnh
        /// </summary>
        /// <param name="imgPath">Đường dẫn ảnh</param>
        /// <param name="anpr">Thư viện nhận dạng ARH</param>
        /// <param name="image">Thư viên phân tích ảnh của ARH</param>
        /// <returns>Thông tin biển số và vị trí nếu có, ngược lại trả về chuỗi rỗng.</returns>
        public static async Task<string> GetPlateNumberNew(string imgPath, cmAnpr anpr, gxImage image)
        {
            try
            {
                // Biến lưu trữ thông tin biển số và vị trí
                var plateAndLocation = "";
                // Kiểm tra xem đường dẫn ảnh có khác null không.
                if (imgPath != null)
                {
                    // Load ảnh từ đường dẫn.
                    image.Load(imgPath);

                    // Biến đếm kết quả nhận dạng biển số.
                    int resultix = 0;

                    // Sử dụng SemaphoreSlim để đảm bảo chỉ có một luồng được phép truy cập vào anpr đồng thời.
                    await semaphoreSlim.WaitAsync();
                    bool found = false;

                    try
                    {
                        // Thử tìm kiếm biển số trong ảnh.
                        found = anpr.FindFirst(image);
                    }
                    catch
                    {
                        // Bỏ qua lỗi nếu có.
                        // ignored
                    }
                    finally
                    {
                        // Giải phóng SemaphoreSlim.
                        semaphoreSlim.Release();
                    }


                    // Lặp qua tất cả các kết quả nhận dạng.
                    while (found)
                    {
                        // Lấy mã quốc gia từ thư viện nhận dạng ARH.
                        String cc = anpr.GetCountryCode(anpr.GetType(), (int)CC_TYPE.CCT_COUNTRY_SHORT);
                        resultix++;

                        // Lấy thông tin vị trí.
                        var location = $"{anpr.GetFrame().x1}x{anpr.GetFrame().y1}|{anpr.GetFrame().x2}x{anpr.GetFrame().y2}|{anpr.GetFrame().x3}x{anpr.GetFrame().y3}|{anpr.GetFrame().x4}x{anpr.GetFrame().y4}";
                        // Ghi thông tin biển số và vị trí vào biến plateAndLocation.
                        plateAndLocation = $"{anpr.GetText()}|{location}";
                        //found = anpr.FindNext();

                        // Dừng vòng lặp vì chỉ quan tâm đến kết quả nhận dạng đầu tiên.
                        found = false;

                    }

                    // Trả về null nếu không có kết quả nào, ngược lại trả về thông tin biển số và vị trí.
                    return resultix == 0 ? null : plateAndLocation;
                }
                else
                {
                    // Nếu đường dẫn ảnh là null, trả về chuỗi rỗng.
                    return "";
                }
            }
            catch (gxException e)
            {
                // Ghi log lỗi nếu có lỗi xảy ra.
                Utilities.WriteErrorLog("[GET_PLATE_NUMBER]", $"[ERROR: {e}]");
                return "";
            }
        }
    }
}
