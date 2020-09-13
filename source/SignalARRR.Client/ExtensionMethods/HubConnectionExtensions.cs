using System;
using System.Collections.Generic;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Http.Connections.Client;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Reflectensions.ExtensionMethods;

namespace SignalARRR.Client.ExtensionMethods {
    public static class HubConnectionExtensions {

        public static Uri GetResponseUri(this HubConnection hubConnection) {

            var serviceProvider = (IServiceProvider)hubConnection.GetType().GetField("_serviceProvider", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(hubConnection);
            var endPoint = serviceProvider.GetRequiredService<EndPoint>();
            var endpointUri = endPoint.GetPropertyValue<Uri>("Uri");
            return new Uri($"{endpointUri}/response");

        }

        public static Func<Task<string>> GetAccessTokenProvider(this HubConnection hubConnection) {

            var connectionFactory = hubConnection.GetType().GetField("_connectionFactory", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(hubConnection);

            var httpConnectionOption = connectionFactory?.GetType().GetField("_httpConnectionOptions", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(connectionFactory) as HttpConnectionOptions;

            return httpConnectionOption?.AccessTokenProvider;
        }
    }
}
