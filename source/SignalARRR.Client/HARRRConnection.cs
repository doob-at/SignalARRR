using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http.Connections.Client;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Converters;
using Reflectensions.ExtensionMethods;
using Reflectensions.JsonConverters;
using SignalARRR.Client;
using SignalARRR.Client.ExtensionMethods;
using SignalARRR.Constants;

namespace SignalARRR {
    public class HARRRConnection {
        private HubConnection HubConnection { get; }
        private HARRRConnectionOptions Options { get; }
        private ConcurrentDictionary<string, Delegate> ServerRequestHandlers { get; } = new ConcurrentDictionary<string, Delegate>();

        private Func<Task<string>> AccessTokenProvider { get; }

        private readonly Lazy<Reflectensions.Json> lazyJson = new Lazy<Reflectensions.Json>(() => new Reflectensions.Json()
            .RegisterJsonConverter<StringEnumConverter>()
            .RegisterJsonConverter<DefaultDictionaryConverter>()
        );

        public Reflectensions.Json Convert => lazyJson.Value;

        private HARRRConnection(HubConnection hubConnection, HARRRConnectionOptions options = null) {

            Options = options ?? new HARRRConnectionOptions();

            HubConnection = hubConnection;
            AccessTokenProvider = hubConnection.GetAccessTokenProvider() ?? (() => Task.FromResult<string>(null));

            this.On<ServerRequestMessage>(MethodNames.ChallengeAuthentication, requestMessage => {

                var msg = new ClientResponseMessage(requestMessage.Id);
                msg.PayLoad = AccessTokenProvider().GetAwaiter().GetResult();

                HubConnection.SendCoreAsync(MethodNames.ReplyServerRequest, new object[] { msg });
            });

            this.On<ServerRequestMessage>(MethodNames.InvokeServerRequest, (requestMessage) => {

                var msg = new ClientResponseMessage(requestMessage.Id);

                if (ServerRequestHandlers.TryGetValue(requestMessage.Method, out var handler)) {

                    var pars = handler.Method.GetParameters();
                    var arguments = new List<object>();
                    for (var i = 0; i < pars.Length; i++) {
                        var pInfo = pars[i];
                        var obj = requestMessage.Arguments[i];
                        if (obj is JsonElement je) {
                            obj = Convert.ToObject(je.ToString(), pInfo.ParameterType);
                        } 
                        arguments.Add(obj.To(pInfo.ParameterType));
                    }

                    var argsArray = arguments.ToArray();

                    var res = handler.DynamicInvoke(argsArray);
                    msg.PayLoad = res;

                } else {
                    msg.ErrorMessage = $"Method '{requestMessage.Method}' not Found";
                }

                if (Options.HttpResponse) {
                    var payload = Convert.ToJson(msg);
                    var url = HubConnection.GetResponseUri();
                    var httpClient = new HttpClient();
                    httpClient.PostAsync(url, new StringContent(payload));
                } else {
                    HubConnection.SendCoreAsync(MethodNames.ReplyServerRequest, new object[] { msg });
                }


            });
        }

        public HARRRConnection RegisterClientMethod(MethodInfo method, object target, string prefix = null) {

            var name = $"{prefix}{method.Name}";
            ServerRequestHandlers.TryAdd(name, DelegateHelper.CreateDelegate(method, target));
            return this;
        }

        public HARRRConnection RegisterClientMethods(object target, string prefix = null) {

            var methods = target.GetType().GetMethods();
            foreach (var methodInfo in methods) {
                
                RegisterClientMethod(methodInfo, target, prefix);
            }
            
            return this;
        }


      
        public IDisposable On(string methodName, Type[] parameterTypes, Func<object[], object, Task> handler, object state) {
            return HubConnection.On(methodName, parameterTypes, handler, state);
        }

        public void OnServerRequest(string methodName, Delegate handler) {

            ServerRequestHandlers.TryAdd(methodName, handler);
        }

        public void OnServerRequest(string methodName, Func<object, object> handler) {

            OnServerRequest<object>(methodName, handler);
        }
        public void OnServerRequest<TIn>(string methodName, Func<TIn, object> handler) {

            ServerRequestHandlers.TryAdd(methodName, handler);
        }

        public void OnServerRequest<TIn1,TIn2>(string methodName, Func<TIn1, TIn2, object> handler) {

            ServerRequestHandlers.TryAdd(methodName, handler);
        }

        public void OnServerRequest<TIn1, TIn2, TIn3>(string methodName, Func<TIn1, TIn2, TIn3, object> handler) {

            ServerRequestHandlers.TryAdd(methodName, handler);
        }

        public void OnServerRequest<TIn1, TIn2, TIn3, TIn4>(string methodName, Func<TIn1, TIn2, TIn3, TIn4, object> handler) {

            ServerRequestHandlers.TryAdd(methodName, handler);
        }


        public async Task<object> InvokeCoreAsync(string methodName, Type returnType, object[] args, CancellationToken cancellationToken = default) {
            var msg = new ClientRequestMessage(methodName, args).WithAuthorization(AccessTokenProvider);
            return HubConnection.InvokeCoreAsync(MethodNames.InvokeMessageResultOnServer, returnType, new object[] { msg }, cancellationToken);
        }

        public async Task InvokeCoreAsync(string methodName, object[] args, CancellationToken cancellationToken = default) {
            var msg = new ClientRequestMessage(methodName, args).WithAuthorization(AccessTokenProvider);
            await HubConnection.InvokeCoreAsync(MethodNames.InvokeMessageOnServer, new object[] { msg }, cancellationToken);
        }

        public async Task<TResult> InvokeCoreAsync<TResult>(string methodName, object[] args, CancellationToken cancellationToken = default) {
            var msg = new ClientRequestMessage(methodName, args).WithAuthorization(AccessTokenProvider);
            var resultMsg = await HubConnection.InvokeCoreAsync<TResult>(MethodNames.InvokeMessageResultOnServer, new object[] { msg }, cancellationToken);
            return resultMsg;
        }

        public Task SendCoreAsync(string methodName, object[] args, CancellationToken cancellationToken = default) {
            var msg = new ClientRequestMessage(methodName, args).WithAuthorization(AccessTokenProvider);
            return HubConnection.SendCoreAsync(MethodNames.SendMessageToServer, new object[] { msg }, cancellationToken);
        }

        public IAsyncEnumerable<TResult> StreamAsyncCore<TResult>(string methodName, object[] args, CancellationToken cancellationToken = default) {
            var msg = new ClientRequestMessage(methodName, args).WithAuthorization(AccessTokenProvider);
            return HubConnection.StreamAsyncCore<TResult>(MethodNames.StreamMessageFromServer, new object[] { msg }, cancellationToken);
        }

        public Task<ChannelReader<object>> StreamAsChannelCoreAsync(string methodName, Type returnType, object[] args, CancellationToken cancellationToken = default) {
            var msg = new ClientRequestMessage(methodName, args).WithAuthorization(AccessTokenProvider);
            return HubConnection.StreamAsChannelCoreAsync(MethodNames.StreamMessageFromServer, returnType, new object[] { msg }, cancellationToken);
        }

        public async Task<ChannelReader<TResult>> StreamAsChannelCoreAsync<TResult>(string methodName, object[] args, CancellationToken cancellationToken = default) {
            var msg = new ClientRequestMessage(methodName, args).WithAuthorization(AccessTokenProvider);
            return await HubConnection.StreamAsChannelCoreAsync<TResult>(MethodNames.StreamMessageFromServer, new object[] { msg }, cancellationToken);
        }


        

        public HubConnection AsSignalRHubConnection() {
            return HubConnection;
        }

        public static HARRRConnection Create(Action<HubConnectionBuilder> builder, Action<HARRRConnectionOptionsBuilder> optionsBuilder = null) {
            return Create(builder?.InvokeAction()?.Build(), optionsBuilder?.InvokeAction());
        }

        public static HARRRConnection Create(HubConnection hubConnection, Action<HARRRConnectionOptionsBuilder> optionsBuilder = null) {
            return Create(hubConnection, optionsBuilder.InvokeAction());
        }

        public static HARRRConnection Create(HubConnection hubConnection, HARRRConnectionOptions options = null) {
            return new HARRRConnection(hubConnection, options);
        }


        


        #region HubConnectionDecorator

        public event Func<Exception, Task> Closed {
            add => HubConnection.Closed += value;
            remove => HubConnection.Closed -= value;
        }

        public event Func<Exception, Task> Reconnecting {
            add => HubConnection.Reconnecting += value;
            remove => HubConnection.Reconnecting -= value;
        }

        public event Func<string, Task> Reconnected {
            add => HubConnection.Reconnected += value;
            remove => HubConnection.Reconnected -= value;
        }

        public TimeSpan ServerTimeout {
            get => HubConnection.ServerTimeout;
            set => HubConnection.ServerTimeout = value;
        }

        public TimeSpan KeepAliveInterval {
            get => HubConnection.KeepAliveInterval;
            set => HubConnection.KeepAliveInterval = value;
        }

        public TimeSpan HandshakeTimeout {
            get => HubConnection.HandshakeTimeout;
            set => HubConnection.HandshakeTimeout = value;
        }

        public string ConnectionId => HubConnection.ConnectionId;

        public HubConnectionState State => HubConnection.State;

        public Task StartAsync(CancellationToken cancellation = default) => HubConnection.StartAsync(cancellation);
        public Task StopAsync(CancellationToken cancellation = default) => HubConnection.StopAsync(cancellation);

        public Task DisposeAsync() {
            return HubConnection.DisposeAsync();
        }

        #endregion

    }
}
