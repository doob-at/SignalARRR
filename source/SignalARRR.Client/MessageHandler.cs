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
using SignalARRR.Helper;

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

            string payload = null;
            string error = null;
            try {
                payload = await AccessTokenProvider();
            } catch (Exception e) {
                error = e.GetBaseException().Message;
            }
            

            await HARRRConnection.SendCoreAsync(MethodNames.ReplyServerRequest, new object[] { message.Id, payload, error });

        }

        public async Task InvokeServerRequest(ServerRequestMessage message) {
            
            try {
                message = PrepareServerRequestMessage(message);
                var payload = await InvokeMethodAsync(message);
                await SendResponse(message.Id, payload, null);
            } catch (Exception e) {
                await HARRRConnection.AsSignalRHubConnection().SendCoreAsync(MethodNames.ReplyServerRequest, new object[] { message.Id, null, e.GetBaseException().Message });
            }

        }

        public async Task InvokeServerMessage(ServerRequestMessage message) {

            try {
                message = PrepareServerRequestMessage(message);
                await InvokeMethodAsync(message);
            } catch {
                // ignored
            }
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

        


        private async Task SendResponse(Guid id, object payload, string error) {

            if (Options.HttpResponse) {
                var url = HARRRConnection.AsSignalRHubConnection().GetResponseUri(id, error);
                var httpClient = new HttpClient();

                if (!string.IsNullOrEmpty(error)) {
                    await httpClient.PostAsync(url, null);
                } else {
                    var jsonPayload = Json.Converter.ToJson(payload);
                    await httpClient.PostAsync(url, new StringContent(jsonPayload, Encoding.UTF8, "application/json"));
                }
                
            } else {
                await HARRRConnection.AsSignalRHubConnection().SendCoreAsync(MethodNames.ReplyServerRequest, new object[] { id, payload, error });
            }
        }

        private async Task<object> InvokeMethodAsync(ServerRequestMessage serverRequestMessage) {



            var methodCallInfo = MethodsCollection.GetMethod(serverRequestMessage.Method);
            if(methodCallInfo == null)
                throw new Exception($"Method '{serverRequestMessage.Method}' not found!");

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

            if (!UsesNewtonsoftJson && !UsesMessagePack) { // System.Text.Json is used
                var requestJson = JsonSerializer.Serialize(message);
                message = Json.Converter.ToObject<ServerRequestMessage>(requestJson);
            } else if (!UsesNewtonsoftJson) {// Messagepack is used
                var requestJson = Json.Converter.ToJson(message);
                message = Json.Converter.ToObject<ServerRequestMessage>(requestJson);
            }

            return message;
        }
    }
}
