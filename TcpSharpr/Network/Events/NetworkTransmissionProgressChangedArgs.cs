using System;
using System.Collections.Generic;
using System.Text;

namespace TcpSharpr.Network.Events {
    public class NetworkTransmissionProgressChangedArgs : EventArgs {
        public double Progress { get; private set; }

        public NetworkTransmissionProgressChangedArgs(double progress) {
            Progress = progress;
        }
    }
}
