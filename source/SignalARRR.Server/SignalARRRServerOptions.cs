using System.Collections.Generic;
using System.Reflection;

namespace SignalARRR.Server {
    public class SignalARRRServerOptions {

        public List<Assembly> AssembliesContainingServerMethods = new List<Assembly>()
        {
            Assembly.GetEntryAssembly()
        };

    }

    public class SignalARRRServerOptionsBuilder {
        private SignalARRRServerOptions _options = new SignalARRRServerOptions();

        public SignalARRRServerOptionsBuilder AddServerMethodsFrom(params Assembly[] assemblies) {
            foreach (var assembly in assemblies) {
                if (!_options.AssembliesContainingServerMethods.Contains(assembly))
                    _options.AssembliesContainingServerMethods.Add(assembly);
            }

            return this;
        }


        public static implicit operator SignalARRRServerOptions(SignalARRRServerOptionsBuilder builder) {
            return builder._options;
        }
    }
}
