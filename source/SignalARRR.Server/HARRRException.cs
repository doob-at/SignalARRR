using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.AspNetCore.SignalR;

namespace SignalARRR.Server {
    public class HARRRException: HubException {

        public HARRRException(Exception exception): this(exception.GetBaseException().GetType().FullName, exception.GetBaseException().Message) {

        }

        public HARRRException(string type, string message): base($"[{type}] {message}") {

        }
    }
}
