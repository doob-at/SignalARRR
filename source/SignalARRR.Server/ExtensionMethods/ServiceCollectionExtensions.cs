using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Reflectensions.ExtensionMethods;
using SignalARRR.Attributes;

namespace SignalARRR.Server.ExtensionMethods {
    public static class ServiceCollectionExtensions {
        public static IServiceCollection AddSignalARRR(this IServiceCollection serviceCollection, Action<SignalARRRServerOptionsBuilder> options = null) {

            SignalARRRServerOptions serverOptions = options?.InvokeAction() ?? new SignalARRRServerOptionsBuilder();

            AddSignalARRRMethods(serviceCollection, serverOptions);
            serviceCollection.AddSingleton<ServerRequestManager>();
            serviceCollection.AddSingleton<InMemoryHARRRClientManager>();
            serviceCollection.AddSingleton<IHARRRClientManager>(sp => sp.GetRequiredService<InMemoryHARRRClientManager>());
            serviceCollection.AddSingleton<ClientManager>(sp => new ClientManager(sp.GetRequiredService<IHARRRClientManager>()));
            serviceCollection.AddTransient(typeof(ClientContextDispatcher<>));

            return serviceCollection;
        }


        private static void AddSignalARRRMethods(IServiceCollection serviceCollection, SignalARRRServerOptions serverOptions) {


            Dictionary<Type, (ISignalARRRServerMethodsCollection collection, Type serviceType)> hubMethodsDictionary = serverOptions.AssembliesContainingServerMethods
                .SelectMany(ass => ass.GetTypes().WhichInheritFromClass(typeof(HARRR))).ToDictionary(type => type,
                    hubType => {
                        var serviceType = typeof(SignalARRRServerMethodsCollection<>).MakeGenericType(hubType);
                        var genColl = (ISignalARRRServerMethodsCollection)Activator.CreateInstance(serviceType);

                        var messageMethodsWithName = hubType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly).Select(m =>
                            (MethodInfo: m, Attribute: m.GetCustomAttribute<MessageNameAttribute>()));

                        foreach (var (methodInfo, methodNameAttribute) in messageMethodsWithName) {
                            var methodName = methodNameAttribute?.Name ?? methodInfo.Name;
                            genColl.AddMethod(methodName, methodInfo);
                        }

                        return  (genColl, serviceType);
                    });

           var serverMethodsFromAllAssemblies = serverOptions.AssembliesContainingServerMethods
                .SelectMany(ass => ass.GetTypes().WhichInheritFromClass(typeof(ServerMethods<>)))
                .GroupBy(ass => ass.BaseType?.GenericTypeArguments[0]);

            foreach (var grouping in serverMethodsFromAllAssemblies) {

                if(!hubMethodsDictionary.TryGetValue(grouping.Key, out var coll))
                    continue;

                
                foreach (var type in grouping) {

                    serviceCollection.AddTransient(type);
                    var rootName = type.GetCustomAttribute<MessageNameAttribute>()?.Name ?? type.Name;
                    var methodsWithName = type.GetMethods().Select(m => (MethodInfo: m, Attribute: m.GetCustomAttribute<MessageNameAttribute>()));
                    foreach (var (methodInfo, methodNameAttribute) in methodsWithName) {
                        var methodName = methodNameAttribute?.Name ?? methodInfo.Name;
                        var concatNames = $"{rootName}.{methodName}";
                        coll.collection.AddMethod(concatNames, methodInfo);
                    }

                }
            }

            foreach (var (key, (collection, serviceType)) in hubMethodsDictionary)
            {
                serviceCollection.AddSingleton(serviceType, collection);
            }

        }
    }
}
