using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Http.Features.Authentication;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Newtonsoft.Json.Linq;
using Reflectensions;
using SignalARRR.Server.ExtensionMethods;

namespace SignalARRR.Server {
    public abstract class HARRR : Hub {

        private IHARRRClientManager ClientManager { get; }
        private ISignalARRRServerMethodsCollection MethodsCollection { get; }
        private ServerRequestManager ServerRequestManager { get; }

        protected IServiceProvider ServiceProvider { get; }
        public ILogger Logger { get; set; }


        private ClientContext _clientContext;
        public ClientContext ClientContext {
            get => _clientContext ?? ClientManager.GetClient(Context.ConnectionId);
            set => _clientContext = value;
        }

        protected HARRR(IServiceProvider serviceProvider) {
            ServiceProvider = serviceProvider;

            ClientManager = serviceProvider.GetRequiredService<IHARRRClientManager>();
            Logger = serviceProvider.GetService<ILoggerFactory>()?.CreateLogger(this.GetType().FullName) ?? NullLogger.Instance;

            MethodsCollection =
                (ISignalARRRServerMethodsCollection)serviceProvider.GetRequiredService(
                    typeof(SignalARRRServerMethodsCollection<>).MakeGenericType(this.GetType()));

            ServerRequestManager = serviceProvider.GetService<ServerRequestManager>();
        }

        public override async Task OnConnectedAsync() {
            try {


                var cl = ClientManager.Register(this, Context);

                await base.OnConnectedAsync().ConfigureAwait(false);
                if (Logger.IsEnabled(LogLevel.Debug))
                    Logger.LogDebug("HARRR '{Name}' connected - {Ip}({ConnectionId})", this.GetType().Name, cl.RemoteIp, Context.ConnectionId);

            } catch (Exception ex) {
                Logger.LogError(ex, "Error in HARRR '{HubName}' on Method \"OnConnect()\" from  - {Ip}({ConnectionId}", this.GetType().Name);
            }
        }

        public override async Task OnDisconnectedAsync(Exception exception) {

            try {

                var cl = ClientManager.UnRegister(Context.ConnectionId);
                await base.OnDisconnectedAsync(exception).ConfigureAwait(false);

                if (Logger.IsEnabled(LogLevel.Debug))
                    Logger.LogDebug("HARRR {Name} disconnected - {Ip}({ConnectionId})", this.GetType().Name, cl.RemoteIp, Context.ConnectionId);

            } catch (Exception ex) {
                Logger.LogError(ex, "Error in HARRR '{HubName}' on Method \"OnDisconnected(bool stopCalled)\" from - {Ip}({ConnectionId}",
                    this.GetType().Name);
            }

        }



        public async Task InvokeMessage(ClientRequestMessage clientMessage) {
            try {
                if (Logger.IsEnabled(LogLevel.Debug))
                    Logger.LogDebug("InvokeMessage {Method}", clientMessage.Method);

                var strHelper = new MessageHandler(this, ClientContext, MethodsCollection, ServiceProvider);


                await strHelper.InvokeMethodAsync(clientMessage).ConfigureAwait(false);
            } catch (Exception e) {
                throw new HARRRException(e);
            }

        }

        public async Task<object> InvokeMessageResult(ClientRequestMessage clientMessage) {
            try {
                if (Logger.IsEnabled(LogLevel.Debug))
                    Logger.LogDebug("InvokeMessageResult {Method}", clientMessage.Method);

                var strHelper = new MessageHandler(this, ClientContext, MethodsCollection, ServiceProvider);




                return await strHelper.InvokeMethodAsync(clientMessage);
            } catch (Exception e) {

                throw new HARRRException(e);

            }

        }


        public Task SendMessage(ClientRequestMessage clientMessage) {

            try {
                if (Logger.IsEnabled(LogLevel.Debug))
                    Logger.LogDebug("SendMessage {Method}", clientMessage.Method);

                var strHelper = new MessageHandler(this, ClientContext, MethodsCollection, ServiceProvider);
                _ = strHelper.InvokeMethodAsync(clientMessage).ConfigureAwait(false);
                return Task.CompletedTask;
            } catch (Exception e) {
                throw new HARRRException(e);
            }

        }

        public async Task<IAsyncEnumerable<object>> StreamMessage(ClientRequestMessage clientMessage, CancellationToken cancellationToken) {

            try {
                if (Logger.IsEnabled(LogLevel.Debug))
                    Logger.LogDebug("StreamMessage {Method}", clientMessage.Method);

                var strHelper = new MessageHandler(this, ClientContext, MethodsCollection, ServiceProvider);
                return await strHelper.InvokeStreamAsync(clientMessage, cancellationToken).ConfigureAwait(false);
            } catch (Exception e) {
                throw new HARRRException(e);
            }
        }

        public async Task ReplyServerRequest(Guid id, object payload, string error) {

            var responseType = ServerRequestManager.GetResponseType(id);
            var jtoken = Json.Converter.ToJToken(payload);
            switch (responseType) {
                case RequestType.Default: {
                        
                        ServerRequestManager.CompleteRequest(id, jtoken, error);
                        return;
                    }
                case RequestType.Proxy: {
                        var httpContext = ServerRequestManager.GetHttpContext(id);

                        if (error != null) {
                            await httpContext.BadRequest(error);
                        } else {
                            await httpContext.Ok(jtoken);
                        }
                        ServerRequestManager.CompleteProxyRequest(id);
                        return;
                    }
            }


        }


    }


}
