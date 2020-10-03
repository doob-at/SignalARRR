using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using Reflectensions;
using Reflectensions.ExtensionMethods;
using Reflectensions.Helper;
using SignalARRR.Attributes;
using SignalARRR.Client.ExtensionMethods;
using SignalARRR.Constants;

namespace SignalARRR.Client {
    public class MessageHandler {

        private HARRRConnectionOptions Options { get; }
        private HARRRConnection HARRRConnection { get; }
        private Func<Task<string>> AccessTokenProvider { get; }

        private ISignalARRRClientMethodsCollection MethodsCollection { get; } = new SignalARRRClientMethodsCollection();
        private bool UsesNewtonsoftJson { get; }
        private bool UsesMessagePack { get; }

        public MessageHandler(HARRRConnection harrrConnection, HARRRConnectionOptions options) {
            HARRRConnection = harrrConnection;
            Options = options;
            UsesNewtonsoftJson = HARRRConnection.AsSignalRHubConnection().UsesNewtonsoftJson();
            UsesMessagePack = HARRRConnection.AsSignalRHubConnection().UsesMessagePack();
            AccessTokenProvider = HARRRConnection.AsSignalRHubConnection().GetAccessTokenProvider() ?? (() => Task.FromResult<string>(null));
        }

        public async Task ChallengeAuthentication(ServerRequestMessage message) {

            var msg = new ClientResponseMessage(message.Id);
            msg.PayLoad = await AccessTokenProvider();

            await HARRRConnection.SendCoreAsync(MethodNames.ReplyServerRequest, new object[] { msg });

        }

        public async Task InvokeServerRequest(ServerRequestMessage message) {

            var msg = new ClientResponseMessage(message.Id);

            try {
                message = PrepareServerRequestMessage(message);

                msg.PayLoad = await InvokeMethodAsync(message);

                await SendResponse(msg);
            } catch (Exception e) {

                msg.ErrorMessage = e.GetBaseException().Message;
                await HARRRConnection.AsSignalRHubConnection().SendCoreAsync(MethodNames.ReplyServerRequest, new object[] { msg });
            }

        }

        public async Task InvokeServerMessage(ServerRequestMessage message) {

            message = PrepareServerRequestMessage(message);

            await InvokeMethodAsync(message);

        }


        public void RegisterMethods<TClass>(string prefix = null) where TClass : class {
            RegisterMethods(typeof(TClass), typeof(TClass), prefix);
        }
        public void RegisterMethods<TClass>(TClass instance, string prefix = null) where TClass : class {
            RegisterMethods(typeof(TClass), typeof(TClass), instance, prefix);
        }
        public void RegisterMethods<TClass>(Func<TClass> factory, string prefix = null) where TClass : class {
            RegisterMethods(typeof(TClass), typeof(TClass), factory, prefix);
        }

        public void RegisterMethods<TInterface, TClass>(string prefix = null) where TClass : class, TInterface {
            RegisterMethods(typeof(TInterface), typeof(TClass), prefix);
        }
        public void RegisterMethods<TInterface, TClass>(TClass instance, string prefix = null) where TClass : class, TInterface {
            RegisterMethods(typeof(TInterface), typeof(TClass), instance, prefix);
        }
        public void RegisterMethods<TInterface, TClass>(Func<TClass> factory, string prefix = null) where TClass : class, TInterface {
            RegisterMethods(typeof(TInterface), typeof(TClass), factory, prefix);
        }

        public void RegisterMethods(Type interfaceType, Type instanceType, string prefix = null) {
            Func<object> factory = () => {
                var fromServiceProvider = HARRRConnection.AsSignalRHubConnection().GetServiceProvider().GetService(instanceType);
                if (fromServiceProvider != null) {
                    return fromServiceProvider;
                }

                return Activator.CreateInstance(instanceType);
            };
            RegisterMethods(interfaceType, instanceType, factory, prefix);
        }
        public void RegisterMethods(Type interfaceType, Type instanceType, object instance, string prefix = null) {
            RegisterMethods(interfaceType, instanceType, () => instance, prefix);
        }
        public void RegisterMethods(Type interfaceType, Type instanceType, Func<object> factory, string prefix = null) {

            var rootName = instanceType.GetCustomAttribute<MessageNameAttribute>()?.Name ?? prefix.ToNull() ?? instanceType.Name;
            var methodsWithName = interfaceType.GetMethods().Select(m => (MethodInfo: m, Attribute: m.GetCustomAttribute<MessageNameAttribute>()));
            foreach (var (methodInfo, methodNameAttribute) in methodsWithName) {
                var methodName = methodNameAttribute?.Name ?? methodInfo.Name;
                var concatNames = $"{rootName}.{methodName}";
                MethodsCollection.AddMethod(concatNames, methodInfo, factory);
            }
        }

        


        private async Task SendResponse(ClientResponseMessage responseMessage) {

            if (Options.HttpResponse) {
                var payload = Json.Converter.ToJson(responseMessage);
                var url = HARRRConnection.AsSignalRHubConnection().GetResponseUri();
                var httpClient = new HttpClient();
                await httpClient.PostAsync(url, new StringContent(payload));
            } else {
                await HARRRConnection.AsSignalRHubConnection().SendCoreAsync(MethodNames.ReplyServerRequest, new object[] { responseMessage });
            }
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


        private ServerRequestMessage PrepareServerRequestMessage(ServerRequestMessage message) {
            if (!UsesNewtonsoftJson && !UsesMessagePack) {
                var requestJson = JsonSerializer.Serialize(message);
                message = Json.Converter.ToObject<ServerRequestMessage>(requestJson);
            }

            return message;
        }
    }
}
