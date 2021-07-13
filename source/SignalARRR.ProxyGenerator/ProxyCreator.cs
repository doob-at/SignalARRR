using System;
using System.Collections.Concurrent;
using ImpromptuInterface;

namespace doob.SignalARRR.ProxyGenerator {
    public class ProxyCreator {

        private static ConcurrentDictionary<Type, object> generatedTypes { get; } = new();

        public static T CreateInstanceFromInterface<T>(ProxyCreatorHelper classCreatorHelper) where T : class {

            var pr = new SignalARRRDynamicProxy<T>(classCreatorHelper);

            return Impromptu.ActLike<T>(pr);
            //return (T)generatedTypes.GetOrAdd(typeof(T), type => {
            //    var pr = new SignalARRRDynamicProxy<T>(classCreatorHelper);

            //    return Impromptu.ActLike<T>(pr);
            //});

        }
    }
}
