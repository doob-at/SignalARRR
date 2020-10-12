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
        private readonly MethodArgumentPreperator _methodArgumentPreperator;

        public ServerClassCreatorHelper(ClientContext clientContext) {
            _clientContext = clientContext;
            _pushStreamManager = clientContext.ServiceProvider.GetRequiredService<ServerPushStreamManager>();
            _methodArgumentPreperator = new MethodArgumentPreperator(_clientContext);
        }

        public override T Invoke<T>(string methodName, IEnumerable<object> arguments, string[] genericArguments, CancellationToken cancellationToken = default) {
           return AsyncHelper.RunSync(() => InvokeAsync<T>(methodName, arguments, genericArguments, cancellationToken));
        }

        public override Task<T> InvokeAsync<T>(string methodName, IEnumerable<object> arguments, string[] genericArguments, CancellationToken cancellationToken = default) {

            
            var preparedArguments = _methodArgumentPreperator.PrepareArguments(arguments).ToList();

            var msg = new ServerRequestMessage(methodName, preparedArguments);
            if (cancellationToken != CancellationToken.None) {
                msg.CancellationGuid = Guid.NewGuid();
                cancellationToken.Register(() => {
                    _clientContext.CancelToken(msg.CancellationGuid.Value);
                });
            }

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
            var preparedArguments = _methodArgumentPreperator.PrepareArguments(arguments).ToList();

            var msg = new ServerRequestMessage(methodName, preparedArguments);
            if (cancellationToken != CancellationToken.None) {
                msg.CancellationGuid = Guid.NewGuid();
                cancellationToken.Register(() => _clientContext.CancelToken(msg.CancellationGuid.Value));
            }
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
