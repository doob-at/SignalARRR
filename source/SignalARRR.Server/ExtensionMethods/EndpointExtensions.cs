using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;

namespace SignalARRR.Server.ExtensionMethods {
    public static class EndpointExtensions {

        public static bool IsSignalREndpoint(this Endpoint endpoint) {

            return endpoint?.Metadata.GetMetadata<HubMetadata>() != null;
        }

    }
}
