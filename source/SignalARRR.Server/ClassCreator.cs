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
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.DependencyInjection;
using Reflectensions.ExtensionMethods;

namespace SignalARRR.Server {
    internal class ClassCreator {
        private static ConcurrentDictionary<Type, Type> generatedTypes { get; } = new ConcurrentDictionary<Type, Type>();

        private Type FromType { get; }


        private ClassCreator(Type fromType) {
            FromType = fromType;
        }




        private MemberDeclarationSyntax CreateMethod(MethodInfo methodInfo) {

            var isVoid = methodInfo.ReturnType == typeof(void);
            var isGenericReturn = methodInfo.ReturnType.IsGenericParameter;

            var isTask = methodInfo.ReturnType == typeof(Task);
            var isTaskOfT = methodInfo.ReturnType.IsGenericTypeOf(typeof(Task<>));

            var genericArguments = methodInfo.GetGenericArguments().Select(arg => arg.Name);


            var methodParameters = methodInfo.GetParameters().Where(p => !p.GetCustomAttributes().Any(attr => attr.GetType().Name.Equals("FromServicesAttribute")));
            var returnType = isVoid ? SyntaxFactory.ParseTypeName("void") : AsTypeSyntax(methodInfo.ReturnType);

            var body = new StringBuilder();
            body.AppendLine($"var paramsArray = new object[] {{{String.Join(", ", methodParameters.Select(p => p.Name))}}};");
            body.AppendLine($"var methodName = String.IsNullOrWhiteSpace(_nameSpace) ? \"{methodInfo.Name}\" : $\"{{_nameSpace}}.{methodInfo.Name}\";");


            //var paramsArray = $"var paramsArray = new object[] {{{String.Join(", ", methodParameters.Where(p => p.ParameterType != typeof(CancellationToken)).Select(p => p.Name))}}};";
            var cancellationToken = methodParameters.Where(p => p.ParameterType == typeof(CancellationToken))
                .Select(p => p.Name).FirstOrDefault().ToNull();


            var gArrayBlock = "";
            var genArgumentsToTypesArray = "";
            if (genericArguments.Any()) {

                var gArrayBuilder = new StringBuilder();
                gArrayBuilder.AppendLine("var gargs = new List<string>();");

                foreach (var genericArgument in genericArguments) {
                    gArrayBuilder.AppendLine($"gargs.Add(typeof({genericArgument}).FullName);");
                }

                gArrayBlock = gArrayBuilder.ToString();
                genArgumentsToTypesArray = $", gargs.ToArray()";
            }

            var l = new List<string>();

            var insertCancellationToken = cancellationToken != null ? $", {cancellationToken}" : String.Empty;
            if (isVoid || isTask) {
                body.AppendLine(gArrayBlock);
                body.AppendLine($"var task = _helper.Send(_clientcontext, methodName, paramsArray{genArgumentsToTypesArray}{insertCancellationToken});");
                if (isTask) {
                    body.AppendLine("await task;");
                } else {
                    body.AppendLine("task.GetAwaiter().GetResult();");
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
                body.AppendLine(gArrayBlock);
                body.AppendLine($"var task = _helper.Invoke{invokeGenericArgument}(_clientcontext, methodName, paramsArray{genArgumentsToTypesArray}{insertCancellationToken});");
                if (isTaskOfT) {
                    body.AppendLine($"return await task;");
                } else {
                    body.AppendLine($"return task.GetAwaiter().GetResult();");
                }


            }

            var syntax = SyntaxFactory.ParseStatement(body.ToString());



            var syntaxParameters = methodParameters
                .Select(p => SyntaxFactory.Parameter(SyntaxFactory.Identifier(p.Name))
                .WithType(AsTypeSyntax(p.ParameterType)));

            var modifiers = (isTaskOfT || isTask) ? SyntaxKind.PublicKeyword | SyntaxKind.AsyncKeyword : SyntaxKind.PublicKeyword;


            var methodGenericArgument = "";
            if (genericArguments.Any()) {
                var genArgsSyntax = methodInfo.GetGenericArguments().ToList();
                var text = genArgsSyntax.Select(arg => {
                    //var syn = AsTypeSyntax(arg);
                    //    var synres = syn.;
                    return arg.Name;
                }).ToList();
                methodGenericArgument = $"<{String.Join(", ", genArgsSyntax)}>";
            }


            var na = $"{methodInfo.Name}{methodGenericArgument}";

            // Create a method
            var methodDeclaration = SyntaxFactory.MethodDeclaration(returnType, na)

                .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))

                .AddParameterListParameters(syntaxParameters.ToArray())
                .WithBody(SyntaxFactory.Block(syntax));

            if (isTaskOfT || isTask) {
                methodDeclaration = methodDeclaration.AddModifiers(SyntaxFactory.Token(SyntaxKind.AsyncKeyword));
            }

            return methodDeclaration;
        }

        public static ExpressionSyntax GenerateMethodIdentifier(string methodName, string targetIdentifierOrTypeName, Type[] typeArguments = null) {
            ExpressionSyntax methodIdentifier = SyntaxFactory.IdentifierName(targetIdentifierOrTypeName + "." + methodName);
            if (typeArguments != null) {
                methodIdentifier = SyntaxFactory.GenericName(SyntaxFactory.Identifier(targetIdentifierOrTypeName + "." + methodName),
                    SyntaxFactory.TypeArgumentList(SyntaxFactory.SeparatedList(typeArguments.Select(AsTypeSyntax))));
            }
            return methodIdentifier;
        }






        private MemberDeclarationSyntax[] CreateMethods() => FromType.GetMethods().Select(CreateMethod).ToArray();


        private MemberDeclarationSyntax CreateConstructor(string className) {

            var clientcontextParameter = SyntaxFactory.Parameter(SyntaxFactory.Identifier("clientcontext"))
                .WithType(AsTypeSyntax(typeof(ClientContext)));
            var nameSpaceParameter = SyntaxFactory.Parameter(SyntaxFactory.Identifier("nameSpace"))
                .WithType(AsTypeSyntax(typeof(string)));
            var helperParameter = SyntaxFactory.Parameter(SyntaxFactory.Identifier("helper"))
                .WithType(AsTypeSyntax(typeof(ClassCreatorHelper)));

            var body = "_clientcontext = clientcontext;_nameSpace = nameSpace;_helper = helper;";


            var constructor = SyntaxFactory.ConstructorDeclaration(className)
                .AddParameterListParameters(clientcontextParameter, nameSpaceParameter, helperParameter)
                .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
                .WithBody(SyntaxFactory.Block(SyntaxFactory.ParseStatement(body)));

            return constructor;
        }


        public Type Build() {
            return generatedTypes.GetOrAdd(FromType, type => _build());
        }

        public Type _build() {
            var className = $"{FromType.Name}_created";
            var namespaceName = $"GeneratedClientMethodTypes";



            var baseType = SyntaxFactory.SimpleBaseType(AsTypeSyntax(FromType));



            var classDeclaration = SyntaxFactory.ClassDeclaration(className)
                .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
                .AddBaseListTypes(baseType);

            var clientcontextVariable = SyntaxFactory.VariableDeclaration(AsTypeSyntax(typeof(ClientContext)))
                .AddVariables(SyntaxFactory.VariableDeclarator("_clientcontext"));
            var nameSpaceVariable = SyntaxFactory.VariableDeclaration(AsTypeSyntax(typeof(string)))
                .AddVariables(SyntaxFactory.VariableDeclarator("_nameSpace"));
            var helperVariable = SyntaxFactory.VariableDeclaration(AsTypeSyntax(typeof(ClassCreatorHelper)))
                .AddVariables(SyntaxFactory.VariableDeclarator("_helper"));

            var clientcontextFieldDeclaration = SyntaxFactory.FieldDeclaration(clientcontextVariable)
                .AddModifiers(SyntaxFactory.Token(SyntaxKind.PrivateKeyword));

            var nameSpaceFieldDeclaration = SyntaxFactory.FieldDeclaration(nameSpaceVariable)
                .AddModifiers(SyntaxFactory.Token(SyntaxKind.PrivateKeyword));

            var helperFieldDeclaration = SyntaxFactory.FieldDeclaration(helperVariable)
                .AddModifiers(SyntaxFactory.Token(SyntaxKind.PrivateKeyword));


            classDeclaration = classDeclaration
                .AddMembers(clientcontextFieldDeclaration, nameSpaceFieldDeclaration, helperFieldDeclaration)
                .AddMembers(CreateConstructor(className))
                .AddMembers(CreateMethods());




            var @namespace = SyntaxFactory.NamespaceDeclaration(SyntaxFactory.ParseName(namespaceName)).NormalizeWhitespace();
            @namespace = @namespace.AddUsings(
                SyntaxFactory.UsingDirective(SyntaxFactory.ParseName("System")),
                SyntaxFactory.UsingDirective(SyntaxFactory.ParseName("System.Collections.Generic"))
            );
            @namespace = @namespace.AddMembers(classDeclaration);


            var c = @namespace.ToFullString();
            // Normalize and get code as string.
            var code = @namespace
                .NormalizeWhitespace()
                .ToFullString();

            var syntaxtree = CSharpSyntaxTree.ParseText(code);

            var assemblies = AppDomain
                .CurrentDomain
                .GetAssemblies()
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


            if (type.IsGenericParameter) {
                return SyntaxFactory.ParseTypeName(type.Name);
            }

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
            return CreateTypeFromInterface(typeof(T));
        }

        public static Type CreateTypeFromInterface(Type type) {
            return new ClassCreator(type).Build();
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

        public Task<TResult> Invoke<TResult>(ClientContext clientContext, string method, object[] arguments, CancellationToken cancellationToken = default) {
            return Invoke<TResult>(clientContext, method, arguments, null, cancellationToken);
        }

        public async Task<TResult> Invoke<TResult>(ClientContext clientContext, string method, object[] arguments, string[] genericArguments, CancellationToken cancellationToken = default) {

            using var serviceProviderScope = clientContext.ServiceProvider.CreateScope();

            var hubContextType = typeof(ClientContextDispatcher<>).MakeGenericType(clientContext.HARRRType);
            var harrrContext = (IClientContextDispatcher)serviceProviderScope.ServiceProvider.GetRequiredService(hubContextType);
            var msg = new ServerRequestMessage(method, arguments);
            msg.GenericArguments = genericArguments;
            var res = await harrrContext.InvokeClientAsync<TResult>(clientContext.Id, msg, cancellationToken);
            return res;

        }

        public Task Send(ClientContext clientContext, string method, object[] arguments, CancellationToken cancellationToken = default) {
            return Send(clientContext, method, arguments, null, cancellationToken);
        }

        public async Task Send(ClientContext clientContext, string method, object[] arguments, string[] genericArguments, CancellationToken cancellationToken = default) {

            using var serviceProviderScope = clientContext.ServiceProvider.CreateScope();

            var hubContextType = typeof(ClientContextDispatcher<>).MakeGenericType(clientContext.HARRRType);
            var harrrContext = (IClientContextDispatcher)serviceProviderScope.ServiceProvider.GetRequiredService(hubContextType);
            var msg = new ServerRequestMessage(method, arguments);
            msg.GenericArguments = genericArguments;
            await harrrContext.SendClientAsync(clientContext.Id, msg, cancellationToken);
        }
    }
}
