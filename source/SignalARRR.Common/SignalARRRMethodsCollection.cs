using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using doob.Reflectensions.ExtensionMethods;
using doob.SignalARRR.Common.Interfaces;

namespace doob.SignalARRR.Common {
    public class SignalARRRMethodsCollection: ISignalARRRMethodsCollection {

        private readonly ConcurrentDictionary<string, List<(Delegate Factory, MethodInfo MethodInfo)>> _collection = new();

        public void AddMethod(string name, MethodInfo methodInfo) {

            object Factory(IServiceProvider sp) {
                var fromServiceProvider = sp.GetService(methodInfo.DeclaringType);
                if (fromServiceProvider != null) {
                    return fromServiceProvider;
                }

                return Activator.CreateInstance(methodInfo.DeclaringType);
            }

            AddMethod(name, methodInfo, Factory);
        }

        public void AddMethod(string name, MethodInfo methodInfo, object instance) {
            AddMethod(name, methodInfo, (sp) => instance);
        }

        public void AddMethod<T>(string name, MethodInfo methodInfo, Func<IServiceProvider, T> factory = null) {
            _collection.AddOrUpdate(name, AdaptList((factory, methodInfo), null), (s, list) => {
                return AdaptList((factory, methodInfo), list);
            });
        }

        private List<(Delegate Factory, MethodInfo MethodInfo)> AdaptList((Delegate Factory, MethodInfo MethodInfo) value, List<(Delegate Factory, MethodInfo MethodInfo)>? list) {

            if (list == null)
                list = new List<(Delegate Factory, MethodInfo MethodInfo)>();

            list.Add(value);
            return list;
        }

        public (Delegate Factory, MethodInfo MethodInfo) GetMethodInformations(string name, Type[] parameterTypes) {
            if (_collection.TryGetValue(name, out var methodsList)) {

                return methodsList
                    .Where(k => k.MethodInfo.HasParametersOfType(parameterTypes))
                    .FirstOrDefault();

            }

            throw new Exception($"Method '{name}' not found!");
        }
    }
}
