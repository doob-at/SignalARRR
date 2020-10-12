﻿using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace SignalARRR.Server {
    internal interface IClientContextDispatcher {

        Task ProxyClientAsync(string clientId, ServerRequestMessage serverRequestMessage, HttpContext httpContext);

        Task<TResult> InvokeClientAsync<TResult>(string clientId, ServerRequestMessage serverRequestMessage,
            CancellationToken cancellationToken);

        Task SendClientAsync(string clientId, ServerRequestMessage serverRequestMessage, CancellationToken cancellationToken);

        Task<string> Challenge(string clientId);

        Task CancelToken(string clientId, Guid id);
    }
}
