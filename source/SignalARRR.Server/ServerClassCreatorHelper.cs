using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Reflectensions.Helper;
using SignalARRR.CodeGenerator;
using SignalARRR.Server.ExtensionMethods;

namespace SignalARRR.Server {
    public class ServerClassCreatorHelper : ClassCreatorHelper {
        private readonly ClientContext _clientContext;


        public ServerClassCreatorHelper(ClientContext clientContext) {
            _clientContext = clientContext;
        }

        public override T Invoke<T>(string methodName, IEnumerable<object> arguments, string[] genericArguments, CancellationToken cancellationToken = default) {
           return AsyncHelper.RunSync(() => InvokeAsync<T>(methodName, arguments, genericArguments, cancellationToken));
        }

        public override Task<T> InvokeAsync<T>(string methodName, IEnumerable<object> arguments, string[] genericArguments, CancellationToken cancellationToken = default) {
            var msg = new ServerRequestMessage(methodName, arguments);
            msg.GenericArguments = genericArguments;
            using var serviceProviderScope = _clientContext.ServiceProvider.CreateScope();

            var hubContextType = typeof(ClientContextDispatcher<>).MakeGenericType(_clientContext.HARRRType);
            var harrrContext = (IClientContextDispatcher)serviceProviderScope.ServiceProvider.GetRequiredService(hubContextType);
            return harrrContext.InvokeClientAsync<T>(_clientContext.Id, msg, cancellationToken);
        }

        public override void Send(string methodName, IEnumerable<object> arguments, string[] genericArguments, CancellationToken cancellationToken = default) {
            AsyncHelper.RunSync(() => SendAsync(methodName, arguments, genericArguments, cancellationToken));
        }

        public override Task SendAsync(string methodName, IEnumerable<object> arguments, string[] genericArguments, CancellationToken cancellationToken = default) {
            var msg = new ServerRequestMessage(methodName, arguments);
            msg.GenericArguments = genericArguments;
            using var serviceProviderScope = _clientContext.ServiceProvider.CreateScope();

            var hubContextType = typeof(ClientContextDispatcher<>).MakeGenericType(_clientContext.HARRRType);
            var harrrContext = (IClientContextDispatcher)serviceProviderScope.ServiceProvider.GetRequiredService(hubContextType);
            return harrrContext.SendClientAsync(_clientContext.Id, msg, cancellationToken);
        }

        public override IAsyncEnumerable<TResult> StreamAsync<TResult>(string methodName, IEnumerable<object> arguments, string[] genericArguments, CancellationToken cancellationToken = default) {
            throw new NotImplementedException();
        }
    }
}
