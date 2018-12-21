using System;

namespace TcpSharpr.Network.Events {
    public class StatusChangedEventArgs : EventArgs {
        public bool IsRunning { get; private set; }
        public StatusChangedEventArgs(bool isRunning) {
            IsRunning = isRunning;
        }
    }
}
