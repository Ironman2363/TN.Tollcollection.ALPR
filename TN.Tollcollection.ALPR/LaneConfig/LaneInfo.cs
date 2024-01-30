

namespace TN.Tollcollection.ALPR.LaneConfig
{
    /// <summary>
    /// Thông tin về 1 làn xe khi kết nối tới services thông qua Socket TCP/IP
    /// </summary>
    public class LaneInfo
    {
        public string Id { get; set; }
        public string IpPcLane { get; set; }
        public string IpCameraLpn { get; set; }
        public string IpCameraLane { get; set; }
        public string UserCamLane { get; set; }
        public string PassCamLane { get; set; }
        public SyncSocketClient SocketClient { get; set; }

        public LaneInfo(string id, string ipPcLane, string ipCameraLpn, string ipCameraLane, string userCamLane, string passCamLane, SyncSocketClient socketClient)
        {
            Id = id;
            IpPcLane = ipPcLane;
            IpCameraLpn = ipCameraLpn;
            IpCameraLane = ipCameraLane;
            UserCamLane = userCamLane;
            PassCamLane = passCamLane;
            SocketClient = socketClient;
        }
    }
}
