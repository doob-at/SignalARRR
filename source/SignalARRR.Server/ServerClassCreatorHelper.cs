using System;
using System.Collections.Generic;
using System.IO;
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
        private readonly ServerPushStreamManager _pushStreamManager;

        public ServerClassCreatorHelper(ClientContext clientContext) {
            _clientContext = clientContext;
            _pushStreamManager = clientContext.ServiceProvider.GetRequiredService<ServerPushStreamManager>();
        }

        public override T Invoke<T>(string methodName, IEnumerable<object> arguments, string[] genericArguments, CancellationToken cancellationToken = default) {
           return AsyncHelper.RunSync(() => InvokeAsync<T>(methodName, arguments, genericArguments, cancellationToken));
        }

        public override Task<T> InvokeAsync<T>(string methodName, IEnumerable<object> arguments, string[] genericArguments, CancellationToken cancellationToken = default) {

            var preparedArguments = PrepareArguments(arguments, _clientContext).ToList();

            var msg = new ServerRequestMessage(methodName, preparedArguments);
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

        private IEnumerable<object> PrepareArguments(IEnumerable<object> arguments, ClientContext clientContext) {
            foreach (var argument in arguments) {

                if (argument == null) {
                    yield return null;
                    continue;
                }
                    
                
                if (argument is Stream stream) {

                    var identifier = _pushStreamManager.StoreStreamForDownload(stream, clientContext.ConnectedTo);
                    var streamReference = new StreamReference() {Uri = identifier};
                    yield return streamReference;
                    continue;
                }

                yield return argument;
            }
        }
    }
}
