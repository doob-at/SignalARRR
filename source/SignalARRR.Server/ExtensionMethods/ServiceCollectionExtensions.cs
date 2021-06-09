using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using doob.Reflectensions.Common;
using doob.Reflectensions.ExtensionMethods;
using Microsoft.Extensions.DependencyInjection;
using NamedServices.Microsoft.Extensions.DependencyInjection;
using SignalARRR.Attributes;
using SignalARRR.CodeGenerator;
using SignalARRR.Interfaces;

namespace SignalARRR.Server.ExtensionMethods {
    public static class ServiceCollectionExtensions {
        public static IServiceCollection AddSignalARRR(this IServiceCollection serviceCollection, Action<SignalARRRServerOptionsBuilder> options = null) {

            SignalARRRServerOptions serverOptions = options?.InvokeAction() ?? new SignalARRRServerOptionsBuilder();

            AddSignalARRRMethods(serviceCollection, serverOptions);
            serviceCollection.AddSingleton<ServerRequestManager>();
            serviceCollection.AddSingleton<ServerPushStreamManager>();
            serviceCollection.AddSingleton<InMemoryHARRRClientManager>();
            serviceCollection.AddSingleton<IHARRRClientManager>(sp => sp.GetRequiredService<InMemoryHARRRClientManager>());
            serviceCollection.AddSingleton<ClientManager>(sp => new ClientManager(sp.GetRequiredService<IHARRRClientManager>()));
            serviceCollection.AddTransient(typeof(ClientContextDispatcher<>));

            foreach (var type in serverOptions.PreBuiltClientMethods) {
                ClassCreator.CreateTypeFromInterface(type);
            }
            return serviceCollection;
        }


        private static void AddSignalARRRMethods(IServiceCollection serviceCollection, SignalARRRServerOptions serverOptions) {


            
            Dictionary<Type, ISignalARRRMethodsCollection> hubMethodsDictionary = serverOptions.AssembliesContainingServerMethods
                .SelectMany(ass => ass.GetTypes().WhichInheritFromClass(typeof(HARRR))).ToDictionary(type => type,
                    hubType => {

                        //var serviceType = typeof(SignalARRRServerMethodsCollection<>).MakeGenericType(hubType);
                        var genColl = new SignalARRRMethodsCollection();
                        
                       

                        var messageMethodsWithName = hubType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly).Select(m =>
                            (MethodInfo: m, Attribute: m.GetCustomAttribute<MessageNameAttribute>()));

                        foreach (var (methodInfo, methodNameAttribute) in messageMethodsWithName) {
                            var methodName = methodNameAttribute?.Name ?? methodInfo.Name;
                            genColl.AddMethod(methodName, methodInfo);
                        }

                        return (ISignalARRRMethodsCollection)genColl;
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
                        coll.AddMethod(concatNames, methodInfo);
                    }

                    var interfaceCollection = new SignalARRRInterfaceCollection();
                    foreach (var @interface in type.GetInterfaces()) {
                        interfaceCollection.RegisterInterface(@interface, type);
                    }

                    var n = type.BaseType?.GenericTypeArguments[0].FullName;
                    serviceCollection.AddNamedSingleton<ISignalARRRMethodsCollection>(n, _ => coll);
                    serviceCollection.AddNamedTransient<ISignalARRRInterfaceCollection>(n, _ => interfaceCollection);

                }
            }

            //foreach (var (key, (collection, serviceType)) in hubMethodsDictionary)
            //{
            //    serviceCollection.AddSingleton(serviceType, collection);
            //}

        }
    }
}
