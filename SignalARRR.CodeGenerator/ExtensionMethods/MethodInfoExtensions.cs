using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace SignalARRR.CodeGenerator.ExtensionMethods {
    public static class MethodInfoExtensions {

        public static (string ArrayBuilder, string Parameter) GetGenericArgumentsAsString(this MethodInfo methodInfo) {

            var genericArguments = methodInfo.GetGenericArguments().Select(arg => $"typeof({arg.Name}).FullName").ToList();
            
            var arrayBuilder = $"var gargs = new string[] {{{string.Join(", ", genericArguments)}}};";

            return (arrayBuilder, $", gargs");
        }
    }
}
