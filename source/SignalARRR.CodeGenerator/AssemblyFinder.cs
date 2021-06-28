using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace doob.SignalARRR.CodeGenerator {
    public class AssemblyFinder {
        public List<Type> Types { get; }

       

        private List<Type> _processedTypes = new List<Type>();

        public AssemblyFinder(params Type[] types) {
            Types = types.ToList();
        }

        public List<Assembly> GetNeededAssemblies() {
            var l = new List<Assembly>();


            foreach (var type in Types) {
                var assemblies = GetNeededAssembliesInternal(type);
                foreach (var assembly in assemblies)
                {
                    if (!l.Contains(assembly)) {
                        l.Add(assembly);
                    }
                }
            }

            return l;
        }

        private List<Assembly> GetNeededAssembliesInternal(Type type) {

            
            if (_processedTypes.Contains(type)) {
                return new List<Assembly>();
            }
            
            _processedTypes.Add(type);

            var l = new List<Assembly>();

            if (!l.Contains(type.Assembly)) {
                l.Add(type.Assembly);
            }

            var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance);

            foreach (var methodInfo in methods) {
                var ml = GetNeededAssembliesInternal(methodInfo.ReturnType);
                foreach (var assembly in ml)
                {
                    if (!l.Contains(assembly)) {
                        l.Add(assembly);
                    }
                }

                foreach (var parameterInfo in methodInfo.GetParameters()) {
                    var pl = GetNeededAssembliesInternal(parameterInfo.ParameterType);
                    foreach (var assembly in pl)
                    {
                        if (!l.Contains(assembly)) {
                            l.Add(assembly);
                        }
                    }
                }
            }

            return l;
        }
    }

}
