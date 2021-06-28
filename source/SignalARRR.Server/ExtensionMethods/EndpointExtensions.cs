using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;

namespace doob.SignalARRR.Server.ExtensionMethods {
    public static class EndpointExtensions {

        public static bool IsSignalREndpoint(this Endpoint endpoint) {

            return endpoint?.Metadata.GetMetadata<HubMetadata>() != null;
        }

    }
}
