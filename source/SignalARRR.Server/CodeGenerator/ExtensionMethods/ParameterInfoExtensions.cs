using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace doob.SignalARRR.Server.CodeGenerator.ExtensionMethods {
    public static class ParameterInfoExtensions {


        public static IEnumerable<ParameterInfo> WithName(this IEnumerable<ParameterInfo> parameterInfos, string name,
            bool ignoreCase) {

            var stringComparison = ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            return parameterInfos.Where(p => p.Name.Equals(name, stringComparison));
        }

        public static IEnumerable<ParameterInfo> WithType(this IEnumerable<ParameterInfo> parameterInfos, Type type) {
            return parameterInfos.Where(p => p.ParameterType == type);
        }

        public static IEnumerable<ParameterInfo> WithoutType(this IEnumerable<ParameterInfo> parameterInfos, Type type) {
            return parameterInfos.Where(p => p.ParameterType != type);
        }

        public static IEnumerable<ParameterInfo> WithType<T>(this IEnumerable<ParameterInfo> parameterInfos) {
            return parameterInfos.WithType(typeof(T));
        }
        public static IEnumerable<ParameterInfo> WithoutType<T>(this IEnumerable<ParameterInfo> parameterInfos) {
            return parameterInfos.WithoutType(typeof(T));
        }

        public static IEnumerable<ParameterInfo> WithoutAttribute(this IEnumerable<ParameterInfo> parameterInfos, string attributeName) {
            return parameterInfos
                .Where(p => !p.GetCustomAttributes()
                    .Any(attr => attr.GetType().Name.Equals(attributeName))
                );
        }

        public static string ToParamsArrayText(this IEnumerable<ParameterInfo> parameterInfos, string variableName) {
            
            return $"var {variableName} = new object[] {{{string.Join(", ", parameterInfos.Select(p => p.Name))}}};";
        }
    }
}
