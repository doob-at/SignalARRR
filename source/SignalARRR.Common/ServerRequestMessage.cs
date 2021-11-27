using System;
using System.Collections.Generic;
using System.Linq;

namespace doob.SignalARRR.Common {
    public class ServerRequestMessage {

        public Guid Id { get; set; }
        public string? InterfaceType { get; set; }
        public string Method { get; set; }
        public object[] Arguments { get; set; }
        public string[] GenericArguments { get; set; }
        public Guid? CancellationGuid { get; set; }

        public ServerRequestMessage()
        {
            Id = Guid.NewGuid();
        }

        public ServerRequestMessage(string methodName): this() {
            if (methodName.Contains("|")) {
                InterfaceType = methodName.Split('|')[0];
                methodName = methodName.Split('|')[1];
            }

            Method = methodName;
        }

        public ServerRequestMessage WithArguments(params object[] arguments) {
            return WithArguments(arguments.ToList());
        }

        public ServerRequestMessage WithArguments(IEnumerable<object> arguments) {
            Arguments = arguments.ToArray();
            return this;
        }

        public ServerRequestMessage WithInterface(Type interfaceType) {
            InterfaceType = interfaceType.FullName;
            return this;
        }

    }
}
