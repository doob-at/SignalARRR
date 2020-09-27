using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Reflectensions.ExtensionMethods;

namespace SignalARRR.Client {
    internal class ClassCreator {
        private static ConcurrentDictionary<Type, Type> generatedTypes { get; } = new ConcurrentDictionary<Type, Type>();

        private Type FromType { get; }


        private ClassCreator(Type fromType) {
            FromType = fromType;
        }




        private MemberDeclarationSyntax CreateMethod(MethodInfo methodInfo) {

            var isVoid = methodInfo.ReturnType == typeof(void);
            var isTask = methodInfo.ReturnType == typeof(Task);
            var isTaskOfT = methodInfo.ReturnType.IsGenericTypeOf(typeof(Task<>));

            var methodParameters = methodInfo.GetParameters().Where(p => !p.GetCustomAttributes().Any(attr => attr.GetType().Name.Equals("FromServicesAttribute")));
            var returnType = isVoid ? SyntaxFactory.ParseTypeName("void") : AsTypeSyntax(methodInfo.ReturnType);

            var body = new StringBuilder();
            body.AppendLine($"var paramsArray = new object[] {{{String.Join(", ", methodParameters.Select(p => p.Name))}}};");
            body.AppendLine($"var methodName = String.IsNullOrWhiteSpace(_nameSpace) ? \"{methodInfo.Name}\" : $\"{{_nameSpace}}.{methodInfo.Name}\";");


            var paramsArray = $"var paramsArray = new object[] {{{String.Join(", ", methodParameters.Where(p => p.ParameterType != typeof(CancellationToken)).Select(p => p.Name))}}};";
            var cancellationToken = methodParameters.Where(p => p.ParameterType == typeof(CancellationToken))
                .Select(p => p.Name).FirstOrDefault().ToNull();


            var taskType = methodInfo.ReturnType;
            if (taskType.IsGenericTypeOf(typeof(Task<>))) {
                taskType = methodInfo.ReturnType.GenericTypeArguments[0];
            }

            var isStreamingMethod = false;
            var streamType = "";

            if (taskType.IsGenericTypeOf(typeof(ChannelReader<>))) {
                streamType = "channel";
                isStreamingMethod = true;
            }

            if (taskType.IsGenericTypeOf(typeof(IAsyncEnumerable<>))) {
                streamType = "asyncenumerable";
                isStreamingMethod = true;
            }

            if (taskType.IsGenericTypeOf(typeof(IObservable<>))) {
                streamType = "observable";
                isStreamingMethod = true;
            }


            var insertCancellationToken = cancellationToken != null ? $", {cancellationToken}" : String.Empty;
            if (isVoid || isTask) {
                body.AppendLine($"var task = _harrConnection.SendCoreAsync(methodName, paramsArray{insertCancellationToken});");
                if (isTask) {
                    body.AppendLine("await task;");
                } else {
                    body.AppendLine("task.GetAwaiter().GetResult();");
                }

            } else if (isStreamingMethod) {

                var invokeGenericArgument = "";


                var args = methodInfo.ReturnType.GetGenericArguments();
                var argsSyntax = args.Select(arg => AsTypeSyntax(arg).ToString());
                invokeGenericArgument = $"<{String.Join(", ", argsSyntax)}>";



                body.AppendLine($"var stream = _harrConnection.StreamAsyncCore{invokeGenericArgument}(methodName, paramsArray{insertCancellationToken});");

                switch (streamType) {
                    case "channel": {
                            body.AppendLine($"return _helper.ToChannelReader(stream{insertCancellationToken});");
                            break;
                        }
                    case "asyncenumerable": {
                            body.AppendLine("return stream;");
                            break;
                        }
                    case "observable": {
                            body.AppendLine("return System.Linq.AsyncEnumerable.ToObservable(stream);");
                            break;
                        }
                }


            } else {
                var invokeGenericArgument = "";

                if (isTaskOfT) {
                    var args = methodInfo.ReturnType.GetGenericArguments()?.ToList();
                    if (args != null && args.Any()) {
                        var argsSyntax = args.Select(arg => AsTypeSyntax(arg).ToString());
                        invokeGenericArgument = $"<{String.Join(", ", argsSyntax)}>";
                    }
                } else {
                    invokeGenericArgument = $"<{AsTypeSyntax(methodInfo.ReturnType)}>";
                }

                body.AppendLine($"var task = _harrConnection.InvokeCoreAsync{invokeGenericArgument}(methodName, paramsArray{insertCancellationToken});");
                if (isTaskOfT) {
                    body.AppendLine($"return await task;");
                } else {
                    body.AppendLine($"return task.GetAwaiter().GetResult();");
                }
            }

            var syntax = SyntaxFactory.ParseStatement(body.ToString());



            var syntaxParameters = methodParameters.Select(p => SyntaxFactory.Parameter(SyntaxFactory.Identifier(p.Name)).WithType(AsTypeSyntax(p.ParameterType)));

            
            // Create a method
            var methodDeclaration = SyntaxFactory.MethodDeclaration(returnType, methodInfo.Name)
                .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))

                .AddParameterListParameters(syntaxParameters.ToArray())
                .WithBody(SyntaxFactory.Block(syntax));

            if (isTaskOfT || isTask) {
                methodDeclaration = methodDeclaration.AddModifiers(SyntaxFactory.Token(SyntaxKind.AsyncKeyword));
            }

            return methodDeclaration;
        }



        private MemberDeclarationSyntax[] CreateMethods() => FromType.GetMethods().Select(CreateMethod).ToArray();


        private MemberDeclarationSyntax CreateConstructor(string className) {

            var harrConnectionParameter = SyntaxFactory.Parameter(SyntaxFactory.Identifier("harrConnection"))
                .WithType(AsTypeSyntax(typeof(HARRRConnection)));
            var nameSpaceParameter = SyntaxFactory.Parameter(SyntaxFactory.Identifier("nameSpace"))
                .WithType(AsTypeSyntax(typeof(string)));
            var helperParameter = SyntaxFactory.Parameter(SyntaxFactory.Identifier("helper"))
                .WithType(AsTypeSyntax(typeof(ClassCreatorHelper)));

            var body = "_harrConnection = harrConnection;_nameSpace = nameSpace;_helper = helper;";


            var constructor = SyntaxFactory.ConstructorDeclaration(className)
                .AddParameterListParameters(harrConnectionParameter, nameSpaceParameter, helperParameter)
                .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
                .WithBody(SyntaxFactory.Block(SyntaxFactory.ParseStatement(body)));

            return constructor;
        }

        public Type Build() {

            return generatedTypes.GetOrAdd(FromType, type => _build());
        }

        public Type _build() {
            var className = $"{FromType.Name}_created";
            var namespaceName = $"GeneratedServerMethodTypes";



            var baseType = SyntaxFactory.SimpleBaseType(AsTypeSyntax(FromType));



            var classDeclaration = SyntaxFactory.ClassDeclaration(className)
                .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
                .AddBaseListTypes(baseType);

            var harrConnectionVariable = SyntaxFactory.VariableDeclaration(AsTypeSyntax(typeof(HARRRConnection)))
                .AddVariables(SyntaxFactory.VariableDeclarator("_harrConnection"));
            var nameSpaceVariable = SyntaxFactory.VariableDeclaration(AsTypeSyntax(typeof(string)))
                .AddVariables(SyntaxFactory.VariableDeclarator("_nameSpace"));
            var helperVariable = SyntaxFactory.VariableDeclaration(AsTypeSyntax(typeof(ClassCreatorHelper)))
                .AddVariables(SyntaxFactory.VariableDeclarator("_helper"));

            var harrConnectionFieldDeclaration = SyntaxFactory.FieldDeclaration(harrConnectionVariable)
                .AddModifiers(SyntaxFactory.Token(SyntaxKind.PrivateKeyword));

            var nameSpaceFieldDeclaration = SyntaxFactory.FieldDeclaration(nameSpaceVariable)
                .AddModifiers(SyntaxFactory.Token(SyntaxKind.PrivateKeyword));

            var helperFieldDeclaration = SyntaxFactory.FieldDeclaration(helperVariable)
                .AddModifiers(SyntaxFactory.Token(SyntaxKind.PrivateKeyword));


            classDeclaration = classDeclaration
                .AddMembers(harrConnectionFieldDeclaration, nameSpaceFieldDeclaration, helperFieldDeclaration)
                .AddMembers(CreateConstructor(className))
                .AddMembers(CreateMethods());



            var @namespace = SyntaxFactory.NamespaceDeclaration(SyntaxFactory.ParseName(namespaceName)).NormalizeWhitespace();
            @namespace = @namespace.AddUsings(SyntaxFactory.UsingDirective(SyntaxFactory.ParseName("System")));
            @namespace = @namespace.AddMembers(classDeclaration);

            // Normalize and get code as string.
            var code = @namespace
                .NormalizeWhitespace()
                .ToFullString();

            var syntaxtree = CSharpSyntaxTree.ParseText(code);

            var asy = System.Linq.AsyncEnumerable.Range(0, 1);

            var assemblies = AppDomain
                .CurrentDomain
                .GetAssemblies()
                .Where(a => !string.IsNullOrEmpty(a.Location))
                .Select(a => MetadataReference.CreateFromFile(a.Location))
                .Cast<MetadataReference>()
                .ToArray();



            var compilation = CSharpCompilation.Create(namespaceName).AddSyntaxTrees(syntaxtree)
                .AddReferences(assemblies)
                .WithOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));


            using (var ms = new MemoryStream()) {
                var result = compilation.Emit(ms);

                if (result.Success) {
                    ms.Seek(0, SeekOrigin.Begin);
                    Assembly assembly = Assembly.Load(ms.ToArray());

                    var newTypeFullName = $"{namespaceName}.{className}";

                    var type = assembly.GetType(newTypeFullName);

                    return type;
                } else {
                    IEnumerable<Diagnostic> failures = result.Diagnostics.Where(diagnostic =>
                        diagnostic.IsWarningAsError ||
                        diagnostic.Severity == DiagnosticSeverity.Error);

                    foreach (Diagnostic diagnostic in failures) {
                        Console.Error.WriteLine("{0}: {1}", diagnostic.Id, diagnostic.GetMessage());
                    }

                    return null;
                }
            }



        }


        /// <summary>
        /// Generates the type syntax.
        /// </summary>
        private static TypeSyntax AsTypeSyntax(Type type) {
            string name = $"{type.Namespace}.{type.Name.Replace('+', '.')}";

            if (type.IsGenericType) {
                // Get the C# representation of the generic type minus its type arguments.
                name = name.Substring(0, name.IndexOf("`"));

                // Generate the name of the generic type.
                var genericArgs = type.GetGenericArguments();
                return SyntaxFactory.GenericName(SyntaxFactory.Identifier(name),
                    SyntaxFactory.TypeArgumentList(SyntaxFactory.SeparatedList(genericArgs.Select(AsTypeSyntax)))
                );
            } else
                return SyntaxFactory.ParseTypeName(name);
        }




        public static Type CreateTypeFromInterface<T>() {
            return new ClassCreator(typeof(T)).Build();
        }

        public static T CreateInstanceFromInterface<T>(params object[] args) {

            var t = CreateTypeFromInterface<T>();

            var nargs = args.ToList();
            nargs.Add(new ClassCreatorHelper());

            var instance = (T)Activator.CreateInstance(t, nargs.ToArray());
            return instance;
        }
    }

    public class ClassCreatorHelper {

        public ChannelReader<T> ToChannelReader<T>(IAsyncEnumerable<T> asyncEnumerable, CancellationToken token = default) {
            
            var output = Channel.CreateUnbounded<T>(new UnboundedChannelOptions { SingleWriter = true });
            var writer = output.Writer;
            Task.Run(async () => {
                await foreach (var x1 in asyncEnumerable) {
                    if (!writer.TryWrite(x1)) {
                        await writer.WriteAsync(x1, token);
                    }
                }

                writer.TryComplete();
            });
            return output.Reader;
        }
    }
}
