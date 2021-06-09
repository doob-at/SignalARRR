using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using doob.Reflectensions.Helper;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using SignalARRR.CodeGenerator;
using SignalARRR.Server.ExtensionMethods;

namespace SignalARRR.Server {
    public class ServerClassCreatorProxyHelper : ClassCreatorHelper {
        private readonly ClientContext _clientContext;
        private readonly HttpContext _httpContext;
        private readonly ServerPushStreamManager _pushStreamManager;
        private readonly MethodArgumentPreperator _methodArgumentPreperator;

        public ServerClassCreatorProxyHelper(ClientContext clientContext, HttpContext httpContext) {
            _clientContext = clientContext;
            _httpContext = httpContext;
            _pushStreamManager = clientContext.ServiceProvider.GetRequiredService<ServerPushStreamManager>();
            _methodArgumentPreperator = new MethodArgumentPreperator(_clientContext);
        }

        public override T Invoke<T>(string methodName, IEnumerable<object> arguments, string[] genericArguments, CancellationToken cancellationToken = default) {
           return SimpleAsyncHelper.RunSync(() => InvokeAsync<T>(methodName, arguments, genericArguments, cancellationToken));
        }

        public override async Task<T> InvokeAsync<T>(string methodName, IEnumerable<object> arguments, string[] genericArguments, CancellationToken cancellationToken = default) {

            
            var preparedArguments = _methodArgumentPreperator.PrepareArguments(arguments).ToList();

            var msg = new ServerRequestMessage(methodName, preparedArguments);
            if (cancellationToken != CancellationToken.None) {
                msg.CancellationGuid = Guid.NewGuid();
                cancellationToken.Register(() => {
#pragma warning disable 4014
                    _clientContext.CancelToken(msg.CancellationGuid.Value);
#pragma warning restore 4014
                });
            }

            msg.GenericArguments = genericArguments;
            using var serviceProviderScope = _clientContext.ServiceProvider.CreateScope();

            var hubContextType = typeof(ClientContextDispatcher<>).MakeGenericType(_clientContext.HARRRType);
            var harrrContext = (IClientContextDispatcher)serviceProviderScope.ServiceProvider.GetRequiredService(hubContextType);
            await harrrContext.ProxyClientAsync(_clientContext.Id, msg, _httpContext);
            return default;
        }

        public override void Send(string methodName, IEnumerable<object> arguments, string[] genericArguments, CancellationToken cancellationToken = default) {
            SimpleAsyncHelper.RunSync(() => SendAsync(methodName, arguments, genericArguments, cancellationToken));
        }

        public override async Task SendAsync(string methodName, IEnumerable<object> arguments, string[] genericArguments, CancellationToken cancellationToken = default) {

            var preparedArguments = _methodArgumentPreperator.PrepareArguments(arguments).ToList();

            var msg = new ServerRequestMessage(methodName, preparedArguments);
            if (cancellationToken != CancellationToken.None) {
                msg.CancellationGuid = Guid.NewGuid();
                cancellationToken.Register(() => {
#pragma warning disable 4014
                    _clientContext.CancelToken(msg.CancellationGuid.Value);
#pragma warning restore 4014
                });
            }

            msg.GenericArguments = genericArguments;
            using var serviceProviderScope = _clientContext.ServiceProvider.CreateScope();

            var hubContextType = typeof(ClientContextDispatcher<>).MakeGenericType(_clientContext.HARRRType);
            var harrrContext = (IClientContextDispatcher)serviceProviderScope.ServiceProvider.GetRequiredService(hubContextType);
            await harrrContext.SendClientAsync(_clientContext.Id, msg, cancellationToken);
            await _httpContext.Ok();
        }

        public override IAsyncEnumerable<TResult> StreamAsync<TResult>(string methodName, IEnumerable<object> arguments, string[] genericArguments, CancellationToken cancellationToken = default) {
            throw new NotImplementedException(nameof(StreamAsync));
        }


        //private IEnumerable<object> PrepareArguments(IEnumerable<object> arguments, ClientContext clientContext) {
        //    foreach (var argument in arguments) {

        //        if (argument == null) {
        //            yield return null;
        //            continue;
        //        }


        //        if (argument is Stream stream) {

        //            var identifier = _pushStreamManager.StoreStreamForDownload(stream, clientContext.ConnectedTo);
        //            var streamReference = new StreamReference() { Uri = identifier };
        //            yield return streamReference;
        //            continue;
        //        }

        //        yield return argument;
        //    }
        //}
    }
}
