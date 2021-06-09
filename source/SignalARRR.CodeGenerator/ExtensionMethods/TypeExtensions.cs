using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Channels;
using doob.Reflectensions.ExtensionMethods;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;


namespace SignalARRR.CodeGenerator.ExtensionMethods {
    public static class TypeExtensions {
        public static TypeSyntax AsTypeSyntax(this Type type) {

            if (type == typeof(void)) {
                return SyntaxFactory.ParseTypeName("void");
            }

            if (type.IsGenericParameter) {
                return SyntaxFactory.ParseTypeName(type.Name);
            }

            var name = $"{type.Namespace}.{type.Name.Replace('+', '.')}";

            if (type.IsGenericType) {
                // Get the C# representation of the generic type minus its type arguments.
                name = name.Substring(0, name.IndexOf("`"));

                // Generate the name of the generic type.
                var genericArgs = type.GetGenericArguments();
                return SyntaxFactory.GenericName(SyntaxFactory.Identifier(name),
                    SyntaxFactory.TypeArgumentList(SyntaxFactory.SeparatedList(genericArgs.Select(AsTypeSyntax)))
                );
            } else {
                return SyntaxFactory.ParseTypeName(name);
            }

        }

        public static string AsGenericArgumentsText(this TypeSyntax type) {

            if (type != null) {
                return $"<{type}>";
            }

            return String.Empty;
        }

        public static string AsGenericArgumentsText(this Type type) {

            if (type != null) {
                return type.AsTypeSyntax().AsGenericArgumentsText();
            }

            return String.Empty;
        }

        public static IEnumerable<TypeSyntax> AsTypeSyntax(this IEnumerable<Type> types) {

            return types?.Select(t => t.AsTypeSyntax()) ?? Enumerable.Empty<TypeSyntax>();
        }

        public static string AsGenericArgumentsText(this IEnumerable<TypeSyntax> types) {

            if (types?.Any() == true) {
                return $"<{String.Join(", ", types)}>";
            }
            return String.Empty;
        }

        public static string AsGenericArgumentsText(this IEnumerable<Type> types) {

            return types.AsTypeSyntax().AsGenericArgumentsText();
        }

        public static (bool IsStreamingType, StreamingType StreamingType) IsStreamingType(this Type type) {


            if (type.IsGenericTypeOf(typeof(IObservable<>))) {
                return (true, StreamingType.Observable);
            }

            if (type.IsGenericTypeOf(typeof(ChannelReader<>))) {
                return (true, StreamingType.ChannelReader);
            }

            if (type.IsGenericTypeOf(typeof(IAsyncEnumerable<>))) {
                return (true, StreamingType.AsyncEnumerable);
            }

            return (false, StreamingType.None);

        }
    }
}
