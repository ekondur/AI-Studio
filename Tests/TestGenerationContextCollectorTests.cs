using AI_Studio.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

internal static class TestGenerationContextCollectorTests
{
    private const string Frameworks = @"
namespace Xunit {
 public class FactAttribute : System.Attribute { }
 public class TheoryAttribute : FactAttribute { }
 public interface IClassFixture<T> { }
 public interface ICollectionFixture<T> { }
 public class CollectionAttribute : System.Attribute { public CollectionAttribute(string name) { } }
 public class CollectionDefinitionAttribute : System.Attribute { public CollectionDefinitionAttribute(string name) { } }
}
namespace NUnit.Framework { public class TestAttribute : System.Attribute { } public class SetUpAttribute : System.Attribute { } }
namespace Microsoft.VisualStudio.TestTools.UnitTesting { public class TestMethodAttribute : System.Attribute { } }
";

    internal static async Task RunAsync()
    {
        await Check("Test context: cross-project base chain, partial setup and helpers", BaseInfrastructure);
        await Check("Test context: fixtures and string-based collection fixtures", Fixtures);
        await Check("Test context: semantic ranking, namespaces and unrelated projects", Ranking);
        await Check("Test context: preferred project and framework alternatives", PreferredProject);
        await Check("Test context: unsaved source, no matches and configured fallback", SnapshotAndFallback);
        await Check("Test context: inherited tests and colocated test helpers", ColocatedTests);
        await Check("Test context: limits, cancellation and deadline", Limits);
        Console.WriteLine("All 7 test-generation context regression groups passed.");
    }

    private static async Task Check(string name, Func<Task> test)
    {
        await test();
        Console.WriteLine("PASS " + name);
    }

    private static Project Project(AdhocWorkspace workspace, string name, params ProjectId[] references)
    {
        var project = workspace.AddProject(name, LanguageNames.CSharp)
            .WithCompilationOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
            .AddMetadataReference(MetadataReference.CreateFromFile(typeof(object).Assembly.Location));
        foreach (var reference in references) project = project.AddProjectReference(new ProjectReference(reference));
        return project;
    }

    private static Document Add(Project project, string name, string code) =>
        project.AddDocument(name, SourceText.From(code), filePath: "C:/Tests/" + project.Name + "/" + name);

    private static Document Production(AdhocWorkspace workspace)
    {
        var document = Add(Project(workspace, "App"), "Service.cs",
            "namespace App { public class Service { public int Run(int value) => value + 1; public int Run(string value) => 0; } }");
        Require(workspace.TryApplyChanges(document.Project.Solution), "Unable to apply fixture.");
        return workspace.CurrentSolution.GetDocument(document.Id);
    }

    private static async Task<string> Collect(Document production, TestGenerationContextCollector collector = null,
        string preferred = null, CancellationToken token = default)
    {
        var root = await production.GetSyntaxRootAsync();
        var method = root.DescendantNodes().OfType<MethodDeclarationSyntax>().First();
        return await (collector ?? new TestGenerationContextCollector()).CollectAsync(production,
            method.ExpressionBody?.Span ?? method.Body.Span, preferred, token);
    }

    private static async Task BaseInfrastructure()
    {
        using var workspace = new AdhocWorkspace();
        var production = Production(workspace);
        var common = Add(Project(workspace, "TestSupport", production.Project.Id), "Shared.cs", @"
public abstract class RootBase { protected int SharedConfiguration = 7; }
public abstract partial class ServiceTestBase<T> : RootBase {
 protected App.Service CreateService() => new App.Service();
 protected Builder Builder = new Builder();
 private string PrivateSetting = ""not accessible to derived tests"";
}
public class Builder { public int Build() => Seed.Value(); }
public static class Seed { public static int Value() => 41; }");
        common = Add(common.Project, "Setup.cs", "public abstract partial class ServiceTestBase<T> { protected ServiceTestBase() { SharedConfiguration = 12; } }");
        Require(workspace.TryApplyChanges(common.Project.Solution), "Unable to apply support fixture.");
        var tests = Add(Project(workspace, "App.Tests", production.Project.Id, common.Project.Id), "Framework.cs", Frameworks);
        tests = Add(tests.Project, "ServiceTests.cs", @"
using Xunit;
namespace App.Tests {
 public class ServiceTests : ServiceTestBase<int> {
  [Fact] public void Existing() { var result = CreateService().Run(Builder.Build()); }
 }
}");
        var output = await Collect(tests.Project.Solution.GetDocument(production.Id));
        Has(output, "references the selected method");
        Has(output, "ServiceTestBase<int>");
        Has(output, "SharedConfiguration = 7");
        Has(output, "SharedConfiguration = 12");
        Has(output, "CreateService()");
        Has(output, "class Builder");
        Has(output, "Value() => 41");
        Has(output, "using Xunit;");
        Has(output, "TestSupport/C:/Tests/TestSupport/Setup.cs:1");
        Require(output.Split(new[] { "class RootBase" }, StringSplitOptions.None).Length == 2, "Base class duplicated.");
        var root = await production.GetSyntaxRootAsync();
        var span = root.DescendantNodes().OfType<MethodDeclarationSyntax>().First().Span;
        var context = await new SourceContextCollector(testGeneration: true).CollectAsync(production, span, CancellationToken.None);
        Has(context, "Production source context for test generation");
        Lacks(context, "Static performance analysis context");
    }

    private static async Task Fixtures()
    {
        using var workspace = new AdhocWorkspace();
        var production = Production(workspace);
        var tests = Add(Project(workspace, "App.Tests", production.Project.Id), "Framework.cs", Frameworks);
        tests = Add(tests.Project, "ServiceTests.cs", @"
[Xunit.Collection(""Database"")]
public class ServiceTests : Xunit.IClassFixture<LocalFixture> {
 public ServiceTests(LocalFixture fixture) { }
 [Xunit.Fact] public void Existing() { new App.Service().Run(1); }
}
public class LocalFixture { public int LocalSetup = 11; }
[Xunit.CollectionDefinition(""Database"")]
public class DatabaseCollection : Xunit.ICollectionFixture<DatabaseFixture> { }
public class DatabaseFixture { public string ConnectionSetup = ""fixture source""; }");
        var output = await Collect(tests.Project.Solution.GetDocument(production.Id));
        Has(output, "LocalSetup = 11");
        Has(output, "xUnit collection definition: Database");
        Has(output, "ConnectionSetup");
        Has(output, "No test or setup code was executed");
    }

    private static async Task Ranking()
    {
        using var workspace = new AdhocWorkspace();
        var production = Production(workspace);
        var unrelated = Add(Project(workspace, "Unrelated.Tests"), "Framework.cs", Frameworks);
        unrelated = Add(unrelated.Project, "ServiceTests.cs", "public class ServiceTests { [Xunit.Fact] public void UnrelatedProject() { } }");
        Require(workspace.TryApplyChanges(unrelated.Project.Solution), "Unable to apply unrelated fixture.");
        var tests = Add(Project(workspace, "App.Tests", production.Project.Id), "Framework.cs", Frameworks);
        tests = Add(tests.Project, "References.cs", @"
namespace Decoy { public class Service { public int Run(int n) => n; } }
public class DifferentNameTests { [Xunit.Fact] public void Exact() { new App.Service().Run(1); } }
public class ServiceNameOnlyTests { [Xunit.Fact] public void DecoyCall() { new Decoy.Service().Run(1); } }
public class OtherOverloadTests { [Xunit.Fact] public void Other() { new App.Service().Run(""a""); } }
public class UnrelatedTests { [Xunit.Fact] public void UnrelatedExample() { } }
public class FakeFactAttribute : System.Attribute { }
public class ServiceFakeTests { [FakeFact] public void NotATest() { new App.Service().Run(1); } }
");
        var output = await Collect(tests.Project.Solution.GetDocument(production.Id));
        Has(output, "Reference candidate: DifferentNameTests");
        Has(output, "references the selected method");
        Has(output, "OtherOverloadTests");
        Lacks(output, "ServiceNameOnlyTests");
        Lacks(output, "UnrelatedProject");
        Lacks(output, "UnrelatedExample");
        Lacks(output, "NotATest");
        Require(output.IndexOf("Reference candidate: DifferentNameTests", StringComparison.Ordinal)
            < output.IndexOf("Reference candidate: OtherOverloadTests", StringComparison.Ordinal), "Method reference did not rank above other overload.");
    }

    private static async Task PreferredProject()
    {
        using var workspace = new AdhocWorkspace();
        var production = Production(workspace);
        var unit = Add(Project(workspace, "App.UnitTests", production.Project.Id), "Framework.cs", Frameworks);
        unit = Add(unit.Project, "ServiceTests.cs", "class ServiceTests { [NUnit.Framework.Test] public void UnitTest() { new App.Service().Run(1); } }");
        Require(workspace.TryApplyChanges(unit.Project.Solution), "Unable to apply unit fixture.");
        var integration = Add(Project(workspace, "App.IntegrationTests", production.Project.Id), "Framework.cs", Frameworks);
        integration = Add(integration.Project, "ServiceTests.cs", "class ServiceTests { [Microsoft.VisualStudio.TestTools.UnitTesting.TestMethod] public void DatabaseTest() { new App.Service().Run(1); } }");
        var current = integration.Project.Solution.GetDocument(production.Id);
        var both = await Collect(current);
        Has(both, "Multiple reference classes");
        Has(both, "NUnit"); Has(both, "MSTest");
        var preferred = await Collect(current, preferred: "app.unittests");
        Has(preferred, "UnitTest()"); Lacks(preferred, "DatabaseTest()");
        var missing = await Collect(current, preferred: "Missing.Tests");
        Has(missing, "Preferred test project was not found"); Lacks(missing, "DatabaseTest()");
    }

    private static async Task SnapshotAndFallback()
    {
        using var workspace = new AdhocWorkspace();
        var production = Production(workspace);
        var missing = await Collect(production);
        Has(missing, "No reference test class was selected");
        var tests = Add(Project(workspace, "App.Tests", production.Project.Id), "Framework.cs", Frameworks);
        tests = Add(tests.Project, "ConventionTests.cs", "class ConventionTests { [Xunit.Fact] public void Old() { } }");
        var snapshot = tests.Project.Solution.WithDocumentText(tests.Id,
            SourceText.From("class ConventionTests { [Xunit.Fact] public void Unsaved() { } }"));
        var output = await Collect(snapshot.GetDocument(production.Id));
        Has(output, "project convention example only");
        Has(output, "void Unsaved()"); Lacks(output, "void Old()");
    }

    private static async Task ColocatedTests()
    {
        using var workspace = new AdhocWorkspace();
        var production = Production(workspace);
        var tests = Add(production.Project, "Framework.cs", Frameworks);
        tests = Add(tests.Project, "ServiceTests.cs", @"
public abstract class TestBase {
 protected Helper Helper = new Helper();
 [Xunit.Fact] public void InheritedTest() { new App.Service().Run(Helper.Value()); }
}
public class ServiceTests : TestBase { }
public class Helper { public int Value() => 55; }");
        var output = await Collect(tests.Project.Solution.GetDocument(production.Id));
        Has(output, "Reference candidate: ServiceTests");
        Has(output, "name match only; verify suitability");
        Has(output, "InheritedTest()");
        Has(output, "Value() => 55");
        Lacks(output, "public class Service {");
    }

    private static async Task Limits()
    {
        using var workspace = new AdhocWorkspace();
        var production = Production(workspace);
        var tests = Add(Project(workspace, "App.Tests", production.Project.Id), "Framework.cs", Frameworks);
        tests = Add(tests.Project, "ServiceTests.cs", @"
class ServiceTests : Base1 { [Xunit.Fact] public void Existing() { new App.Service().Run(1); } }
class Base1 : Base2 { } class Base2 : Base3 { } class Base3 : Base4 { } class Base4 : Base5 { } class Base5 { }");
        var current = tests.Project.Solution.GetDocument(production.Id);
        var count = await Collect(current, new TestGenerationContextCollector(maxTypes: 1));
        Has(count, "type or character limit"); Lacks(count, "class Base1 :");
        var characters = await Collect(current, new TestGenerationContextCollector(maxCharacters: 30));
        Has(characters, "character limit"); Lacks(characters, "```csharp");
        var documents = await Collect(current, new TestGenerationContextCollector(maxDocuments: 0));
        Has(documents, "Document search limit");
        var depth = await Collect(current);
        Has(depth, "depth limit"); Lacks(depth, "class Base5 {");
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        try { await Collect(current, token: cancelled.Token); throw new Exception("Cancellation was swallowed."); }
        catch (OperationCanceledException) { }
        var timedOut = await Collect(current, new TestGenerationContextCollector(timeout: TimeSpan.FromTicks(1)));
        Has(timedOut, "time limit");
    }

    private static void Has(string value, string expected) => Require(value.Contains(expected), "Missing: " + expected + "\n" + value);
    private static void Lacks(string value, string unexpected) => Require(!value.Contains(unexpected), "Unexpected: " + unexpected + "\n" + value);
    private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
}
