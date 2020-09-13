using System;
using System.Threading;
using System.Threading.Tasks;

namespace SignalARRR.Server {
    internal interface IClientContextDispatcher {

        Task<TResult> InvokeClientAsync<TResult>(string clientId, ServerRequestMessage serverRequestMessage,
            CancellationToken cancellationToken);

        Task<string> Challenge(string clientId);
    }
}
