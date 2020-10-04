using System;
using System.Collections.Generic;
using System.Reflection;

namespace SignalARRR.Client {
    internal interface ISignalARRRClientMethodsCollection {
        void AddMethod(string name, MethodInfo methodInfo);
        void AddMethod<T>(string name, MethodInfo methodInfo, Func<T> factory = null);
        MethodCallInfo GetMethod(string clientMessageMethod);
    }

    internal class SignalARRRClientMethodsCollection : ISignalARRRClientMethodsCollection  {

        private readonly Dictionary<string, MethodCallInfo> _collection = new Dictionary<string, MethodCallInfo>();

        public void AddMethod(string name, MethodInfo methodInfo) {
            if (!_collection.ContainsKey(name)) {
                var mci = new MethodCallInfo(methodInfo);
                _collection.Add(name, mci);
            }
        }

        public void AddMethod<T>(string name, MethodInfo methodInfo, Func<T> factory = null) {
            if (!_collection.ContainsKey(name)) {
                var mci = new MethodCallInfo(methodInfo, factory);
                _collection.Add(name, mci);
            }
        }

        public MethodCallInfo GetMethod(string name) {
            return _collection.TryGetValue(name, out var methodInfo) ? methodInfo : null;
        }

    }

    internal class MethodCallInfo {

        public MethodInfo MethodInfo { get; set; }

        public Delegate Factory { get; set; }


        public MethodCallInfo(MethodInfo methodInfo, Delegate factory = null) {
            MethodInfo = methodInfo;
            Factory = factory;
        }
    }
}
