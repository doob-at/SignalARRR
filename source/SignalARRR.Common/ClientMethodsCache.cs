using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace SignalARRR {
    public class ClientMethodsCache {

        public MethodInfo MethodInfo { get; set; }

        public Delegate Factory { get; set; }

    }
}
