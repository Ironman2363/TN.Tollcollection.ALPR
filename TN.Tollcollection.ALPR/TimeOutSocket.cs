using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace TN.Tollcollection.ALPR
{
    // Dùng để kết nối với server
    public class TimeOutSocket
    {
        private static bool _isConnectionSuccessful = false;
        private static Exception _socketException;
        //TODO: Check if readonly works
        private static readonly ManualResetEvent TimeoutObject = new ManualResetEvent(false);
        
        public static TcpClient Connect(IPEndPoint remoteEndPoint, int timeoutMSec)
        {
            TimeoutObject.Reset();
            _socketException = null;

            string serverip = Convert.ToString(remoteEndPoint.Address);
            int serverport = remoteEndPoint.Port;
            // Tạo một đối tượng TcpClient.
            TcpClient tcpclient = new TcpClient();

            // Ở đây, tcpclient chỉ là một đối tượng được tạo ra nhưng chưa kết nối với bất kỳ máy chủ nào.

            // Đối tượng TcpClient này có thể được sử dụng để thiết lập kết nối với một máy chủ TCP cụ thể.
            // Để làm điều này, bạn cần sử dụng các phương thức như Connect() để kết nối đến một địa chỉ IP và cổng xác định.

            tcpclient.BeginConnect(serverip, serverport, new AsyncCallback(CallBackMethod), tcpclient);
            // Sử dụng kết nối không đồng bộ dể kết nối đến máy chủ
            // Đợi cho đến khi quá trình kết nối hoàn tất hoặc đã qua thời gian chờ.
            if (TimeoutObject.WaitOne(timeoutMSec, false))
            {
                // Kiểm tra xem kết nối có thành công hay không.
                if (_isConnectionSuccessful)
                {
                    // Nếu thành công, trả về đối tượng TcpClient.
                    return tcpclient;
                }
                else
                {
                    // Nếu không thành công, trả về null
                    return null;
                    //throw _socketException;
                }
            }
            else
            {
                // Đóng TcpClient và ném ngoại lệ TimeoutException nếu đã qua thời gian chờ.
                tcpclient.Close();
                throw new TimeoutException("TimeOut Exception");
            }
        }
        private static void CallBackMethod(IAsyncResult asyncresult)
        {
            try
            {
                _isConnectionSuccessful = false;
                TcpClient tcpclient = asyncresult.AsyncState as TcpClient;

                if (tcpclient.Client != null)
                {
                    tcpclient.EndConnect(asyncresult);
                    _isConnectionSuccessful = true;
                }
            }
            catch (Exception ex)
            {
                _isConnectionSuccessful = false;
                _socketException = ex;
                Utilities.WriteErrorLog("TimeOutSocket_CallBackMethod", ex.ToString());
            }
            finally
            {
                TimeoutObject.Set();
            }
        }
    }
}
