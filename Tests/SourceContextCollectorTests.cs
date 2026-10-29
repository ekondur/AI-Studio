using AI_Studio.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

// Executable regression tests: dotnet run --project Tests/SourceContextCollector.Tests.csproj
internal static class SourceContextCollectorTests
{
    private static async Task Main()
    {
        await Check("Cross-project calls, overloads, transitive calls and cycles", CrossProject);
        await Check("Interface implementations and virtual overrides", Dispatch);
        await Check("Constructors, properties, fields, extension and local methods", Members);
        await Check("Partial methods", PartialMethods);
        await Check("Unsaved solution snapshot and source line numbers", Snapshot);
        await Check("Multiple selected members", MultipleMembers);
        await Check("Missing source and unresolved calls", MissingSource);
        await Check("Depth, declaration and character limits", Limits);
        await Check("Cancellation and collection timeout", Cancellation);
        await Check("Invalid and oversized selection", InvalidSelection);
        await Check("Security: cross-project callers, upstream guards and downstream dependencies", SecurityCallers);
        await Check("Security: interface and base-contract callers", SecurityDispatch);
        await Check("Security: partial, containing and base type attributes", SecurityAttributes);
        await Check("Security: caller depth, declaration and annotation limits", SecurityLimits);
        await Check("Security: recursive callers and performance isolation", SecurityCycles);
        await Check("Security: cancellation and timeout", SecurityCancellation);
        Console.WriteLine("All 16 source context regression tests passed.");
    }

    private static async Task Check(string name, Func<Task> test)
    {
        await test();
        Console.WriteLine("PASS " + name);
    }

    private static Project AddProject(AdhocWorkspace workspace, string name)
    {
        var project = workspace.AddProject(name, LanguageNames.CSharp);
        return project.WithCompilationOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
            .AddMetadataReference(MetadataReference.CreateFromFile(typeof(object).Assembly.Location));
    }

    private static Document AddDocument(Project project, string name, string code) =>
        project.AddDocument(name, SourceText.From(code), filePath: "C:/Fixture/" + name);

    private static async Task<string> Analyze(Document document, string method = "Run", SourceContextCollector collector = null)
    {
        var root = await document.GetSyntaxRootAsync();
        var node = root.DescendantNodes().OfType<MethodDeclarationSyntax>().First(n => n.Identifier.Text == method);
        // Select only the method body to verify surrounding declaration context is collected.
        return await (collector ?? new SourceContextCollector()).CollectAsync(document,
            node.Body?.Span ?? node.ExpressionBody.Span, CancellationToken.None);
    }

    private static async Task CrossProject()
    {
        using var workspace = new AdhocWorkspace();
        var library = AddDocument(AddProject(workspace, "Library"), "Library.cs", @"
public static class Worker {
    public static int Work(int n) => Next(n); // INT_OVERLOAD
    public static int Work(string n) => 999; // WRONG_OVERLOAD
    public static int Next(int n) => n > 0 ? Work(n - 1) : 0; // TRANSITIVE_CYCLE
}");
        workspace.TryApplyChanges(library.Project.Solution);
        var app = AddProject(workspace, "App").AddProjectReference(new ProjectReference(library.Project.Id));
        var document = AddDocument(app, "App.cs", "public class Entry { public int Run() => Worker.Work(2); }");
        var result = await Analyze(document);
        Has(result, "Work(int n)");
        Has(result, "Next(int n)");
        Lacks(result, "Work(string n)");
        Require(result.Split(new[] { "public static int Next" }, StringSplitOptions.None).Length == 2, "Cycle duplicated source.");
        Has(result, "Library/C:/Fixture/Library.cs:3");
    }

    private static async Task Dispatch()
    {
        using var workspace = new AdhocWorkspace();
        var doc = AddDocument(AddProject(workspace, "Dispatch"), "Dispatch.cs", @"
public interface IRepo { int Read(); }
public class Repo : IRepo { public int Read() => Fetch(); private int Fetch() => 42; }
public class Parent { public virtual int Value() => 1; }
public class Child : Parent { public override int Value() => 2; }
public class Entry { public int Run(IRepo repo, Parent parent) => repo.Read() + parent.Value(); }");
        var result = await Analyze(doc);
        Has(result, "Possible interface implementation");
        Has(result, "Fetch() => 42");
        Has(result, "Possible override");
        Has(result, "override int Value() => 2");
        var root = await doc.GetSyntaxRootAsync();
        var selected = root.DescendantNodes().OfType<MethodDeclarationSyntax>().First(n => n.Identifier.Text == "Read");
        var declarationResult = await new SourceContextCollector().CollectAsync(doc, selected.Span, CancellationToken.None);
        Has(declarationResult, "Fetch() => 42");
    }

    private static async Task Members()
    {
        using var workspace = new AdhocWorkspace();
        var doc = AddDocument(AddProject(workspace, "Members"), "Members.cs", @"
public class Box {
    private int size = Seed();
    private static int Seed() => 7;
    public Box(int n) { size += n; }
    public int Value => size;
}
public static class Extensions { public static int Twice(this int n) => n * 2; }
public class Entry { public int Run() { int Local(int n) => n.Twice(); return Local(new Box(2).Value); } }");
        var result = await Analyze(doc);
        Has(result, "public Box(int n)");
        Has(result, "Value => size");
        Has(result, "int size = Seed()");
        Has(result, "Seed() => 7");
        Has(result, "Twice(this int n)");

        var construction = doc.WithText(SourceText.From(@"
class Parent { public Parent() { Initialize(); } void Initialize() { } }
class Child : Parent { int[] buffer = Allocate(); static int[] Allocate() => new int[1000]; }
class Entry { public object Run() => new Child(); }
"));
        var initialization = await Analyze(construction);
        Has(initialization, "buffer = Allocate()");
        Has(initialization, "Allocate() => new int[1000]");
        Has(initialization, "Implicit base constructor");
        Has(initialization, "void Initialize()");
    }

    private static async Task PartialMethods()
    {
        using var workspace = new AdhocWorkspace();
        var doc = AddDocument(AddProject(workspace, "Partial"), "First.cs", "public partial class Entry { partial void Work(); public void Run() { Work(); } }");
        var other = AddDocument(doc.Project, "Second.cs", "public partial class Entry { partial void Work() { Slow(); } void Slow() { } }");
        var result = await Analyze(other.Project.GetDocument(doc.Id));
        Has(result, "Slow()");
        Has(result, "Second.cs");
    }

    private static async Task Snapshot()
    {
        using var workspace = new AdhocWorkspace();
        var doc = AddDocument(AddProject(workspace, "Snapshot"), "Snapshot.cs", "class Entry { public int Run() => 1; }");
        var changed = doc.WithText(SourceText.From("class Entry {\n public int Run() => Fresh();\n int Fresh() => 17;\n}"));
        var result = await Analyze(changed);
        Has(result, "Snapshot.cs:2");
        Has(result, "3: int Fresh() => 17;");
        Lacks(result, "Run() => 1");
    }

    private static async Task MultipleMembers()
    {
        using var workspace = new AdhocWorkspace();
        var doc = AddDocument(AddProject(workspace, "Multiple"), "Multiple.cs", "class Entry { int First() => Shared(); int Second() => Shared(); int Shared() => 3; }");
        var root = await doc.GetSyntaxRootAsync();
        var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>().ToArray();
        var result = await new SourceContextCollector().CollectAsync(doc,
            TextSpan.FromBounds(methods[0].SpanStart, methods[1].Span.End), CancellationToken.None);
        Has(result, "First()"); Has(result, "Second()"); Has(result, "Shared() => 3");
    }

    private static async Task MissingSource()
    {
        using var workspace = new AdhocWorkspace();
        var doc = AddDocument(AddProject(workspace, "Missing"), "Missing.cs", "class Entry { public int Run() { Missing.Call(); return System.Math.Abs(-1); } }");
        var result = await Analyze(doc);
        Has(result, "Compilation errors");
        Has(result, "Unresolved/dynamic call");
        Has(result, "System.Math.Abs");
    }

    private static async Task Limits()
    {
        using var workspace = new AdhocWorkspace();
        var doc = AddDocument(AddProject(workspace, "Limits"), "Limits.cs", "class Entry { int Run() => A(); int A() => B(); int B() => 3; }");
        var depth = await Analyze(doc, collector: new SourceContextCollector(maxDepth: 1));
        Has(depth, "depth limit"); Lacks(depth, "int B() => 3");
        var count = await Analyze(doc, collector: new SourceContextCollector(maxDeclarations: 1));
        Has(count, "Declaration limit"); Lacks(count, "int A() => B()");
        var large = doc.WithText(SourceText.From("class Entry { int Run() => Big(); int Big() { /*" + new string('x', 4000) + "*/ return 1; } }"));
        var limited = await Analyze(large, collector: new SourceContextCollector(maxCharacters: 1000));
        Has(limited, "context size limit");
        Require(limited.Length < 3000, "Character budget did not bound the context.");
    }

    private static async Task Cancellation()
    {
        using var workspace = new AdhocWorkspace();
        var doc = AddDocument(AddProject(workspace, "Cancel"), "Cancel.cs", "class Entry { int Run() => 1; }");
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Throws<OperationCanceledException>(() => new SourceContextCollector().CollectAsync(doc, new TextSpan(14, 15), cts.Token));
        var timeout = await Analyze(doc, collector: new SourceContextCollector(timeout: TimeSpan.Zero));
        Has(timeout, "time limit");
    }

    private static async Task InvalidSelection()
    {
        using var workspace = new AdhocWorkspace();
        var doc = AddDocument(AddProject(workspace, "Invalid"), "Invalid.cs", "class Entry { int Run() => 1; }");
        await Throws<InvalidOperationException>(() => new SourceContextCollector().CollectAsync(doc, new TextSpan(999, 1), CancellationToken.None));
        await Throws<InvalidOperationException>(() => new SourceContextCollector().CollectAsync(doc, new TextSpan(0, 0), CancellationToken.None));
        await Throws<InvalidOperationException>(() => new SourceContextCollector(maxCharacters: 10).CollectAsync(doc, new TextSpan(0, 20), CancellationToken.None));
    }

    private static async Task SecurityCallers()
    {
        using var workspace = new AdhocWorkspace();
        var library = AddDocument(AddProject(workspace, "Library"), "Service.cs", @"
public class Service {
    public string Run(string input) => Store(input);
    public string Run(int input) => input.ToString();
    private string Store(string input) => input;
}");
        workspace.TryApplyChanges(library.Project.Solution);
        var app = AddProject(workspace, "Web").AddProjectReference(new ProjectReference(library.Project.Id));
        var endpoint = AddDocument(app, "Endpoint.cs", @"
public class Endpoint {
    public string Handle(string input) { if (!Allowed(input)) return null; return new Service().Run(input); }
    private bool Allowed(string input) => input != null;
    public string Unrelated() => new Service().Run(123);
    public string Request(string input) => Handle(input);
    public string BeyondLimit(string input) => Request(input);
}");
        var current = endpoint.Project.Solution.GetDocument(library.Id);
        var result = await Analyze(current, collector: new SourceContextCollector(securityAnalysis: true));
        Has(result, "Static security analysis context");
        Has(result, "Caller of Service.Run(string)");
        Has(result, "if (!Allowed(input)) return null");
        Has(result, "private bool Allowed");
        Has(result, "private string Store");
        Has(result, "public string Request");
        Has(result, "Web/C:/Fixture/Endpoint.cs:3");
        Lacks(result, "public string Unrelated");
        Lacks(result, "public string BeyondLimit");
        Has(result, "Caller depth limit");
        Has(result, "not complete taint/data-flow analysis");
    }

    private static async Task SecurityDispatch()
    {
        using var workspace = new AdhocWorkspace();
        var doc = AddDocument(AddProject(workspace, "Contracts"), "Contracts.cs", @"
public interface IService { string Run(string value); }
public class Service : IService { public string Run(string value) => value; }
public class Endpoint { public string Handle(IService service, string value) => service.Run(value); }
");
        // Select the concrete implementation, not the contract.
        var root = await doc.GetSyntaxRootAsync();
        var implementation = root.DescendantNodes().OfType<MethodDeclarationSyntax>().Single(n => n.Identifier.Text == "Run" && n.ExpressionBody != null);
        var result = await new SourceContextCollector(securityAnalysis: true).CollectAsync(doc, implementation.Span, CancellationToken.None);
        Has(result, "public string Handle");
        Has(result, "Possible caller via contract/dispatch");

        var derived = doc.WithText(SourceText.From(@"
public class Parent { public virtual string Run(string value) => value; }
public class Child : Parent { public override string Run(string value) => value; }
public class Endpoint { public string Handle(Parent service, string value) => service.Run(value); }
"));
        root = await derived.GetSyntaxRootAsync();
        implementation = root.DescendantNodes().OfType<MethodDeclarationSyntax>().Single(n => n.Modifiers.Any(SyntaxKind.OverrideKeyword));
        result = await new SourceContextCollector(securityAnalysis: true).CollectAsync(derived, implementation.Span, CancellationToken.None);
        Has(result, "public string Handle");
        Has(result, "Possible caller via contract/dispatch");
    }

    private static async Task SecurityAttributes()
    {
        using var workspace = new AdhocWorkspace();
        var doc = AddDocument(AddProject(workspace, "Attributes"), "Controller.cs", @"
namespace Policies { public class GateAttribute : System.Attribute { public GateAttribute(string policy) {} } }
[Policies.Gate(""base-policy"")] public class Parent {}
[Policies.Gate(""outer-policy"")] public partial class Outer {
    public partial class Controller : Parent { [Policies.Gate(""method-policy"")] public string Run(string input) => input; }
}");
        var other = AddDocument(doc.Project, "Controller.Policy.cs", @"
public partial class Outer {
    [Policies.Gate(""partial-policy"")] public partial class Controller {}
}");
        var result = await Analyze(other.Project.GetDocument(doc.Id), collector: new SourceContextCollector(securityAnalysis: true));
        Has(result, "Policies.GateAttribute(\"method-policy\")");
        Has(result, "Policies.GateAttribute(\"partial-policy\")");
        Has(result, "Policies.GateAttribute(\"outer-policy\")");
        Has(result, "Policies.GateAttribute(\"base-policy\")");
        Has(result, "Controller.Policy.cs:3");
        Has(result, "inheritance and enforcement depend on the framework");
        Has(result, "Attribute presence alone does not prove");
    }

    private static async Task SecurityLimits()
    {
        using var workspace = new AdhocWorkspace();
        var doc = AddDocument(AddProject(workspace, "Limits"), "Limit.cs", @"
class Entry {
    public int Run() => 1;
    public int Parent() => Run();
    public int Grandparent() => Parent();
    public int Other() => Run();
    public int More() => Run();
}");
        var depth = await Analyze(doc, collector: new SourceContextCollector(securityAnalysis: true, maxCallerDepth: 1));
        Has(depth, "Caller depth limit");
        Lacks(depth, "public int Grandparent");
        var count = await Analyze(doc, collector: new SourceContextCollector(securityAnalysis: true, maxDeclarations: 6));
        Has(count, "Caller declaration limit");

        var attributes = doc.WithText(SourceText.From("class TagAttribute : System.Attribute { public TagAttribute(string s) {} }\n[Tag(\""
            + new string('x', 4000) + "\")] class Entry { public int Run() => 1; }"));
        var bounded = await Analyze(attributes, collector: new SourceContextCollector(securityAnalysis: true, maxCharacters: 1000));
        Has(bounded, "attribute");
        Has(bounded, "truncated");
        Require(bounded.Length < 4000, "Security annotation budget exceeded.");
    }

    private static async Task SecurityCycles()
    {
        using var workspace = new AdhocWorkspace();
        var doc = AddDocument(AddProject(workspace, "Cycles"), "Cycle.cs", @"
class Entry { int Run(int n) => n; int Caller(int n) => n > 0 ? Run(n) : Again(n); int Again(int n) => Caller(n); }");
        var security = await Analyze(doc, collector: new SourceContextCollector(securityAnalysis: true));
        Require(security.Split(new[] { "int Caller(int n)" }, StringSplitOptions.None).Length == 2, "Recursive caller duplicated source.");
        var performance = await Analyze(doc);
        Lacks(performance, "int Caller(int n)");
        Lacks(performance, "Caller search:");
        Has(performance, "Static performance analysis context");
    }

    private static async Task SecurityCancellation()
    {
        using var workspace = new AdhocWorkspace();
        var doc = AddDocument(AddProject(workspace, "Cancel"), "Cancel.cs", "class Entry { int Run() => 1; int Caller() => Run(); }");
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Throws<OperationCanceledException>(() => new SourceContextCollector(securityAnalysis: true)
            .CollectAsync(doc, new TextSpan(14, 15), cts.Token));
        var result = await Analyze(doc, collector: new SourceContextCollector(securityAnalysis: true, timeout: TimeSpan.Zero));
        Has(result, "time limit");
    }

    private static void Has(string value, string expected) => Require(value.Contains(expected), "Missing: " + expected + "\n" + value);
    private static void Lacks(string value, string unexpected) => Require(!value.Contains(unexpected), "Unexpected: " + unexpected + "\n" + value);
    private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static async Task Throws<T>(Func<Task> action) where T : Exception
    {
        try { await action(); } catch (T) { return; }
        throw new Exception("Expected " + typeof(T).Name);
    }
}
