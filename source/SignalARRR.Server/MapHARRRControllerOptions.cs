using Microsoft.AspNetCore.Http.Connections;

namespace doob.SignalARRR.Server {
    public class MapHARRRControllerOptions: HttpConnectionDispatcherOptions {

        public bool HttpResponse { get; set; }

        public bool HttpDownloadSource { get; set; }
    }
}
