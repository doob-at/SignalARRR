﻿using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.SignalR.Protocol;
using Microsoft.Extensions.DependencyInjection;
using Reflectensions.ExtensionMethods;
using Reflectensions.HelperClasses;
using SignalARRR.Client.ExtensionMethods;

namespace SignalARRR.Client {
    public class HARRRContext {
        private readonly IServiceProvider _serviceProvider;
        
        public Uri BaseUrl { get;}

        public bool UseHttpResponse { get; }
        
        public HubProtocolType HubProtocolType { get; }
        internal Func<Task<string>> AccessTokenProvider { get; }

        public MessageHandler MessageHandler { get; }

        public HARRRContext(IServiceProvider serviceProvider, HARRRConnectionOptions options) {
            _serviceProvider = serviceProvider;

            BaseUrl = GetBaseUrl();
            HubProtocolType = Enum<HubProtocolType>.Find(_serviceProvider.GetRequiredService<IHubProtocol>().GetType().Name);
            UseHttpResponse = options.HttpResponse;
            AccessTokenProvider = GetHubConnection().GetAccessTokenProvider() ?? (() => Task.FromResult<string>(null));
            MessageHandler = new MessageHandler(this);
        }

        private Uri GetBaseUrl() {
            var endPoint = _serviceProvider.GetRequiredService<EndPoint>();
            return endPoint.GetPropertyValue<Uri>("Uri");
        }

        public Uri GetResponseUri(Guid id, string error = null) {
            var uriBuilder = new UriBuilder(new Uri($"{GetBaseUrl()}/response/{id}"));
            if (!string.IsNullOrEmpty(error)) {
                uriBuilder.Query = WebUtility.UrlEncode($"error={error}")!;
            }
            return uriBuilder.Uri;
        }

        public Uri GetDownloadUri(string identifier) {
            return new Uri($"{GetBaseUrl()}/download/{identifier}");
        }

        public HubConnection GetHubConnection() {
            return _serviceProvider.GetRequiredService<HubConnection>();
        }

    }
}
