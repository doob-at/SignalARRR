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
using Microsoft.CodeAnalysis.CSharp.Syntax;
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
using SignalARRR.CodeGenerator;
using SignalARRR.Constants;

namespace SignalARRR {
    public class HARRRConnection {
        private HubConnection HubConnection { get; }
        private HARRRConnectionOptions Options { get; }
        private ConcurrentDictionary<string, Delegate> ServerRequestHandlers { get; } = new ConcurrentDictionary<string, Delegate>();

        private Func<Task<string>> AccessTokenProvider { get; }


        private MessageHandler MessageHandler { get; }
        private HARRRConnection(HubConnection hubConnection, HARRRConnectionOptions options = null) {

            Options = options ?? new HARRRConnectionOptions();
            HubConnection = hubConnection;
            MessageHandler = new MessageHandler(this, Options);
            AccessTokenProvider = hubConnection.GetAccessTokenProvider() ?? (() => Task.FromResult<string>(null));

            this.On<ServerRequestMessage>(MethodNames.ChallengeAuthentication, 
                async (requestMessage) => await MessageHandler.ChallengeAuthentication(requestMessage)
            );

            this.On<ServerRequestMessage>(MethodNames.InvokeServerRequest,
                async (requestMessage) => await MessageHandler.InvokeServerRequest(requestMessage)
            );

            this.On<ServerRequestMessage>(MethodNames.InvokeServerMessage,
                async (requestMessage) => await MessageHandler.InvokeServerMessage(requestMessage)
            );
        }


        public HARRRConnection PreBuiltTypedMethods<T>() {
            ClassCreator.CreateTypeFromInterface<T>();
            return this;
        }

        public T GetTypedMethods<T>(string nameSpace = null) {
            var instance = ClassCreator.CreateInstanceFromInterface<T>(new ClientClassCreatorHelper(this), nameSpace);
            return instance;
        }


        public void RegisterClientMethods<TClass>(string prefix = null) where TClass : class {
            MessageHandler.RegisterMethods<TClass>(prefix);
        }
        public void RegisterClientMethods<TClass>(TClass instance, string prefix = null) where TClass : class {
            MessageHandler.RegisterMethods<TClass>(instance, prefix);
        }
        public void RegisterClientMethods<TClass>(Func<TClass> factory, string prefix = null) where TClass : class {
            MessageHandler.RegisterMethods<TClass>(factory, prefix);
        }

        public void RegisterClientMethods<TInterface, TClass>(string prefix = null) where TClass : class, TInterface {
            MessageHandler.RegisterMethods<TInterface, TClass>(prefix);
        }
        public void RegisterClientMethods<TInterface, TClass>(TClass instance, string prefix = null) where TClass : class, TInterface {
            MessageHandler.RegisterMethods<TInterface, TClass>(instance, prefix);
        }
        public void RegisterClientMethods<TInterface, TClass>(Func<TClass> factory, string prefix = null) where TClass : class, TInterface {
            MessageHandler.RegisterMethods<TInterface, TClass>(factory, prefix);
        }

        public void RegisterClientMethods(Type interfaceType, Type instanceType, string prefix = null) {
            MessageHandler.RegisterMethods(interfaceType, instanceType, prefix);
        }
        public void RegisterClientMethods(Type interfaceType, Type instanceType, object instance, string prefix = null) {
            MessageHandler.RegisterMethods(interfaceType, instanceType, instance, prefix);
        }
        public void RegisterClientMethods(Type interfaceType, Type instanceType, Func<object> factory, string prefix = null) {
            MessageHandler.RegisterMethods(interfaceType, instanceType, factory, prefix);
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

        public void OnServerRequest<TIn1, TIn2>(string methodName, Func<TIn1, TIn2, object> handler) {

            ServerRequestHandlers.TryAdd(methodName, handler);
        }

        public void OnServerRequest<TIn1, TIn2, TIn3>(string methodName, Func<TIn1, TIn2, TIn3, object> handler) {

            ServerRequestHandlers.TryAdd(methodName, handler);
        }

        public void OnServerRequest<TIn1, TIn2, TIn3, TIn4>(string methodName, Func<TIn1, TIn2, TIn3, TIn4, object> handler) {

            ServerRequestHandlers.TryAdd(methodName, handler);
        }

        public async Task<object> InvokeCoreAsync(ClientRequestMessage message, Type returnType, CancellationToken cancellationToken = default) {
            message = message.WithAuthorization(AccessTokenProvider);
            return await HubConnection.InvokeCoreAsync(MethodNames.InvokeMessageResultOnServer, returnType, new object[] { message }, cancellationToken);
        }

        public async Task InvokeCoreAsync(ClientRequestMessage message, CancellationToken cancellationToken = default) {
            message = message.WithAuthorization(AccessTokenProvider);
            await HubConnection.InvokeCoreAsync(MethodNames.InvokeMessageOnServer, new object[] { message }, cancellationToken);
        }

        public async Task<object> InvokeCoreAsync(string methodName, Type returnType, object[] args, CancellationToken cancellationToken = default) {
            var msg = new ClientRequestMessage(methodName, args).WithAuthorization(AccessTokenProvider);
            return await HubConnection.InvokeCoreAsync(MethodNames.InvokeMessageResultOnServer, returnType, new object[] { msg }, cancellationToken);
        }

        public async Task InvokeCoreAsync(string methodName, object[] args, CancellationToken cancellationToken = default) {
            var msg = new ClientRequestMessage(methodName, args).WithAuthorization(AccessTokenProvider);
            await HubConnection.InvokeCoreAsync(MethodNames.InvokeMessageOnServer, new object[] { msg }, cancellationToken);
        }

        public async Task<TResult> InvokeCoreAsync<TResult>(ClientRequestMessage message, CancellationToken cancellationToken = default) {
            message = message.WithAuthorization(AccessTokenProvider);
            var resultMsg = await HubConnection.InvokeCoreAsync<TResult>(MethodNames.InvokeMessageResultOnServer, new object[] { message }, cancellationToken);
            return resultMsg;
        }
        public async Task<TResult> InvokeCoreAsync<TResult>(string methodName, object[] args, CancellationToken cancellationToken = default) {
            var msg = new ClientRequestMessage(methodName, args).WithAuthorization(AccessTokenProvider);
            var resultMsg = await HubConnection.InvokeCoreAsync<TResult>(MethodNames.InvokeMessageResultOnServer, new object[] { msg }, cancellationToken);
            return resultMsg;
        }

        public Task SendCoreAsync(ClientRequestMessage message, CancellationToken cancellationToken = default) {
            message = message.WithAuthorization(AccessTokenProvider);
            return HubConnection.SendCoreAsync(MethodNames.SendMessageToServer, new object[] { message }, cancellationToken);
        }

        public Task SendCoreAsync(string methodName, object[] args, CancellationToken cancellationToken = default) {
            var msg = new ClientRequestMessage(methodName, args).WithAuthorization(AccessTokenProvider);
            return HubConnection.SendCoreAsync(MethodNames.SendMessageToServer, new object[] { msg }, cancellationToken);
        }

        public IAsyncEnumerable<TResult> StreamAsyncCore<TResult>(ClientRequestMessage message, CancellationToken cancellationToken = default) {
            message = message.WithAuthorization(AccessTokenProvider);
            return HubConnection.StreamAsyncCore<TResult>(MethodNames.StreamMessageFromServer, new object[] { message }, cancellationToken);
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
