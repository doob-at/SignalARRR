using System;
using System.Reflection;

namespace doob.SignalARRR.Common {
    public class ClientMethodsCache {

        public MethodInfo MethodInfo { get; set; }

        public Delegate Factory { get; set; }

    }
}
