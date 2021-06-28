using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using doob.Reflectensions.ExtensionMethods;
using doob.SignalARRR.CodeGenerator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using SignalARRR.CodeGenerator.ExtensionMethods;

namespace SignalARRR.CodeGenerator {
    public class ClassCreator {

        private static ConcurrentDictionary<Type, Type> generatedTypes { get; } = new ConcurrentDictionary<Type, Type>();

        private Type FromType { get; }

        private ClassCreator(Type fromType) {
            FromType = fromType;
        }


        private static MemberDeclarationSyntax CreateConstructor(string className) {


            var nameSpaceParameter = SyntaxFactory.Parameter(SyntaxFactory.Identifier("nameSpace"))
                .WithType(typeof(string).AsTypeSyntax());
            var helperParameter = SyntaxFactory.Parameter(SyntaxFactory.Identifier("helper"))
                .WithType(typeof(ClassCreatorHelper).AsTypeSyntax());

            var body = "_nameSpace = nameSpace;_helper = helper;";


            var constructor = SyntaxFactory.ConstructorDeclaration(className)
                .AddParameterListParameters(nameSpaceParameter, helperParameter)
                .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
                .WithBody(SyntaxFactory.Block(SyntaxFactory.ParseStatement(body)));

            return constructor;
        }
        private static MemberDeclarationSyntax CreateMethod(MethodInfo methodInfo) {

            var isVoid = methodInfo.ReturnType == typeof(void);
            var isTask = methodInfo.ReturnType == typeof(Task);
            var isTaskOfT = methodInfo.ReturnType.IsGenericTypeOf(typeof(Task<>));
            var methodParameters = methodInfo.GetParameters().WithoutAttribute("FromServicesAttribute").ToList();
            var cancellationToken = methodParameters.WithType<CancellationToken>().Select(p => $", {p.Name}").FirstOrDefault();
            var isStreamingMethod = methodInfo.ReturnType.IsStreamingType();
            var genericArguments = methodInfo.GetGenericArgumentsAsString();

            var body = new StringBuilder();
            body.AppendLine(methodParameters.ToParamsArrayText("paramsArray"));
            body.AppendLine($"var methodName = String.IsNullOrWhiteSpace(_nameSpace) ? \"{methodInfo.Name}\" : $\"{{_nameSpace}}|{methodInfo.Name}\";");
            body.AppendLine(genericArguments.ArrayBuilder);

            var invokeGenericArgument = (isTaskOfT || isStreamingMethod.IsStreamingType) ? methodInfo.ReturnType.GetGenericArguments().AsGenericArgumentsText() : methodInfo.ReturnType.AsGenericArgumentsText();

            if (isVoid) {
                body.AppendLine($"_helper.Send(methodName, paramsArray{genericArguments.Parameter}{cancellationToken});");
            } else if (isTask) {
                body.AppendLine($"await _helper.SendAsync(methodName, paramsArray{genericArguments.Parameter}{cancellationToken});");
            } else if (isStreamingMethod.IsStreamingType) {

                body.AppendLine($"var stream = _helper.StreamAsync{invokeGenericArgument}(methodName, paramsArray{genericArguments.Parameter}{cancellationToken});");

                switch (isStreamingMethod.StreamingType) {
                    case StreamingType.ChannelReader: {
                            body.AppendLine($"return _helper.ToChannelReader(stream{cancellationToken});");
                            break;
                        }
                    case StreamingType.AsyncEnumerable: {
                            body.AppendLine("return stream;");
                            break;
                        }
                    case StreamingType.Observable: {
                            body.AppendLine("return System.Linq.AsyncEnumerable.ToObservable(stream);");
                            break;
                        }
                }

            } else {

                if (isTaskOfT) {
                    body.AppendLine($"return await _helper.InvokeAsync{invokeGenericArgument}(methodName, paramsArray{genericArguments.Parameter}{cancellationToken});");
                } else {
                    body.AppendLine($"return _helper.Invoke{invokeGenericArgument}(methodName, paramsArray{genericArguments.Parameter}{cancellationToken});");
                }
            }


            var syntax = SyntaxFactory.ParseStatement(body.ToString());

            var returnTypeSyntax = methodInfo.ReturnType.AsTypeSyntax();

            var syntaxParameters = methodParameters.Select(p => SyntaxFactory.Parameter(SyntaxFactory.Identifier(p.Name)).WithType(p.ParameterType.AsTypeSyntax()));

            var na = $"{methodInfo.Name}{methodInfo.GetGenericArguments().AsGenericArgumentsText()}";
            // Create a method
            var methodDeclaration = SyntaxFactory.MethodDeclaration(returnTypeSyntax, na)
                .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
                .AddParameterListParameters(syntaxParameters.ToArray())
                .WithBody(SyntaxFactory.Block(syntax));

            if (isTaskOfT || isTask) {
                methodDeclaration = methodDeclaration.AddModifiers(SyntaxFactory.Token(SyntaxKind.AsyncKeyword));
            }

            return methodDeclaration;
        }

        public Type Build() {
            return generatedTypes.GetOrAdd(FromType, type => _build());
        }

        private Type _build() {
            var className = $"{FromType.Name}_created";
            var namespaceName = $"GeneratedClientMethodTypes";



            var baseType = SyntaxFactory.SimpleBaseType(FromType.AsTypeSyntax());



            var classDeclaration = SyntaxFactory.ClassDeclaration(className)
                .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
                .AddBaseListTypes(baseType);


            var nameSpaceVariable = SyntaxFactory.VariableDeclaration(typeof(string).AsTypeSyntax())
                .AddVariables(SyntaxFactory.VariableDeclarator("_nameSpace"));
            var helperVariable = SyntaxFactory.VariableDeclaration(typeof(ClassCreatorHelper).AsTypeSyntax())
                .AddVariables(SyntaxFactory.VariableDeclarator("_helper"));

            var nameSpaceFieldDeclaration = SyntaxFactory.FieldDeclaration(nameSpaceVariable)
                .AddModifiers(SyntaxFactory.Token(SyntaxKind.PrivateKeyword));

            var helperFieldDeclaration = SyntaxFactory.FieldDeclaration(helperVariable)
                .AddModifiers(SyntaxFactory.Token(SyntaxKind.PrivateKeyword));


            classDeclaration = classDeclaration
                .AddMembers(nameSpaceFieldDeclaration, helperFieldDeclaration)
                .AddMembers(CreateConstructor(className))
                .AddMembers(FromType.GetMethods().Select(CreateMethod).ToArray());




            var @namespace = SyntaxFactory.NamespaceDeclaration(SyntaxFactory.ParseName(namespaceName)).NormalizeWhitespace();
            @namespace = @namespace.AddUsings(
                SyntaxFactory.UsingDirective(SyntaxFactory.ParseName("System"))
            //SyntaxFactory.UsingDirective(SyntaxFactory.ParseName("System.Collections.Generic"))
            );
            @namespace = @namespace.AddMembers(classDeclaration);


            var c = @namespace.ToFullString();
            // Normalize and get code as string.
            var code = @namespace
                .NormalizeWhitespace()
                .ToFullString();

            var syntaxtree = CSharpSyntaxTree.ParseText(code);
            var asy = System.Linq.AsyncEnumerable.Range(0, 1);

            var nAssemblies = new List<Assembly>();
            nAssemblies.AddRange(AppDomain
                .CurrentDomain
                .GetAssemblies());
            var assFinder = new AssemblyFinder( typeof(Task<>), FromType, typeof(Task));
            //var neededAssemblies = assFinder.GetNeededAssemblies();
            ////neededAssemblies.Add(Assembly.GetAssembly(typeof(Task<>)));
            //neededAssemblies.AddRange(AppDomain
            //    .CurrentDomain
            //    .GetAssemblies());
            nAssemblies.AddRange(assFinder.GetNeededAssemblies());


            var assemblies = nAssemblies
                
                .Where(p => !p.IsDynamic)
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
                    var assembly = Assembly.Load(ms.ToArray());
                    return assembly.GetType($"{namespaceName}.{className}");
                } else {
                    var failures = result.Diagnostics.Where(diagnostic =>
                        diagnostic.IsWarningAsError ||
                        diagnostic.Severity == DiagnosticSeverity.Error).Select(d => new Exception(d.GetMessage()));

                    throw new AggregateException(failures);
                }
            }

        }

        public static Type CreateTypeFromInterface<T>() {
            return CreateTypeFromInterface(typeof(T));
        }

        public static Type CreateTypeFromInterface(Type type) {
            return new ClassCreator(type).Build();
        }

        public static T CreateInstanceFromInterface<T>(ClassCreatorHelper classCreatorHelper) {

            var t = CreateTypeFromInterface<T>();

            var nargs = new List<object>();

            nargs.Add(typeof(T).FullName);
            nargs.Add(classCreatorHelper);

            var instance = (T)Activator.CreateInstance(t, nargs.ToArray());
            return instance;
        }
    }

    public abstract class ClassCreatorHelper {


        public abstract void Send(string methodName, IEnumerable<object> arguments, string[] genericArguments, CancellationToken cancellationToken = default);
        public abstract Task SendAsync(string methodName, IEnumerable<object> arguments, string[] genericArguments, CancellationToken cancellationToken = default);

        public abstract T Invoke<T>(string methodName, IEnumerable<object> arguments, string[] genericArguments, CancellationToken cancellationToken = default);
        public abstract Task<T> InvokeAsync<T>(string methodName, IEnumerable<object> arguments, string[] genericArguments, CancellationToken cancellationToken = default);

        public abstract IAsyncEnumerable<TResult> StreamAsync<TResult>(string methodName, IEnumerable<object> arguments, string[] genericArguments, CancellationToken cancellationToken = default);

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
