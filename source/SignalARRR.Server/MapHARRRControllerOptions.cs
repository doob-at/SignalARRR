using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.AspNetCore.Http.Connections;

namespace SignalARRR.Server {
    public class MapHARRRControllerOptions: HttpConnectionDispatcherOptions {

        public bool HttpResponse { get; set; }

        public bool HttpDownloadSource { get; set; }
    }
}
