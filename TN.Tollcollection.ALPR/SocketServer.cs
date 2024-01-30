using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace TN.Tollcollection.ALPR
{
    /// <summary>
    /// Class socket server ALPR
    /// Mở kết nối cho client kết nối
    /// Lắng nghe các message từ client
    /// Duy trì kết nối với client bằng gói HealthCheck
    /// </summary>
    public class SocketServer
    {
        /// <summary>
        /// Trạng thái hiện tại của server
        /// </summary>
        private enum currentState
        {
            err = -1,
            stopped = 0,
            running = 1,
            idle = 2
        }

        public delegate void ClientConnectedHandler(SocketServer listener, string clientIp, bool status);
        public ClientConnectedHandler ClientConnected;
        public delegate void DataReceivedHandler(SocketServer listener, string clientIp, byte[] data, int byteCount);
        public DataReceivedHandler DataReceived;
        public delegate void ClientDisconnectedHandler(SocketServer listener, string clientIp);
        public ClientDisconnectedHandler ClientDisconnected;

        private TcpListener Listener;
        private int Port;
        private IPEndPoint localEndpoint;
        private Int32 newSessionId = 0;
        public bool IsRunning = false;
        private currentState serverState = currentState.stopped;
        private Sessions SessionCollection = new Sessions();
        private object SessionCollectionLocker = new object();
        private bool _activeHealthCheck = false;
        //private int _identifyTimeout = 0;
        private int _healthCheckTimeout = 0;
        private byte[] _healthCheckPackage;

        private Dictionary<string, SocketClientInfo> connectingClients;

        //Timer _verifyClientIdentityTimer;
        Timer _healthCheckTimer;


        /// <summary>
        /// Phương thức khởi tạo một đối tượng SocketServer. 
        /// </summary>
        /// <param name="localIp">Địa chỉ IP cục bộ mà server sẽ lắng nghe trên </param>
        /// <param name="prt">Cổng mà server sẽ lắng nghe.</param>
        /// <param name="activeHealthCheck">Cờ chỉ định xem kiểm tra sức khỏe (health check) sẽ được kích hoạt hay không</param>
        /// <param name="healthCheckTimeout">Thời gian chờ cho kiểm tra sức khỏe.</param>
        /// <param name="identifyTimeout">Thời gian chờ để xác định.</param>
        /// <param name="healthCheckPackage">Gói dữ liệu được sử dụng cho kiểm tra sức khỏe</param>
        public SocketServer(string localIp,int prt, bool activeHealthCheck, int healthCheckTimeout, int identifyTimeout, byte[] healthCheckPackage)
        {
            Port = prt;
            connectingClients = new Dictionary<string, SocketClientInfo>();
            localEndpoint = new IPEndPoint(IPAddress.Parse(localIp), Port);
            _activeHealthCheck = activeHealthCheck;
            //_identifyTimeout = identifyTimeout;
            _healthCheckTimeout = healthCheckTimeout;
            _healthCheckPackage = healthCheckPackage;
        }



        /// <summary>
        /// Khởi động server và bắt đầu lắng nghe kết nối từ clients.
        /// </summary>
        /// <returns>Trả về true nếu khởi động thành công, 
        /// false nếu server đã đang chạy (currentState.running) hoặc có lỗi khi khởi động.</returns>
        public bool Start()
        {
            if (serverState == currentState.running)
            {
                return false;
            }

            // Nếu trạng thái của server là không hoạt động
            serverState = currentState.idle;

            Thread listenerThread = new Thread(theListener);

            try
            {

                IsRunning = true;

                listenerThread.Name = "Server Listener Thread";
                listenerThread.Start();

                if (_activeHealthCheck)
                    StartTimer();
            }
            catch (Exception ex)
            {
                Utilities.WriteErrorLog("SocketServer_Start", ex.ToString());
                return false;
            }

            while (serverState != currentState.running)
            {
                Thread.Sleep(10);
                if (serverState == currentState.err | serverState == currentState.stopped)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Dừng hoạt động của server, đóng các kết nối và giải phóng tài nguyên.
        /// </summary>
        public void Stop()
        {
            IsRunning = false;
            serverState = currentState.stopped;

            try
            {
                if (_activeHealthCheck)
                {
                    StopTimer();
                    foreach (string key in connectingClients.Keys)
                    {
                        try
                        {
                            connectingClients[key]?.Close();
                        }
                        catch
                        {
                            // ignored
                        }
                    }
                    //foreach (string key in verifiedClients.Keys)
                    //{
                    //    try
                    //    {
                    //        verifiedClients[key]?.Close();
                    //    }
                    //    catch
                    //    {
                    //        // ignored
                    //    }
                    //}
                }

                Listener.Stop();
            }
            catch (Exception ex)
            {
                Utilities.WriteErrorLog("SocketServer_Close", ex.ToString());
            }

            try
            {
                SessionCollection.ShutDown();
            }
            catch (Exception ex)
            {
                Utilities.WriteErrorLog("SocketServer_Close_SessionClose", ex.ToString());
            }
        }

        public void StartTimer()
        {
            try
            {
                //if (_identifyTimeout > 0)
                //    _verifyClientIdentityTimer = new Timer(VerifyClientIdentity, null, _identifyTimeout / 2, _identifyTimeout / 2);
                if (_healthCheckTimeout > 0)
                    _healthCheckTimer = new Timer(CheckClientConnection, null, _healthCheckTimeout - 2000, _healthCheckTimeout - 2000);
            }
            catch (Exception ex)
            {
                Utilities.WriteErrorLog("SocketServer_StartTimer", ex.ToString());
            }
        }

        public void StopTimer()
        {
            try
            {
                //if (_verifyClientIdentityTimer != null)
                //{
                //    _verifyClientIdentityTimer.Dispose();
                //    _verifyClientIdentityTimer = null;
                //}

                if (_healthCheckTimer != null)
                {
                    _healthCheckTimer.Dispose();
                    _healthCheckTimer = null;
                }
            }
            catch (Exception ex)
            {
                Utilities.WriteErrorLog("SocketServer_StopTimer", ex.ToString());
            }
        }
        private void theListener()
        {

            try
            {
                // Start listening
                while (true)
                {
                    try
                    {
                        Listener = new TcpListener(localEndpoint);
                        Listener.Start();
                        break;
                    }
                    catch (Exception ex)
                    {
                        serverState = currentState.err;
                        Utilities.WriteErrorLog("SocketServer_theListener", ex.ToString());
                        var t = Task.Run(async delegate
                        {
                            await Task.Delay(5000).ConfigureAwait(false);
                        });
                        t.Wait();
                    }
                }
                Utilities.WriteOperationLog("SocketServer_theListener", $"Start listening on port {Port} on thread {Thread.CurrentThread.ManagedThreadId}");
                StartAccept();
            }
            catch (Exception ex)
            {
                serverState = currentState.err;
                Utilities.WriteErrorLog("SocketServer_theListener", ex.ToString());
                return;
            }

            serverState = currentState.running;
        }
        private bool StartAccept()
        {
            try
            {
                if (IsRunning)
                {
                    Listener.BeginAcceptTcpClient(HandleAsyncConnection, Listener);
                    return true;
                }
            }
            catch (Exception ex)
            {
                Utilities.WriteErrorLog("SocketServer_StartAccept", ex.ToString());
            }

            return false;
        }

        private void HandleAsyncConnection(IAsyncResult res)
        {
            try
            {
                TcpClient client = default(TcpClient);

                if (!StartAccept())
                    return;
                client = Listener.EndAcceptTcpClient(res);

                IPEndPoint endpoint = (IPEndPoint)client.Client.RemoteEndPoint;
                string clientIp = endpoint.Address.ToString();
                int port = endpoint.Port;
                if (_activeHealthCheck)
                {
                    // neu ton tai ket noi cu, dong ket noi va xoa khoi danh sach
                    if (connectingClients.ContainsKey(clientIp))
                    {
                        try
                        {
                            connectingClients[clientIp]?.Close();
                        }
                        catch
                        {
                            // ignored
                        }

                        connectingClients.Remove(clientIp);
                        Utilities.WriteErrorLog("SocketServer_HandleAsyncConnection", $"{clientIp}:{port} is existed, close it first.");
                    }

                    connectingClients.Add(clientIp, new SocketClientInfo(client));
                }

                Utilities.WriteErrorLog("Server", $"{clientIp}:{port} connected to port {Port} on thread {Thread.CurrentThread.ManagedThreadId}");
                //Utilities.WriteOperationLog("SocketServer_HandleAsyncConnection", $"{clientIp}:{port} connected to port {Port} on thread {Thread.CurrentThread.ManagedThreadId}");

                if (ClientConnected != null)
                {
                    ClientConnected(this, clientIp, true);
                }
                Task.Factory.StartNew(() => { HandleNewConnection(client); });
            }
            catch (Exception ex)
            {
                Utilities.WriteErrorLog("SocketServer_HandleAsyncConnection", ex.ToString());
            }
        }

        private object sessionIdIncrementLock = new object();

        private void HandleNewConnection(TcpClient client)
        {
            Int32 thisSessionId = -1;
            SessionCommunications session = null;

            if (thisSessionId == -1)
            {
                lock (sessionIdIncrementLock)
                {
                    thisSessionId = newSessionId;
                    newSessionId += 1;
                }
            }

            Thread newSession = new Thread(Run);
            session = new SessionCommunications(client, thisSessionId);
            newSession.IsBackground = true;
            newSession.Name = "Server Session #" + thisSessionId;
            newSession.Start(session);

            SessionCollection.AddSession(session);
        }

        private void Run(object _session)
        {
            SessionCommunications session = (SessionCommunications)_session;

            TcpClient Server = default(TcpClient);
            NetworkStream Stream = default(NetworkStream);
            IPEndPoint IpEndPoint = default(IPEndPoint);
            byte[] tmp = new byte[2];
            string clientIp = "";
            int port = 0;

            try
            {
                // Create a local Server and Stream objects for clarity.
                Server = session.theClient;
                Stream = Server.GetStream();
            }
            catch (Exception ex)
            {
                // An unexpected error.
                Utilities.WriteErrorLog("SocketServer_Run_GetStream", ex.ToString());
                return;
            }

            try
            {
                // Get the remote machine's IP address.
                IpEndPoint = (IPEndPoint)Server.Client.RemoteEndPoint;
                session.remoteIpAddress = IpEndPoint.Address;

                // no delay on partially filled packets...
                // Send it all as fast as possible.
                Server.NoDelay = true;
                session.IsRunning = true;

                IPEndPoint endpoint = (IPEndPoint)Server.Client.RemoteEndPoint;
                clientIp = endpoint.Address.ToString();
                port = IpEndPoint.Port;

                // Start the communication loop
                byte[] message = new byte[4096];
                int bytesRead;

                do
                {
                    bytesRead = 0;
                    try
                    {
                        bytesRead = Stream.Read(message, 0, message.Length);
                    }
                    catch
                    {
                        break;
                    }

                    if (bytesRead == 0)
                        break;

                    //clientStream.Flush();

                    byte[] data = new byte[bytesRead];
                    Array.Copy(message, data, bytesRead);

                    //Utilities.WriteErrorLog($"Server_Received_{clientIp}: ", Encoding.UTF8.GetString(data) + " - " + Utilities.GetBitStr(data));

                    if (_activeHealthCheck)
                    {
                        if (connectingClients.ContainsKey(clientIp))
                        {
                            connectingClients[clientIp].ConnectedTime = DateTime.Now;
                        }
                    }

                    if (DataReceived != null)
                        DataReceived(this, session.remoteIpAddress.ToString(), data, bytesRead);
                } while (true);
            }
            catch (Exception ex)
            {
                Utilities.WriteErrorLog("SocketServer_Run_Receive", ex.ToString());
            }

            try
            {
                //if (Server != null && Server.Client != null)
                //{
                Stream?.Close();
                Stream?.Dispose();
                //Server.Client.Blocking = false;
                //Server.Client.Close();
                Server?.Close();
                //Thread.Sleep(10);

                Utilities.WriteErrorLog("SocketServer", $"{clientIp} disconnected on port {Port} on thread {Thread.CurrentThread.ManagedThreadId}");
                //Utilities.WriteOperationLog("SocketServer_Run", $"{clientIp} disconnected on port {Port} on thread {Thread.CurrentThread.ManagedThreadId}");
                if (ClientDisconnected != null)
                {
                    ClientDisconnected(this, clientIp);
                }
            }
            catch (Exception ex)
            {
                Utilities.WriteErrorLog("SocketServer_Run_CloseClient", ex.ToString());
            }

            session.IsRunning = false;
        }

        private void CheckClientConnection(Object stateInfo)
        {
            if (!_activeHealthCheck)
                return;

            try
            {
                List<string> removeKeys = new List<string>();
                foreach (string key in connectingClients.Keys)
                {
                    try
                    {
                        //if (connectingClients[key].ConnectedTime.AddMilliseconds(_healthCheckTimeout * 2) < DateTime.Now)
                        //{
                        //    try
                        //    {
                        //        // qua thoi gian xac minh, dong ket noi
                        //        connectingClients[key]?.Close();
                        //    }
                        //    catch
                        //    {

                        //    }
                        //    removeKeys.Add(key);
                        //    Utilities.WriteErrorLog("SocketServer_CheckClientConnection_" + key, $"{key} is inactive too long, close and remove it.");
                        //    Utilities.WriteOperationLog("SocketServer_CheckClientConnection_" + key, $"{key} is inactive too long, close and remove it.");
                        //    continue;
                        //}

                        TcpClient client = connectingClients[key].Client;
                        if (client != null && client.Connected)
                        {
                            // broadcast health check messgae
                            //Utilities.WriteErrorLog($"Server_HealthCheck_{key}: ", Encoding.UTF8.GetString(_healthCheckPackage) + " - " + Utilities.GetBitStr(_healthCheckPackage));
                            NetworkStream clientStream = client.GetStream();
                            clientStream.Write(_healthCheckPackage, 0, _healthCheckPackage.Length);
                            clientStream.Flush();
                        }
                        //else
                        //{
                        //    try
                        //    {
                        //        // qua thoi gian xac minh, dong ket noi
                        //        client?.Close();
                        //    }
                        //    catch
                        //    {
                        //    }
                        //    removeKeys.Add(key);
                        //}
                    }
                    catch (Exception ex)
                    {
                        Utilities.WriteErrorLog("SocketServer_CheckClientConnection", ex.ToString());
                    }
                }

                foreach (string key in removeKeys)
                    connectingClients.Remove(key);
            }
            catch (Exception ex)
            {
                Utilities.WriteErrorLog("SocketServer_CheckClientConnection", ex.ToString());
            }
        }

        public bool SendToClient(string clientIp, byte[] data)
        {
            if (connectingClients.ContainsKey(clientIp))
            {
                try
                {
                    TcpClient client = connectingClients[clientIp].Client;
                    if (client != null && client.Connected)
                    {
                        // broadcast health check messgae
                        //Utilities.WriteErrorLog($"Server_Send_{clientIp}: ", Encoding.UTF8.GetString(data) + " - " + Utilities.GetBitStr(data));
                        NetworkStream clientStream = client.GetStream();
                        clientStream.Write(data, 0, data.Length);
                        clientStream.Flush();
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    Utilities.WriteErrorLog("SocketServer_SendToClient", ex.ToString());
                }
            }

            return false;
        }

        public class SocketClientInfo
        {
            public string Key { get; set; }
            public TcpClient Client { get; set; }
            public DateTime LastHealthCheck { get; set; }
            public DateTime ConnectedTime { get; set; }

            public SocketClientInfo(TcpClient client)
            {
                Client = client;
                LastHealthCheck = DateTime.Now;
                ConnectedTime = DateTime.Now;
            }

            public void Close()
            {
                try
                {
                    NetworkStream stream = Client?.GetStream();
                    stream?.Close();
                    stream?.Dispose();
                }
                catch
                {
                    // ignored
                }
                Client?.Close();
                Client = null;
            }
        }

        public class SessionCommunications
        {
            public TcpClient theClient;
            public bool IsRunning = false;
            public IPAddress remoteIpAddress;
            public Int32 sessionID;
            public bool disConnect = false;
            public bool paused;
            public bool pauseSent;

            public bool shuttingDown;

            public SessionCommunications(TcpClient _theClient, Int32 _sessionID)
            {
                theClient = _theClient;
                sessionID = _sessionID;
                paused = false;
                pauseSent = false;
                shuttingDown = false;
            }

            // Optional ByVal wait As Int32 = 500
            public void Close()
            {
                Thread bgThread = new Thread(WaitClose);
                bgThread.Start();
            }


            private void WaitClose()
            {
                shuttingDown = true;
                disConnect = true;
                try
                {
                    theClient?.Close();
                }
                catch (Exception ex)
                {
                    Utilities.WriteErrorLog("Session_WaitClose", ex.ToString());
                }
            }
        }
        private class Sessions
        {
            private List<SessionCommunications> sessionCollection = new List<SessionCommunications>();
            private object sessionLockObject = new object();

            public void AddSession(SessionCommunications theNewSession)
            {
                Task.Factory.StartNew(() => { bgAddSession(theNewSession); });
            }

            private void bgAddSession(SessionCommunications theNewSession)
            {
                lock (sessionLockObject)
                {
                    if (sessionCollection.Count > theNewSession.sessionID)
                    {
                        sessionCollection[theNewSession.sessionID] = null;
                        sessionCollection[theNewSession.sessionID] = theNewSession;
                    }
                    else
                    {
                        sessionCollection.Add(theNewSession);
                    }
                }
            }


            public bool GetSession(Int32 sessionID, ref SessionCommunications session)
            {
                try
                {
                    session = sessionCollection[sessionID];
                    if (session == null)
                        return false;
                    if (!session.IsRunning)
                        return false;
                    return true;
                }
                catch (Exception ex)
                {
                    Utilities.WriteErrorLog("Session_GetSession", ex.ToString());
                    return false;
                }
            }

            public List<SessionCommunications> GetSessionCollection()
            {
                List<SessionCommunications> thisCopy = new List<SessionCommunications>();

                lock (sessionLockObject)
                {
                    for (Int32 i = 0; i <= sessionCollection.Count - 1; i++)
                    {
                        thisCopy.Add(sessionCollection[i]);
                    }
                }

                return thisCopy;
            }

            public void ShutDown()
            {
                lock (sessionLockObject)
                {
                    foreach (SessionCommunications session in sessionCollection)
                    {
                        try
                        {
                            if (session != null && session.IsRunning)
                                session.Close();
                        }
                        catch (Exception ex)
                        {
                            Utilities.WriteErrorLog("Session_ShutDown", ex.ToString());
                        }
                    }
                }
            }
        }
    }
}