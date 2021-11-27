using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace doob.SignalARRR.Common {
    public class ClientRequestMessage {
        public string? InterfaceType { get; set; }
        public string Method { get; set; }
        public string? Authorization { get; set; }
        public object[]? Arguments { get; set; }
        public string[]? GenericArguments { get; set; }

        //public ClientRequestMessage() { }

        //public ClientRequestMessage(Type interfaceType) {
        //    InterfaceType = interfaceType.FullName;
        //}

        public ClientRequestMessage(string methodName) {
            
            if (methodName.Contains("|")) {
                InterfaceType = methodName.Split('|')[0];
                methodName = methodName.Split('|')[1];
            }

            Method = methodName;
        }


        public ClientRequestMessage WithArguments(params object[] arguments) {
            return WithArguments(arguments.ToList());
        }

        public ClientRequestMessage WithArguments(IEnumerable<object> arguments) {
            Arguments = arguments.ToArray();
            return this;
        }

        public ClientRequestMessage WithInterface(Type interfaceType) {
            InterfaceType = interfaceType.FullName;
            return this;
        }

        public ClientRequestMessage WithAuthorization(string authorization) {
            Authorization = authorization;
            return this;
        }

        public ClientRequestMessage WithAuthorization(Func<Task<string>> authorization) {
            Authorization = authorization?.Invoke().GetAwaiter().GetResult();
            return this;
        }

    }
}
