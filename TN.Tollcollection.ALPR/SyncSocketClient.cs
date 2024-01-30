using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace TN.Tollcollection.ALPR
{

    /// <summary>
    /// Đối tượng đại diện cho một client TCP đồng bộ.
    /// </summary>
    public class SyncSocketClient
    {
        public delegate void ConnectDoneHandler(SyncSocketClient client, bool status);
        public ConnectDoneHandler ConnectDone;
        public delegate void SendCommandDoneHandler(SyncSocketClient client, bool status);
        public SendCommandDoneHandler SendCommandDone;
        public delegate void DataReceivedHandler(SyncSocketClient client, string data);
        public DataReceivedHandler DataReceived;


        // khởi tạo thuộc tính của đối tượng
        public string HostIp { get; set; }
        public int Port { get; set; }
        public int MillisecondTimeout { get; set; }
        public bool KeepConnection { get; set; }
        public bool ActiveHealthCheck { get; set; }
        public int HealthCheckInterval { get; set; }
        public string IdentityKey { get; set; }

        private TcpClient _client;
        private static NetworkStream _clientStream;
        Timer _healthCheckTimer;
        private byte[] _healthCheckPackage;
        private DateTime _lastConnection;

        // Khởi tạo Constructor có các tham số
        public SyncSocketClient(string hostIp, int port, int millisecondTimeout, bool keepConnection, bool activeHealthCheck, int healthCheckInterval, byte[] healthCheckPackage, string id)
        {
            this.HostIp = hostIp;
            this.Port = port;
            this.MillisecondTimeout = millisecondTimeout;
            this.KeepConnection = keepConnection;
            this.ActiveHealthCheck = activeHealthCheck;
            this.HealthCheckInterval = healthCheckInterval;
            _healthCheckPackage = healthCheckPackage;
        }



        /// <summary>
        /// Thiết lập kết nối TCP đến một máy chủ với địa chỉ IP và cổng.
        /// Chuẩn bị cho việc nhận dữ liệu từ server.
        /// </summary>
        public void Start()
        {
            try
            {
                //_client = new TcpClient(new IPEndPoint(IPAddress.Parse(_clientIp), _clientPort));
                // Khởi tạo đối tượng của lớp TcpClient
                _client = new TcpClient();
                // Đoạn mã này tạo một đối tượng IPAddress từ một chuỗi đại diện cho địa chỉ IP.
                // Chú ý: HostIp cần phải là một chuỗi đúng định dạng địa chỉ IP (IPv4 hoặc IPv6).
                IPAddress ipAddress = IPAddress.Parse(HostIp);

                // Đoạn mã dưới đây tạo một đối tượng IPEndPoint, kết hợp địa chỉ IP và cổng (port).
                // Đối tượng IPEndPoint này có thể được sử dụng để thiết lập kết nối mạng hoặc lắng nghe kết nối đến.
                // Port là một số nguyên đại diện cho cổng kết nối.
                IPEndPoint remoteEP = new IPEndPoint(ipAddress, Port);

                // Lưu ý: Port cần phải là một số nguyên dương hợp lệ (ví dụ: 8080) để đảm bảo đối tượng IPEndPoint được tạo thành công.


                // Kết nối với 192.168.2.74:9001 và với thời gian 
                _client = TimeOutSocket.Connect(remoteEP, MillisecondTimeout);
                _lastConnection = DateTime.Now;

                if (this.ActiveHealthCheck)
                {
                    StartTimer();
                    // nhan phan hoi tu server
                    try
                    {
                        // Khởi tạo đối tượng chạy đa luồng
                        Thread _thread = new Thread(ReceiveData);
                        // Khởi chạy luồng và thực hiện công việc nhận dữ liệu ReceiveData
                        _thread.Start();
                    }
                    catch
                    {
                        // ignore
                    }
                }
            }
            catch (Exception ex)
            {
                Utilities.WriteErrorLog($"SyncSocketClient_Start_{HostIp}_{Port}", ex.ToString());
                _client?.Close();
                //throw ex;
            }
        }

        /// <summary>
        /// Dừng kết nối và dừng Timer kiểm tra kết nối.
        /// </summary>
        public void Stop()
        {
            try
            {
                if (this.ActiveHealthCheck)
                    StopTimer();

                //_clientStream?.Close();
                _client?.Close();
            }
            catch (Exception ex)
            {
                Utilities.WriteErrorLog($"SyncSocketClient_Stop_{HostIp}_{Port}", ex.ToString());
                //throw ex;
            }
        }

        /// <summary>
        /// Khởi động lại kết nối.
        /// </summary>
        public void Restart()
        {
            try
            {
                // mat ket noi, khoi dong lai socket
                Stop();
                //Thread.Sleep(10);
                Start();
            }
            catch (Exception ex)
            {
                Utilities.WriteErrorLog($"SyncSocketClient_Restart_{HostIp}_{Port}", ex.ToString());
            }
        }


        /// <summary>
        /// Khởi động Timer kiểm tra kết nối.
        /// </summary>
        public void StartTimer()
        {
            try
            {
                if (this.HealthCheckInterval > 0)
                    _healthCheckTimer = new Timer(CheckConnection, null, this.HealthCheckInterval, this.HealthCheckInterval);
            }
            catch (Exception ex)
            {
                Utilities.WriteErrorLog("SyncSocketClient_StartTimer", ex.ToString());
            }
        }


        /// <summary>
        /// Dừng và giải phóng tài nguyên của đối tượng Timer được sử dụng để kiểm tra kết nối.
        /// </summary>
        public void StopTimer()
        {
            try
            {
                if (_healthCheckTimer != null)
                {
                    _healthCheckTimer.Dispose();
                    _healthCheckTimer = null;
                }
            }
            catch (Exception ex)
            {
                Utilities.WriteErrorLog("SyncSocketClient_StopTimer", ex.ToString());
            }
        }



        /// <summary>
        /// Gửi dữ liệu từ client đến server thông qua kết nối TCP.
        /// </summary>
        /// <param name="data">Dữ liệu cần gửi từ client đến server dưới dạng chuỗi.</param>
        /// <returns>Trả về true nếu quá trình gửi dữ liệu thành công và false nếu có lỗi xảy ra.</returns>
        /// <remarks>
        /// Phương thức này chuyển đổi dữ liệu từ chuỗi thành mảng byte sử dụng mã hóa UTF-8, sau đó gọi phương thức Send(byte[] buffer) để thực hiện quá trình gửi.
        /// </remarks>
        public bool Send(string data)
        {
            byte[] buffer = Encoding.UTF8.GetBytes(data);
            return Send(buffer);
        }

        /// <summary>
        /// Gửi dữ liệu từ client đến server thông qua kết nối TCP.
        /// </summary>
        /// <param name="buffer">Mảng byte chứa dữ liệu cần gửi từ client đến server thông qua kết nối TCP.</param>
        /// <returns>Trả về true nếu quá trình gửi dữ liệu thành công và false nếu có lỗi xảy ra.</returns>
        /// <remarks>
        /// Phương thức này thực hiện việc gửi một mảng byte buffer qua kết nối TCP và cập nhật thông tin về thời điểm kết nối cuối cùng được thực hiện.
        /// Trong quá trình gửi, nếu kết nối bị mất, phương thức sẽ thử khởi động lại kết nối và thực hiện gửi lại dữ liệu.
        /// Nếu không giữ kết nối (_keepConnection = false), phương thức đồng thời đóng kết nối sau khi gửi dữ liệu.
        /// </remarks>
        public bool Send(byte[] buffer)
        {
            try
            {
                try
                {
                    
                    if (_client == null /*|| !_client.Connected*/)
                    {
                        Utilities.WriteErrorLog($"SyncSocketClient_Restart_{HostIp}_{Port}", "Connection is disconnected, restarting...");
                        Restart();
                    }

                    _clientStream = _client.GetStream();
                    _clientStream.Write(buffer, 0, buffer.Length);
                    _clientStream.Flush();

                    _lastConnection = DateTime.Now;

                    //if (buffer == _healthCheckPackage)
                    //    Utilities.WriteErrorLog($"Client_HealthCheck_Response_{HostIp}_{Port}: ", Encoding.UTF8.GetString(buffer) + " - " + Utilities.GetBitStr(buffer));
                    //else
                    //    Utilities.WriteErrorLog($"Client_Sent_{HostIp}_{Port}: ", Encoding.UTF8.GetString(buffer) + " - " + Utilities.GetBitStr(buffer));
                }
                catch
                {
                    Utilities.WriteErrorLog($"SyncSocketClient_Restart_{HostIp}_{Port}", "Failed to send, restarting...");
                    Restart();

                    if (_client != null /*&& _client.Connected*/)
                    {
                        _clientStream = _client.GetStream();
                        _clientStream.Write(buffer, 0, buffer.Length);
                        _clientStream.Flush();

                        _lastConnection = DateTime.Now;

                        //if (buffer == _healthCheckPackage)
                        //    Utilities.WriteErrorLog($"Client_HealthCheck_Response_{HostIp}_{Port}: ", Encoding.UTF8.GetString(buffer) + " - " + Utilities.GetBitStr(buffer));
                        //else
                        //    Utilities.WriteErrorLog($"Client_Sent_{HostIp}_{Port}: ", Encoding.UTF8.GetString(buffer) + " - " + Utilities.GetBitStr(buffer));
                    }
                }
                finally
                {
                    if (!this.KeepConnection)
                    {
                        try
                        {
                            //_clientStream?.Close();
                            _client?.Close();
                        }
                        catch
                        {
                            // ignored
                        }
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                string data = Encoding.UTF8.GetString(buffer, 0, buffer.Length);
                Utilities.WriteErrorLog($"SyncSocketClient_Send_{HostIp}_{Port}_{data}", ex.ToString());

                return false;
            }
        }


        /// <summary>
        /// Lắng nghe dữ liệu từ kết nối TCP tới client và xử lý các gói dữ liệu nhận được,
        /// bao gồm việc gửi phản hồi về server khi nhận được thông điệp kiểm tra sức khỏe.
        /// </summary>
        private void ReceiveData()
        {
            try
            {
                if (_client != null /*&& _client.Connected*/)
                {
                    _clientStream = _client.GetStream();

                    while (true)
                    {
                        try
                        {
                            int bytesRead = 0;
                            byte[] response = new byte[_client.ReceiveBufferSize];

                            try
                            {
                                bytesRead = _clientStream.Read(response, 0, response.Length);
                            }
                            catch
                            {
                                break;
                            }

                            if (bytesRead > 0)
                            {
                                byte[] data = new byte[bytesRead];
                                Array.Copy(response, data, bytesRead);
                                //Utilities.WriteErrorLog($"Client_Received_{HostIp}_{Port}: ", Encoding.UTF8.GetString(data) + " - " + Utilities.GetBitStr(data));
                                // receive health check message, response to server
                                _lastConnection = DateTime.Now;
                                Send(_healthCheckPackage);
                            }
                            else
                            {
                                // The connection has closed gracefully, so stop the
                                // thread.
                                break;
                            }
                        }
                        catch
                        {
                            // Handle the exception...
                            break;
                            //Utilities.WriteErrorLog("SyncSocketClient_ReceiveData_Loop", ex1.ToString());
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Utilities.WriteErrorLog($"SyncSocketClient_ReceiveData_{HostIp}_{Port}", ex.ToString());
            }
            finally
            {
                // mat ket noi, restart socket
            }
        }

        /// <summary>
        /// Kiểm tra trạng thái kết nối và khởi động lại socket nếu đã mất kết nối.
        /// </summary>
        /// <param name="stateInfo">Thông tin trạng thái từ hàm gọi. Trong trường hợp này, không sử dụng thông tin trạng thái này.</param>
        private void CheckConnection(Object stateInfo)
        {
            if (!this.ActiveHealthCheck)
                return;

            try
            {
                if (_lastConnection.AddMilliseconds(this.HealthCheckInterval * 2) < DateTime.Now)
                {
                    // mat ket noi qua thoi gian cho phep, khoi dong lai socket
                    Utilities.WriteErrorLog($"SyncSocketClient_Restart_{HostIp}_{Port}", "Connection is inactive, restarting...");
                    Restart();
                    return;
                }
            }
            catch (Exception ex)
            {
                Utilities.WriteErrorLog($"SyncSocketClient_CheckConnection_{HostIp}_{Port}", ex.ToString());
            }
        }
    }
}
