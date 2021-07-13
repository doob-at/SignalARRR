using System;
using System.Collections.Generic;
using System.Text;
using doob.SignalARRR.Common;

namespace doob.SignalARRR.Client {
    public class ServerRequestEventArgs: EventArgs {

        public ServerRequestMessage ServerRequestMessage { get; }

        public ServerRequestEventArgs(ServerRequestMessage serverRequestMessage) {
            ServerRequestMessage = serverRequestMessage;
        }
    }
}
