using System;
using System.Collections.Generic;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Http.Connections.Client;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.SignalR.Protocol;
using Microsoft.Extensions.DependencyInjection;
using Reflectensions.ExtensionMethods;

namespace SignalARRR.Client.ExtensionMethods {
    public static class HubConnectionExtensions {

        public static IServiceProvider GetServiceProvider(this HubConnection hubConnection) {

            var serviceProvider = (IServiceProvider)hubConnection.GetType().GetField("_serviceProvider", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(hubConnection);
            return serviceProvider;

        }

        public static bool UsesNewtonsoftJson(this HubConnection hubConnection) {

            var sp = hubConnection.GetServiceProvider();
            return sp.GetService<IHubProtocol>() is NewtonsoftJsonHubProtocol;

        }

        public static bool UsesMessagePack(this HubConnection hubConnection) {

            var sp = hubConnection.GetServiceProvider();
            return  sp.GetService<IHubProtocol>().GetType().Name == "MessagePackHubProtocol";

        }

        public static Uri GetResponseUri(this HubConnection hubConnection) {

            var serviceProvider = hubConnection.GetServiceProvider();
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
