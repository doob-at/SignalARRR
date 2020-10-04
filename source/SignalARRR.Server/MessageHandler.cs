using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Newtonsoft.Json.Linq;
using Reflectensions.ExtensionMethods;
using Reflectensions.Helper;
using SignalARRR.Exceptions;
using SignalARRR.Helper;
using ObservableExtensions = SignalARRR.Server.ExtensionMethods.ObservableExtensions;

namespace SignalARRR.Server {
    internal class MessageHandler {

        private ISignalARRRServerMethodsCollection MethodsCollection { get; }

        private ILogger Logger { get; }

        private ClientContext ClientContext { get; }

        private HARRR HARRR { get; }

        private IServiceProvider _serviceProvider;

        public MessageHandler(HARRR harrr, ClientContext clientContext, ISignalARRRServerMethodsCollection methodsCollection, IServiceProvider serviceProvider) {
            HARRR = harrr;
            MethodsCollection = methodsCollection;
            ClientContext = clientContext;
            _serviceProvider = serviceProvider;
            Logger = _serviceProvider.GetService<ILoggerFactory>()?.CreateLogger(GetType().FullName) ?? NullLogger.Instance;
        }

        public async Task<IAsyncEnumerable<object>> InvokeStreamAsync(ClientRequestMessage clientMessage, CancellationToken cancellationToken) {

            



            var methodInfo = MethodsCollection.GetMethod(clientMessage.Method);

            if (methodInfo == null) {
                var errorMsg = $"Method '{clientMessage.Method}' not found";
                Logger.LogError(errorMsg);
                throw new Exception(errorMsg);
            }

            var authentication = new SignalARRRAuthentication(_serviceProvider);
            var result = await authentication.Authorize(ClientContext, clientMessage.Authorization, methodInfo);

            if (!result.Succeeded) {
                throw new UnauthorizedException();
            }

            var instance = BuildInvokeTypeInstance(methodInfo);

            var taskType = methodInfo.ReturnType;
            if (taskType.IsGenericTypeOf(typeof(Task<>))) {
                taskType = methodInfo.ReturnType.GenericTypeArguments[0];
            }

            var parameters = BuildExecuteMethodParameters(methodInfo, clientMessage.Arguments, cancellationToken);


            if (taskType.IsGenericTypeOf(typeof(ChannelReader<>))) {
                return await InvokeStreamingMethodAsync(instance, methodInfo, parameters).ConfigureAwait(false);
            }

            if (taskType.IsGenericTypeOf(typeof(IAsyncEnumerable<>))) {
                return await InvokeStreamingMethodAsync(instance, methodInfo, parameters).ConfigureAwait(false);
            }

            if (taskType.IsGenericTypeOf(typeof(IObservable<>))) {
                return await InvokeIObservableMethodAsync(instance, methodInfo, cancellationToken, parameters).ConfigureAwait(false);
            }


            throw new NotSupportedException();

        }

        public async Task<object> InvokeMethodAsync(ClientRequestMessage clientMessage) {

           

            var methodInfo = MethodsCollection.GetMethod(clientMessage.Method);

            if (methodInfo == null) {
                var errorMsg = $"Method '{clientMessage.Method}' not found";
                Logger.LogError(errorMsg);
                throw new Exception(errorMsg);
            }


            var authentication = new SignalARRRAuthentication(_serviceProvider);
            var result = await authentication.Authorize(ClientContext, clientMessage.Authorization, methodInfo);

            if (!result.Succeeded) {
                throw new UnauthorizedException();
            }

            var parameters = BuildExecuteMethodParameters(methodInfo, clientMessage.Arguments);

            var instance = BuildInvokeTypeInstance(methodInfo);

            if (clientMessage.GenericArguments?.Any() == true) {

                var arrType = clientMessage.GenericArguments.Select(TypeHelper.FindType).ToList();
                methodInfo = methodInfo.MakeGenericMethod(arrType.ToArray());
            }

            if (methodInfo.ReturnType == typeof(void) || methodInfo.ReturnType == typeof(Task)) {
                await InvokeHelper.InvokeVoidMethodAsync(instance, methodInfo, parameters);
                return null;
            } else {
                return await InvokeHelper.InvokeMethodAsync<object>(instance, methodInfo, parameters);
            }


        }

        private object BuildInvokeTypeInstance(MethodInfo methodInfo) {

            object instance;
            if (methodInfo.DeclaringType == HARRR.GetType()) {
                instance = ActivatorUtilities.CreateInstance(_serviceProvider, HARRR.GetType());
            } else {
                instance = _serviceProvider.GetRequiredService(methodInfo.ReflectedType);
            }
            instance.SetPropertyValue("ClientContext", ClientContext);
            instance.SetPropertyValue("Context", HARRR.Context);
            instance.SetPropertyValue("Clients", HARRR.Clients);
            instance.SetPropertyValue("Groups", HARRR.Groups);
            var logger = _serviceProvider.GetService<ILoggerFactory>()?.CreateLogger(instance.GetType().FullName) ?? NullLogger.Instance;
            instance.SetPropertyValue("Logger", logger);

            return instance;
        }

        private async Task<StreamingResult> InvokeStreamingMethodAsync(object instance, MethodInfo methodInfo, params object[] parameters) {

            var ch = await InvokeHelper.InvokeMethodAsync<object>(instance, methodInfo, parameters).ConfigureAwait(false);

            Type taskType = methodInfo.ReturnType;
            if (taskType.GetGenericTypeDefinition() == typeof(Task<>)) {
                taskType = methodInfo.ReturnType.GenericTypeArguments[0];
            }


            var convType = typeof(StreamingResult<>).MakeGenericType(taskType.GenericTypeArguments[0]);

            var conv = (StreamingResult)Activator.CreateInstance(convType, ch, ClientContext, methodInfo);

            return conv;
        }

        private async Task<StreamingResult> InvokeIObservableMethodAsync(object instance, MethodInfo methodInfo, CancellationToken cancellationToken, params object[] parameters) {

            var ch = await InvokeHelper.InvokeMethodAsync<object>(instance, methodInfo, parameters).ConfigureAwait(false);

            Type taskType = methodInfo.ReturnType;
            if (taskType.GetGenericTypeDefinition() == typeof(Task<>)) {
                taskType = methodInfo.ReturnType.GenericTypeArguments[0];
            }

            var obsGenType = taskType.GenericTypeArguments[0];
            // ReSharper disable once PossibleNullReferenceException
            var convMethod = typeof(ObservableExtensions).GetMethod("AsChannelReaderInternal", BindingFlags.Static | BindingFlags.NonPublic).MakeGenericMethod(obsGenType);
            var channelReader = convMethod.Invoke(null, new[] { ch, cancellationToken });

            var convType = typeof(StreamingResult<>).MakeGenericType(taskType.GenericTypeArguments[0]);
            var conv = (StreamingResult)Activator.CreateInstance(convType, channelReader, ClientContext, methodInfo);

            return conv;
        }


        private object[] BuildExecuteMethodParameters(MethodInfo methodInfo, IEnumerable<object> parameters, CancellationToken cancellation = default) {

            int paramsPosition = 0;
            var @params = parameters.ToList();
            return methodInfo.GetParameters().Select(p => {

                if (p.ParameterType == typeof(CancellationToken)) {
                    return cancellation;
                }
                
                var fromServices = p.GetCustomAttribute<FromServicesAttribute>();

                if (fromServices != null) {
                    return _serviceProvider.GetRequiredService(p.ParameterType);
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


    }
}
