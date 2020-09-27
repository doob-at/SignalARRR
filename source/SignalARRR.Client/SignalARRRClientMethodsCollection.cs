using System.Collections.Generic;
using System.Reflection;

namespace SignalARRR.Client {
    internal interface ISignalARRRClientMethodsCollection {
        void AddMethod(string name, MethodInfo methodInfo);
        MethodInfo GetMethod(string clientMessageMethod);
    }

    internal class SignalARRRClientMethodsCollection : ISignalARRRClientMethodsCollection  {
        private readonly Dictionary<string, MethodInfo> _collection = new Dictionary<string, MethodInfo>();

        public void AddMethod(string name, MethodInfo methodInfo) {
            if (!_collection.ContainsKey(name)) {
                _collection.Add(name, methodInfo);
            }
        }

        public MethodInfo GetMethod(string name) {
            return _collection.TryGetValue(name, out var methodInfo) ? methodInfo : null;
        }

    }
}
