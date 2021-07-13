using System;
using System.Collections.Generic;
using System.Text;
using doob.SignalARRR.Common;

namespace doob.SignalARRR.Client {
    public partial class HARRRConnection {

        public event EventHandler<ServerRequestEventArgs> OnServerRequestMessage;

    }
}
