using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Security;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using Reflectensions;
using Reflectensions.ExtensionMethods;
using Reflectensions.Helper;
using SignalARRR.Attributes;
using SignalARRR.Client.ExtensionMethods;
using SignalARRR.Constants;
using SignalARRR.Helper;
using SignalARRR.RemoteReferenceTypes;

namespace SignalARRR.Client {
    public class MessageHandler {
        private readonly HARRRContext _harrrContext;
        private ISignalARRRClientMethodsCollection MethodsCollection { get; set; } = new SignalARRRClientMethodsCollection();

        private ConcurrentDictionary<string, Delegate> RegisteredTypes = new ConcurrentDictionary<string, Delegate>();
        public MessageHandler(HARRRContext harrrContext) {
            _harrrContext = harrrContext;
        }

        public async Task ChallengeAuthentication(ServerRequestMessage message) {

            string payload = null;
            string error = null;
            try {
                payload = await _harrrContext.AccessTokenProvider();
            } catch (Exception e) {
                error = e.GetBaseException().Message;
            }
            

            await _harrrContext.GetHubConnection().SendCoreAsync(MethodNames.ReplyServerRequest, new object[] { message.Id, payload, error });

        }

        public async Task InvokeServerRequest(ServerRequestMessage message) {
            
            try {
                message = PrepareServerRequestMessage(message);
                var payload = await InvokeMethodAsync(message);
                await SendResponse(message.Id, payload, null);
            } catch (Exception e) {
                await _harrrContext.GetHubConnection().SendCoreAsync(MethodNames.ReplyServerRequest, new object[] { message.Id, null, e.GetBaseException().Message });
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


        //public void RegisterMethods<TClass>(string prefix = null) where TClass : class {
        //    RegisterMethods(typeof(TClass), typeof(TClass), prefix);
        //}
        //public void RegisterMethods<TClass>(TClass instance, string prefix = null) where TClass : class {
        //    RegisterMethods(typeof(TClass), typeof(TClass), instance, prefix);
        //}
        //public void RegisterMethods<TClass>(Func<TClass> factory, string prefix = null) where TClass : class {
        //    RegisterMethods(typeof(TClass), typeof(TClass), factory, prefix);
        //}

        //public void RegisterMethods<TInterface, TClass>(string prefix = null) where TClass : class, TInterface {
        //    RegisterMethods(typeof(TInterface), typeof(TClass), prefix);
        //}
        //public void RegisterMethods<TInterface, TClass>(TClass instance, string prefix = null) where TClass : class, TInterface {
        //    RegisterMethods(typeof(TInterface), typeof(TClass), instance, prefix);
        //}
        //public void RegisterMethods<TInterface, TClass>(Func<TClass> factory, string prefix = null) where TClass : class, TInterface {
        //    RegisterMethods(typeof(TInterface), typeof(TClass), factory, prefix);
        //}

        //public void RegisterMethods(Type interfaceType, Type instanceType, string prefix = null) {
        //    Func<object> factory = () => {
        //        var fromServiceProvider = _harrrContext.GetHubConnection().GetServiceProvider().GetService(instanceType);
        //        if (fromServiceProvider != null) {
        //            return fromServiceProvider;
        //        }

        //        return Activator.CreateInstance(instanceType);
        //    };
        //    RegisterMethods(interfaceType, instanceType, factory, prefix);
        //}
        //public void RegisterMethods(Type interfaceType, Type instanceType, object instance, string prefix = null) {
        //    RegisterMethods(interfaceType, instanceType, () => instance, prefix);
        //}
        //public void RegisterMethods(Type interfaceType, Type instanceType, Func<object> factory, string prefix = null) {

        //    var rootName = instanceType.GetCustomAttribute<MessageNameAttribute>()?.Name ?? prefix.ToNull() ?? instanceType.Name;
        //    var methodsWithName = interfaceType.GetMethods().Select(m => (MethodInfo: m, Attribute: m.GetCustomAttribute<MessageNameAttribute>()));
        //    foreach (var (methodInfo, methodNameAttribute) in methodsWithName) {
        //        var methodName = methodNameAttribute?.Name ?? methodInfo.Name;
        //        var concatNames = $"{rootName}.{methodName}";
        //        MethodsCollection.AddMethod(interfaceType, methodInfo, factory);
        //    }
        //}


        public void RegisterType<TInterface, TClass>() where TClass : class, TInterface {
            
            Func<IServiceProvider, TClass> factory = (sp) => {
                var fromServiceProvider = sp.GetService<TClass>();
                if (fromServiceProvider != null) {
                    return fromServiceProvider;
                }

                return Activator.CreateInstance<TClass>();
            };
            
            RegisterType<TInterface, TClass>(factory);
        }
        public void RegisterType<TInterface, TClass>(TClass instance) where TClass : class, TInterface {
            
            RegisterType<TInterface, TClass>((sp) => instance);
        }

        public void RegisterType<TInterface, TClass>(Func<IServiceProvider, TClass> factory)
            where TClass : class, TInterface {

            RegisteredTypes.AddOrUpdate(typeof(TInterface).FullName, factory, (s, del) => factory);
        }


        public void RegisterISignalARRRClientMethodsCollection(ISignalARRRClientMethodsCollection methodsCollection) {
            MethodsCollection = methodsCollection;
        }




        private async Task SendResponse(Guid id, object payload, string error) {

            if (_harrrContext.UseHttpResponse) {
                var url = _harrrContext.GetResponseUri(id, error);
                var httpClient = new HttpClient();

                if (!string.IsNullOrEmpty(error)) {
                    await httpClient.PostAsync(url, null);
                } else {
                    var jsonPayload = Json.Converter.ToJson(payload);
                    await httpClient.PostAsync(url, new StringContent(jsonPayload, Encoding.UTF8, "application/json"));
                }
                
            } else {
                await _harrrContext.GetHubConnection().SendCoreAsync(MethodNames.ReplyServerRequest, new object[] { id, payload, error });
            }
        }

        private async Task<object> InvokeMethodAsync(ServerRequestMessage serverRequestMessage) {

            if (serverRequestMessage.Method.Contains("|")) {
                return await InvokeInterfaceMethodAsync(serverRequestMessage);
            }

            var methodCallInfo = MethodsCollection.GetMethod(serverRequestMessage.Method);
            if(methodCallInfo == null)
                throw new Exception($"Method '{serverRequestMessage.Method}' not found!");

            var methodInfo = methodCallInfo.MethodInfo;
            if (methodCallInfo == null || methodInfo == null) {
                var errorMsg = $"Method '{serverRequestMessage.Method}' not found";

                throw new Exception(errorMsg);
            }

            CancellationToken cancellationToken = default;
            if (serverRequestMessage.CancellationGuid.HasValue) {
                var cancellation = new CancellationTokenSource();
                cancellationTokenSources.TryAdd(serverRequestMessage.CancellationGuid.Value, cancellation);
                cancellationToken = cancellation.Token;
            }
            

            var parameters = await BuildExecuteMethodParameters(methodCallInfo.MethodInfo, serverRequestMessage.Arguments, cancellationToken);

            var instance = methodCallInfo.Factory != null ? methodCallInfo.Factory.DynamicInvoke() : Activator.CreateInstance(methodInfo.DeclaringType);

            if (serverRequestMessage.GenericArguments?.Any() == true) {

                var arrType = serverRequestMessage.GenericArguments.Select(TypeHelper.FindType).ToList();
                methodInfo = methodInfo.MakeGenericMethod(arrType.ToArray());
            }

            object result = null;
            if (methodInfo.ReturnType == typeof(void) || methodInfo.ReturnType == typeof(Task)) {
                await InvokeHelper.InvokeVoidMethodAsync(instance, methodInfo, parameters);
            } else {
                result = await InvokeHelper.InvokeMethodAsync<object>(instance, methodInfo, parameters);
            }

            if (serverRequestMessage.CancellationGuid.HasValue) {
                cancellationTokenSources.TryRemove(serverRequestMessage.CancellationGuid.Value, out var token);
            }

            return result;
        }

        private async Task<object> InvokeInterfaceMethodAsync(ServerRequestMessage serverRequestMessage) {

            var splitted = serverRequestMessage.Method.Split('|');
            var interfaceTypeName = splitted.First();

            if (!RegisteredTypes.TryGetValue(interfaceTypeName, out var factory)) {
                throw new TypeLoadException($"Can't find interface '{interfaceTypeName}'");
            }

            
            var instance = factory.DynamicInvoke();


            return null;
        }



        private ConcurrentDictionary<Guid, CancellationTokenSource> cancellationTokenSources = new ConcurrentDictionary<Guid, CancellationTokenSource>();

        private async Task<object[]> BuildExecuteMethodParameters(MethodInfo methodInfo, IEnumerable<object> parameters, CancellationToken cancellation = default) {

            int paramsPosition = 0;
            var @params = parameters.ToList();

            var argumentList = new List<object>();

            foreach (var parameterInfo in methodInfo.GetParameters())
            {
                if (@params.Count < paramsPosition) {
                    throw new IndexOutOfRangeException();
                }
                var par = @params[paramsPosition];
                paramsPosition++;

                if (parameterInfo.ParameterType == typeof(CancellationToken)) {
                    argumentList.Add(cancellation);
                    continue;
                }

                par = await PrepareArgumentForType(parameterInfo.ParameterType, par);

                if (par == null) {
                    argumentList.Add(null);
                    continue;
                }

                if (parameterInfo.ParameterType != par.GetType()) {

                    if (par.TryTo(parameterInfo.ParameterType, out var pt)) {
                        par = pt;
                    } else {
                        var json = Json.Converter.ToJson(par);
                        par = Json.Converter.ToObject(json, parameterInfo.ParameterType);
                    }
                   
                }

                argumentList.Add(par);

            }

            return argumentList.ToArray();
            //return methodInfo.GetParameters().Select(p => {

            //    if (p.ParameterType == typeof(CancellationToken)) {
            //        return cancellation;
            //    }

            //    if (@params.Count < paramsPosition) {
            //        throw new IndexOutOfRangeException();
            //    }

            //    var par = @params[paramsPosition];

            //    par = await PrepareArgumentForType(p.ParameterType, par);

            //    if (par != null && p.ParameterType != par.GetType()) {

            //        if (par is JToken jt) {
            //            par = jt.ToObject(p.ParameterType);
            //        } else {
            //            par = par.To(p.ParameterType);
            //        }

            //    }

            //    paramsPosition++;
            //    return par;

            //}).ToArray();

        }

        private async Task<object> PrepareArgumentForType(Type type, object argument) {

            if (argument == null) {
                if (type.IsNullableType()) {
                    return null;
                } else {
                    return Activator.CreateInstance(type);
                }
            }

            if (type == typeof(Stream)) {
                
                var json = Json.Converter.ToJson(argument);
                var streamReference = Json.Converter.ToObject<StreamReference>(json);
                var resolver = new StreamReferenceResolver(streamReference, _harrrContext);
                return await resolver.ProcessStreamArgument();
            }

            return argument;
        }

        


        private ServerRequestMessage PrepareServerRequestMessage(ServerRequestMessage message) {
            switch (_harrrContext.HubProtocolType)
            {
                case HubProtocolType.JsonHubProtocol:
                {
                    var requestJson = JsonSerializer.Serialize(message);
                    message = Json.Converter.ToObject<ServerRequestMessage>(requestJson);
                    break;
                }
                case HubProtocolType.MessagePackHubProtocol:
                {
                    var requestJson = Json.Converter.ToJson(message);
                    message = Json.Converter.ToObject<ServerRequestMessage>(requestJson);
                    break;
                }
            }

            return message;
        }

        public void CancelTokenFromServer(ServerRequestMessage requestMessage) {

            if (requestMessage.CancellationGuid.HasValue) {
                if (cancellationTokenSources.TryRemove(requestMessage.CancellationGuid.Value, out var token)) {
                    token.Cancel();
                }
            }

        }
    }
}
