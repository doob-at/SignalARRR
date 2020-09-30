using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http.Connections.Client;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.SignalR.Protocol;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using Reflectensions;
using Reflectensions.ExtensionMethods;
using Reflectensions.Helper;
using Reflectensions.JsonConverters;
using SignalARRR.Attributes;
using SignalARRR.Client;
using SignalARRR.Client.ExtensionMethods;
using SignalARRR.Constants;

namespace SignalARRR {
    public class HARRRConnection {
        private HubConnection HubConnection { get; }
        private HARRRConnectionOptions Options { get; }
        private ConcurrentDictionary<string, Delegate> ServerRequestHandlers { get; } = new ConcurrentDictionary<string, Delegate>();

        private ISignalARRRClientMethodsCollection MethodsCollection { get; } = new SignalARRRClientMethodsCollection();
        private Func<Task<string>> AccessTokenProvider { get; }

        private readonly Lazy<Reflectensions.Json> lazyJson = new Lazy<Reflectensions.Json>(() => new Reflectensions.Json()
            .RegisterJsonConverter<StringEnumConverter>()
            .RegisterJsonConverter<DefaultDictionaryConverter>()
        );

        private bool UsesNewtonsoftJson { get; }
        private bool UsesMessagePack { get; }

        private HARRRConnection(HubConnection hubConnection, HARRRConnectionOptions options = null) {

            Options = options ?? new HARRRConnectionOptions();

            HubConnection = hubConnection;

            UsesNewtonsoftJson = HubConnection.UsesNewtonsoftJson();
            UsesMessagePack = HubConnection.UsesMessagePack();

            AccessTokenProvider = hubConnection.GetAccessTokenProvider() ?? (() => Task.FromResult<string>(null));

            this.On<ServerRequestMessage>(MethodNames.ChallengeAuthentication, requestMessage => {

                var msg = new ClientResponseMessage(requestMessage.Id);
                msg.PayLoad = AccessTokenProvider().GetAwaiter().GetResult();

                HubConnection.SendCoreAsync(MethodNames.ReplyServerRequest, new object[] { msg });
            });

            this.On<ServerRequestMessage>(MethodNames.InvokeServerRequest, async (requestMessage) => {

                var msg = new ClientResponseMessage(requestMessage.Id);

                try {
                    if (!UsesNewtonsoftJson && !UsesMessagePack) {
                        var requestJson = JsonSerializer.Serialize(requestMessage);
                        requestMessage = Json.Converter.ToObject<ServerRequestMessage>(requestJson);
                    }

                    msg.PayLoad = await InvokeMethodAsync(requestMessage);

 
                    if (Options.HttpResponse) {
                        var payload = Json.Converter.ToJson(msg);
                        var url = HubConnection.GetResponseUri();
                        var httpClient = new HttpClient();
                        await httpClient.PostAsync(url, new StringContent(payload));
                    } else {
                        await HubConnection.SendCoreAsync(MethodNames.ReplyServerRequest, new object[] { msg });
                    }
                } catch (Exception e) {

                    msg.ErrorMessage = e.GetBaseException().Message;
                    await HubConnection.SendCoreAsync(MethodNames.ReplyServerRequest, new object[] { msg });
                }
                


            });
        }


        public HARRRConnection PreBuiltTypedMethods<T>() {
            ClassCreator.CreateTypeFromInterface<T>();
            return this;
        }

        public T GetTypedMethods<T>(string nameSpace = null) {
            var instance = ClassCreator.CreateInstanceFromInterface<T>(this, nameSpace);
            return instance;
        }

        private async Task<object> InvokeMethodAsync(ServerRequestMessage serverRequestMessage) {



            var methodCallInfo = MethodsCollection.GetMethod(serverRequestMessage.Method);
            var methodInfo = methodCallInfo.MethodInfo;
            if (methodCallInfo == null || methodInfo == null) {
                var errorMsg = $"Method '{serverRequestMessage.Method}' not found";
               
                throw new Exception(errorMsg);
            }
            
           
            var parameters = BuildExecuteMethodParameters(methodCallInfo.MethodInfo, serverRequestMessage.Arguments);

            var instance = methodCallInfo.Factory != null ? methodCallInfo.Factory.DynamicInvoke() : Activator.CreateInstance(methodInfo.DeclaringType);

            if (serverRequestMessage.GenericArguments?.Any() == true) {
                
                var arrType = serverRequestMessage.GenericArguments.Select(TypeHelper.FindType).ToList();
                methodInfo = methodInfo.MakeGenericMethod(arrType.ToArray());
                return await InvokeHelper.InvokeMethodAsync<object>(instance, methodInfo, parameters);
            }

            if (methodInfo.ReturnType == typeof(void) || methodInfo.ReturnType == typeof(Task)) {
                await InvokeHelper.InvokeVoidMethodAsync(instance, methodInfo, parameters);
                return null;
            } else {
                return await InvokeHelper.InvokeMethodAsync<object>(instance, methodInfo, parameters);
            }


        }

        private object[] BuildExecuteMethodParameters(MethodInfo methodInfo, IEnumerable<object> parameters, CancellationToken cancellation = default) {

            int paramsPosition = 0;
            var @params = parameters.ToList();
            return methodInfo.GetParameters().Select(p => {

                if (p.ParameterType == typeof(CancellationToken)) {
                    return cancellation;
                }
                
                if (@params.Count < paramsPosition) {
                    throw new IndexOutOfRangeException();
                }

                var par = @params[paramsPosition];

                if (par != null && p.ParameterType != par.GetType()) {

                    if (par is JToken jt) {
                        par = jt.ToObject(p.ParameterType);
                    } else {
                        par = par.To(p.ParameterType);
                    }

                }

                paramsPosition++;
                return par;

            }).ToArray();

        }




        //public HARRRConnection RegisterClientMethod(MethodInfo method, object target, string prefix = null) {

        //    var messageNamePrefix = prefix.ToNull() ?? method.DeclaringType?.GetCustomAttribute<MessageNameAttribute>()?.Name ?? method.DeclaringType ?.Name;


        //    var name = $"{messageNamePrefix?.Trim('.')}.{method.Name}".Trim('.');
        //    ServerRequestHandlers.TryAdd(name, DelegateHelper.CreateDelegate(method, target));
        //    return this;
        //}

        //private HARRRConnection RegisterClientMethods(object instance, string prefix = null) {

        //    var type = instance.GetType();
        //    var rootName = type.GetCustomAttribute<MessageNameAttribute>()?.Name ?? prefix.ToNull() ?? type.Name;
        //    var methodsWithName = type.GetMethods().Select(m => (MethodInfo: m, Attribute: m.GetCustomAttribute<MessageNameAttribute>()));
        //    foreach (var (methodInfo, methodNameAttribute) in methodsWithName) {
        //        var methodName = methodNameAttribute?.Name ?? methodInfo.Name;
        //        var concatNames = $"{rootName}.{methodName}";
        //        MethodsCollection.AddMethod(concatNames, methodInfo, instance);
        //    }

        //    return this;
        //}

        private HARRRConnection RegisterClientMethods(Type type, string prefix = null) {

            var rootName = type.GetCustomAttribute<MessageNameAttribute>()?.Name ?? prefix.ToNull() ?? type.Name;
            var methodsWithName = type.GetMethods().Select(m => (MethodInfo: m, Attribute: m.GetCustomAttribute<MessageNameAttribute>()));
            foreach (var (methodInfo, methodNameAttribute) in methodsWithName) {
                var methodName = methodNameAttribute?.Name ?? methodInfo.Name;
                var concatNames = $"{rootName}.{methodName}";
                MethodsCollection.AddMethod(concatNames, methodInfo);
            }

            return this;
        }

        public HARRRConnection RegisterClientMethods<TInterface>(string prefix = null) {
            return RegisterClientMethods(typeof(TInterface));
        }

        public HARRRConnection RegisterClientMethods<TInterface>(Func<TInterface> factory, string prefix = null) {
            var type = typeof(TInterface);
            var rootName = type.GetCustomAttribute<MessageNameAttribute>()?.Name ?? prefix.ToNull() ?? type.Name;
            var methodsWithName = type.GetMethods().Select(m => (MethodInfo: m, Attribute: m.GetCustomAttribute<MessageNameAttribute>()));
            foreach (var (methodInfo, methodNameAttribute) in methodsWithName) {
                var methodName = methodNameAttribute?.Name ?? methodInfo.Name;
                var concatNames = $"{rootName}.{methodName}";
                MethodsCollection.AddMethod(concatNames, methodInfo, factory);
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
            return await HubConnection.InvokeCoreAsync(MethodNames.InvokeMessageResultOnServer, returnType, new object[] { msg }, cancellationToken);
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

            var intermediateBuilder = builder?.InvokeAction();
            
            var hasJsonProtocol = intermediateBuilder.Services.Any(s => s.ServiceType == typeof(IHubProtocol) && s.ImplementationType == typeof(NewtonsoftJsonHubProtocol));
            var hasMessagePackHubProtocol = intermediateBuilder.Services.Any(s => s.ServiceType == typeof(IHubProtocol) && s.ImplementationType.Name == "MessagePackHubProtocol");


            if (!hasJsonProtocol && !hasMessagePackHubProtocol) {
                intermediateBuilder = intermediateBuilder.AddNewtonsoftJsonProtocol();
            }
            
            return Create(intermediateBuilder?.Build(), optionsBuilder?.InvokeAction());
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
