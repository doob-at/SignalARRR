using System.Collections.Generic;
using System.Reflection;

namespace SignalARRR.Server {

    internal interface ISignalARRRServerMethodsCollection {
        void AddMethod(string name, MethodInfo methodInfo);
        MethodInfo GetMethod(string clientMessageMethod);
    }

    internal class SignalARRRServerMethodsCollection<T> : ISignalARRRServerMethodsCollection where T : HARRR {
        private readonly Dictionary<string, MethodInfo> _collection = new Dictionary<string, MethodInfo>();

        public void AddMethod(string name, MethodInfo methodInfo) {
            _collection.TryAdd(name, methodInfo);
        }

        public MethodInfo GetMethod(string name) {
            return _collection.TryGetValue(name, out var methodInfo) ? methodInfo : null;
        }

    }
}
