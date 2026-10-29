using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AI_Studio.Helpers
{
    /// <summary>
    /// Collects source evidence from an immutable solution snapshot. No user code is executed.
    /// Breadth-first traversal favors nearby dependencies and is bounded for large solutions.
    /// </summary>
    internal sealed class SourceContextCollector
    {
        private readonly int _maxDepth;
        private readonly int _maxDeclarations;
        private readonly int _maxCharacters;
        private readonly TimeSpan _timeout;
        private readonly bool _securityAnalysis;
        private readonly int _maxCallerDepth;
        private readonly Queue<WorkItem> _pending = new Queue<WorkItem>();
        private readonly HashSet<string> _visited = new HashSet<string>();
        private readonly HashSet<ISymbol> _dispatchVisited = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
        private readonly HashSet<string> _limitations = new HashSet<string>();
        private readonly HashSet<string> _external = new HashSet<string>();
        private readonly StringBuilder _evidence = new StringBuilder();
        private int _declarations;
        private int _examinedReferences;

        internal SourceContextCollector(int maxDepth = 5, int maxDeclarations = 80,
            int maxCharacters = 80000, TimeSpan? timeout = null, bool securityAnalysis = false, int maxCallerDepth = 2)
        {
            _maxDepth = maxDepth;
            _maxDeclarations = maxDeclarations;
            _maxCharacters = maxCharacters;
            _timeout = timeout ?? TimeSpan.FromSeconds(30);
            _securityAnalysis = securityAnalysis;
            _maxCallerDepth = maxCallerDepth;
        }

        internal async Task<string> CollectAsync(Document document, TextSpan selection, CancellationToken cancellationToken)
        {
            if (document == null || document.Project.Language != LanguageNames.CSharp)
                throw new InvalidOperationException("Solution dependency analysis requires a C# solution document.");

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(_timeout);
            var token = timeout.Token;
            try
            {
                var root = await document.GetSyntaxRootAsync(token).ConfigureAwait(false);
                if (root == null || selection.End > root.FullSpan.End || selection.Length == 0)
                    throw new InvalidOperationException("Select C# code to analyze and try again.");
                if (selection.Length > _maxCharacters / 2)
                    throw new InvalidOperationException("The selection is too large. Select a smaller method or code block for analysis.");

                var selectedNode = root.FindNode(selection, getInnermostNodeForTie: true);
                var member = selectedNode.AncestorsAndSelf().OfType<MemberDeclarationSyntax>()
                    .FirstOrDefault(n => !(n is BaseTypeDeclarationSyntax) && !(n is BaseNamespaceDeclarationSyntax));
                if (member != null)
                    Enqueue(document, member, 0, "Selected member (includes surrounding code)");
                else
                {
                    // A selection may contain multiple members or top-level statements.
                    foreach (var node in root.DescendantNodes(n => n.FullSpan.IntersectsWith(selection))
                        .Where(n => (n is MemberDeclarationSyntax && !(n is BaseTypeDeclarationSyntax)
                            && !(n is BaseNamespaceDeclarationSyntax)) || n is GlobalStatementSyntax)
                        .Where(n => n.Span.IntersectsWith(selection)))
                    {
                        token.ThrowIfCancellationRequested();
                        Enqueue(document, node, 0, "Selected declaration");
                    }
                }
                if (_pending.Count == 0)
                    _limitations.Add("No executable declaration was found in the selection; only the selected text is available.");

                if (_securityAnalysis)
                    await CollectCallersAsync(_pending.ToArray(), token).ConfigureAwait(false);

                while (_pending.Count > 0)
                {
                    token.ThrowIfCancellationRequested();
                    if (_declarations >= _maxDeclarations || _evidence.Length >= _maxCharacters)
                    {
                        _limitations.Add("Declaration or context size limit reached; remaining dependencies were not inspected.");
                        break;
                    }
                    var item = _pending.Dequeue();
                    var model = await item.Document.GetSemanticModelAsync(token).ConfigureAwait(false);
                    var annotations = _securityAnalysis && model != null
                        ? await CollectAnnotationsAsync(item, model, token).ConfigureAwait(false) : null;
                    if (!AppendEvidence(item, annotations))
                        continue;
                    if (model == null)
                    {
                        _limitations.Add("Semantic information was unavailable for " + item.Document.Name);
                        continue;
                    }
                    if (model.GetDiagnostics(item.Node.Span, token).Any(d => d.Severity == DiagnosticSeverity.Error))
                        _limitations.Add("Compilation errors affect symbol resolution in " + item.Document.Name);

                    // A selected interface/virtual declaration itself may have no body to traverse.
                    if (item.Depth == 0 && (item.Node is BaseMethodDeclarationSyntax || item.Node is BasePropertyDeclarationSyntax))
                    {
                        var declared = model.GetDeclaredSymbol(item.Node, token);
                        if (declared != null)
                            await FollowSymbolAsync(declared, item.Document.Project.Solution, 0,
                                "Selected declaration", token).ConfigureAwait(false);
                    }

                    var seen = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
                    foreach (var syntax in item.Node.DescendantNodesAndSelf())
                    {
                        token.ThrowIfCancellationRequested();
                        if (!(syntax is SimpleNameSyntax || syntax is InvocationExpressionSyntax
                            || syntax is BaseObjectCreationExpressionSyntax || syntax is ElementAccessExpressionSyntax
                            || syntax is ConstructorInitializerSyntax || syntax is BinaryExpressionSyntax
                            || syntax is PrefixUnaryExpressionSyntax || syntax is PostfixUnaryExpressionSyntax))
                            continue;
                        if (++_examinedReferences > 6000)
                        {
                            _limitations.Add("Symbol reference limit reached; remaining dependencies were not inspected.");
                            _pending.Clear();
                            break;
                        }

                        var info = model.GetSymbolInfo(syntax, token);
                        var symbol = info.Symbol;
                        if (symbol == null)
                        {
                            if (syntax is InvocationExpressionSyntax || syntax is BaseObjectCreationExpressionSyntax)
                                AddExternal("Unresolved/dynamic call at " + Location(item.Document, syntax));
                            continue;
                        }
                        if (symbol is IMethodSymbol method)
                            symbol = (method.ReducedFrom ?? method).OriginalDefinition;
                        else
                            symbol = symbol.OriginalDefinition;
                        if (!(symbol is IMethodSymbol || symbol is IPropertySymbol || symbol is IFieldSymbol || symbol is IEventSymbol)
                            || !seen.Add(symbol))
                            continue;

                        var origin = "Referenced from " + Location(item.Document, syntax);
                        await FollowSymbolAsync(symbol, item.Document.Project.Solution, item.Depth + 1, origin, token).ConfigureAwait(false);
                    }
                }
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                _limitations.Add("Dependency collection time limit reached; this is a partial analysis.");
            }
            cancellationToken.ThrowIfCancellationRequested();

            var result = new StringBuilder(_securityAnalysis
                ? "\n\n## Static security analysis context\n" : "\n\n## Static performance analysis context\n");
            result.AppendLine("Selected location: " + document.Project.Name + "/" + document.Name
                + " (character span " + selection.Start + ".." + selection.End + ")");
            result.AppendLine($"Collected {_declarations} source declarations. Limits: depth {_maxDepth}, {_maxDeclarations} declarations, {_maxCharacters} source-context characters, {_timeout.TotalSeconds:0} seconds.");
            result.AppendLine("Source is an analysis-time snapshot including editor changes. Only reachable, collected declarations are evidence; no runtime measurements were taken.");
            result.AppendLine("Reflection, dynamic/delegate dispatch, DI configuration, external service internals, implicit compiler calls, and runtime data may be missing. Possible implementations are not proven runtime targets. Type members not referenced by collected code are not exhaustively traversed.");
            if (_securityAnalysis)
            {
                result.AppendLine($"Caller search: up to {_maxCallerDepth} levels above selected declarations, 24 symbol queries, and at most {Math.Min(24, _maxDeclarations / 3)} caller declarations within the shared budget. Callees of collected callers are also followed within the dependency limits.");
                result.AppendLine("This is source-context collection, not complete taint/data-flow analysis or proof of exploitability. Caller relationships do not prove that a guard protects every path. Middleware, global filters, routing, authorization policies and deployment configuration are not exhaustively collected. Attribute presence alone does not prove that protection is enforced; absence in this context does not prove a vulnerability.");
            }
            foreach (var limitation in _limitations.OrderBy(s => s, StringComparer.Ordinal))
                result.AppendLine("- LIMITATION: " + limitation);
            if (_external.Count > 0)
            {
                result.AppendLine("### Dependencies without inspected source (bounded list)");
                foreach (var external in _external.OrderBy(s => s, StringComparer.Ordinal))
                    result.AppendLine("- " + external);
            }
            result.Append(_evidence);
            return result.ToString();
        }

        private async Task CollectCallersAsync(WorkItem[] seeds, CancellationToken token)
        {
            // Reserve most of the overall collection deadline for source and outgoing dependencies.
            using var callerTimeout = CancellationTokenSource.CreateLinkedTokenSource(token);
            callerTimeout.CancelAfter(TimeSpan.FromSeconds(Math.Min(10, Math.Max(0.001, _timeout.TotalSeconds / 3))));
            var callerToken = callerTimeout.Token;
            var queue = new Queue<WorkItem>(seeds);
            var visitedNodes = new HashSet<string>();
            var searchedSymbols = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
            var callerCount = 0;
            var queryCount = 0;
            var callerLimit = Math.Min(24, _maxDeclarations / 3);
            try
            {
                while (queue.Count > 0)
                {
                    callerToken.ThrowIfCancellationRequested();
                    var item = queue.Dequeue();
                    var key = item.Document.Id + ":" + item.Node.SpanStart;
                    if (!visitedNodes.Add(key))
                        continue;
                    if (item.Depth >= _maxCallerDepth)
                    {
                        _limitations.Add("Caller depth limit reached; earlier entry points were not searched.");
                        continue;
                    }
                    var model = await item.Document.GetSemanticModelAsync(callerToken).ConfigureAwait(false);
                    if (model == null)
                        continue;
                    var symbol = GetDeclarationSymbol(model, item.Node, callerToken);
                    if (symbol == null)
                    {
                        _limitations.Add("Caller search is unavailable for a selected declaration without a method/property/event symbol.");
                        continue;
                    }

                    // Calls through an interface or base member can reach a selected implementation.
                    foreach (var target in CallerTargets(symbol, callerToken))
                    {
                        if (!searchedSymbols.Add(target.OriginalDefinition))
                            continue;
                        if (++queryCount > 24 || callerCount >= callerLimit)
                        {
                            _limitations.Add("Caller query/declaration limit reached; additional callers were not inspected.");
                            return;
                        }
                        var solution = item.Document.Project.Solution;
                        var callers = await SymbolFinder.FindCallersAsync(target, solution, callerToken).ConfigureAwait(false);
                        foreach (var caller in callers)
                        {
                            foreach (var location in caller.Locations)
                            {
                                callerToken.ThrowIfCancellationRequested();
                                if (++_examinedReferences > 6000)
                                {
                                    _limitations.Add("Symbol reference limit reached during caller search.");
                                    return;
                                }
                                var document = location.SourceTree == null ? null : solution.GetDocument(location.SourceTree);
                                if (document == null || document.Project.Language != LanguageNames.CSharp)
                                {
                                    _limitations.Add("A caller has no accessible C# solution source.");
                                    continue;
                                }
                                var root = await document.GetSyntaxRootAsync(callerToken).ConfigureAwait(false);
                                var node = root?.FindNode(location.SourceSpan, getInnermostNodeForTie: true)
                                    .AncestorsAndSelf().FirstOrDefault(n => n is LocalFunctionStatementSyntax
                                        || (n is MemberDeclarationSyntax && !(n is BaseTypeDeclarationSyntax)
                                            && !(n is BaseNamespaceDeclarationSyntax)));
                                if (node == null)
                                {
                                    _limitations.Add("A caller location could not be expanded to its enclosing declaration.");
                                    continue;
                                }
                                var origin = (caller.IsDirect && SymbolEqualityComparer.Default.Equals(target, symbol)
                                    ? "Caller of " : "Possible caller via contract/dispatch of ")
                                    + Clip(symbol.ToDisplayString()) + "; reference at "
                                    + document.Project.Name + "/" + (document.FilePath ?? document.Name) + ":"
                                    + (location.GetLineSpan().StartLinePosition.Line + 1);
                                if (!_visited.Contains(NodeKey(document, node)))
                                {
                                    if (callerCount >= callerLimit)
                                    {
                                        _limitations.Add("Caller declaration limit reached; additional callers were not inspected.");
                                        return;
                                    }
                                    callerCount++;
                                    Enqueue(document, node, 0, origin);
                                }
                                queue.Enqueue(new WorkItem(document, node, item.Depth + 1, origin));
                            }
                        }
                    }
                }
            }
            catch (OperationCanceledException) when (!token.IsCancellationRequested)
            {
                _limitations.Add("Caller search time limit reached; caller context is partial.");
            }
        }

        private static ISymbol GetDeclarationSymbol(SemanticModel model, SyntaxNode node, CancellationToken token)
        {
            if (node is BaseMethodDeclarationSyntax || node is BasePropertyDeclarationSyntax || node is LocalFunctionStatementSyntax)
                return model.GetDeclaredSymbol(node, token);
            return null;
        }

        private static IEnumerable<ISymbol> CallerTargets(ISymbol symbol, CancellationToken token)
        {
            yield return symbol;
            for (var current = OverriddenMember(symbol); current != null; current = OverriddenMember(current))
            {
                token.ThrowIfCancellationRequested();
                yield return current;
            }
            var type = symbol.ContainingType;
            if (type == null)
                yield break;
            foreach (var contract in type.AllInterfaces)
                foreach (var member in contract.GetMembers())
                {
                    token.ThrowIfCancellationRequested();
                    var implementation = type.FindImplementationForInterfaceMember(member);
                    if (SymbolEqualityComparer.Default.Equals(implementation?.OriginalDefinition, symbol.OriginalDefinition))
                        yield return member;
                }
        }

        private static ISymbol OverriddenMember(ISymbol symbol)
        {
            if (symbol is IMethodSymbol method) return method.OverriddenMethod;
            if (symbol is IPropertySymbol property) return property.OverriddenProperty;
            if (symbol is IEventSymbol evt) return evt.OverriddenEvent;
            return null;
        }

        private async Task<string> CollectAnnotationsAsync(WorkItem item, SemanticModel model, CancellationToken token)
        {
            var output = new StringBuilder();
            var symbols = new Queue<ISymbol>();
            var member = GetDeclarationSymbol(model, item.Node, token);
            if (member != null)
                symbols.Enqueue(member);
            var typeNode = item.Node.Ancestors().OfType<TypeDeclarationSyntax>().FirstOrDefault();
            var type = typeNode == null ? null : model.GetDeclaredSymbol(typeNode, token);
            if (type != null)
                symbols.Enqueue(type);
            var visited = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
            var annotationLimit = Math.Min(8000, _maxCharacters / 4);
            while (symbols.Count > 0)
            {
                token.ThrowIfCancellationRequested();
                var symbol = symbols.Dequeue();
                if (!visited.Add(symbol))
                    continue;
                if (visited.Count > 12)
                {
                    _limitations.Add("Containing/base declaration annotation limit reached.");
                    break;
                }
                if (symbol is INamedTypeSymbol namedType)
                {
                    if (namedType.ContainingType != null) symbols.Enqueue(namedType.ContainingType);
                    if (namedType.BaseType != null && namedType.BaseType.SpecialType != SpecialType.System_Object)
                        symbols.Enqueue(namedType.BaseType);
                }
                else if (OverriddenMember(symbol) is ISymbol baseMember)
                    symbols.Enqueue(baseMember);

                foreach (var attribute in symbol.GetAttributes())
                {
                    token.ThrowIfCancellationRequested();
                    var description = attribute.ToString();
                    if (description.Length > 400)
                        _limitations.Add("An attribute description was truncated; review its source before drawing conclusions.");
                    var reference = attribute.ApplicationSyntaxReference;
                    var document = reference == null ? null : item.Document.Project.Solution.GetDocument(reference.SyntaxTree);
                    var location = document == null ? "metadata/source unavailable"
                        : Location(document, await reference.GetSyntaxAsync(token).ConfigureAwait(false));
                    var line = "Declared attribute on " + Clip(symbol.ToDisplayString()) + " at " + location + ": " + Clip(description) + "\n";
                    if (output.Length + line.Length > annotationLimit)
                    {
                        _limitations.Add("Attribute context size limit reached; additional attributes were omitted.");
                        return output.ToString();
                    }
                    output.Append(line);
                }
            }
            if (output.Length > 0)
                output.Insert(0, "Declaration attributes (including partial/containing/base declarations; inheritance and enforcement depend on the framework):\n");
            return output.ToString();
        }

        private async Task FollowSymbolAsync(ISymbol symbol, Microsoft.CodeAnalysis.Solution solution, int depth, string origin, CancellationToken token)
        {
            if (depth > _maxDepth)
            {
                _limitations.Add("Dependency depth limit reached; deeper calls were not inspected.");
                return;
            }
            var source = await SymbolFinder.FindSourceDefinitionAsync(symbol, solution, token).ConfigureAwait(false) ?? symbol;
            var found = false;
            foreach (var reference in source.DeclaringSyntaxReferences)
            {
                token.ThrowIfCancellationRequested();
                var document = solution.GetDocument(reference.SyntaxTree);
                if (document == null || document.Project.Language != LanguageNames.CSharp)
                    continue;
                var node = await reference.GetSyntaxAsync(token).ConfigureAwait(false);
                if (node is VariableDeclaratorSyntax)
                    node = node.Ancestors().OfType<MemberDeclarationSyntax>().FirstOrDefault() ?? node;
                // Implicit constructors can point at a type. Never include an entire class as a dependency.
                if (node is BaseTypeDeclarationSyntax)
                {
                    continue;
                }
                found = true;
                Enqueue(document, node, depth, origin + "; " + Clip(source.ToDisplayString()));
            }
            if (source is IMethodSymbol partial && partial.PartialImplementationPart != null)
                await FollowSymbolAsync(partial.PartialImplementationPart, solution, depth, origin + " (partial implementation)", token).ConfigureAwait(false);

            if (source is IMethodSymbol constructor && constructor.MethodKind == MethodKind.Constructor
                && constructor.ContainingType.DeclaringSyntaxReferences.Length > 0)
            {
                found = true;
                await FollowInitializersAsync(constructor, solution, depth, token).ConfigureAwait(false);
            }
            if (!found)
                AddExternal(Clip(symbol.ToDisplayString()) + " [" + symbol.ContainingAssembly?.Name + "] — " + origin);

            if (!_dispatchVisited.Add(source))
                return;
            if (source.ContainingType?.TypeKind == TypeKind.Interface)
            {
                foreach (var implementation in await SymbolFinder.FindImplementationsAsync(source, solution, cancellationToken: token).ConfigureAwait(false))
                    await FollowSymbolAsync(implementation, solution, depth, "Possible interface implementation of " + Clip(source.ToDisplayString()), token).ConfigureAwait(false);
            }
            else if (source.IsVirtual || source.IsAbstract || source.IsOverride)
            {
                foreach (var implementation in await SymbolFinder.FindOverridesAsync(source, solution, cancellationToken: token).ConfigureAwait(false))
                    await FollowSymbolAsync(implementation, solution, depth, "Possible override of " + Clip(source.ToDisplayString()), token).ConfigureAwait(false);
            }
        }

        private async Task FollowInitializersAsync(IMethodSymbol constructor, Microsoft.CodeAnalysis.Solution solution,
            int depth, CancellationToken token)
        {
            // Initializers can dominate construction even when the constructor never references the field.
            foreach (var reference in constructor.ContainingType.DeclaringSyntaxReferences)
            {
                var document = solution.GetDocument(reference.SyntaxTree);
                if (document == null)
                    continue;
                var declaration = await reference.GetSyntaxAsync(token).ConfigureAwait(false) as TypeDeclarationSyntax;
                if (declaration == null)
                    continue;
                foreach (var member in declaration.Members)
                {
                    token.ThrowIfCancellationRequested();
                    if ((member is BaseFieldDeclarationSyntax field && field.Declaration.Variables.Any(v => v.Initializer != null))
                        || (member is PropertyDeclarationSyntax property && property.Initializer != null)
                        || (member is ConstructorDeclarationSyntax ctor && ctor.Modifiers.Any(SyntaxKind.StaticKeyword)))
                        Enqueue(document, member, depth, "Construction/type initialization of " + constructor.ContainingType.ToDisplayString()
                            + " (static initialization is a first-use cost)");
                }
            }

            var hasExplicitInitializer = false;
            foreach (var reference in constructor.DeclaringSyntaxReferences)
            {
                var syntax = await reference.GetSyntaxAsync(token).ConfigureAwait(false);
                if (syntax is ConstructorDeclarationSyntax ctor && ctor.Initializer != null)
                    hasExplicitInitializer = true;
                // Primary constructor base calls require separate handling; report this coverage gap.
                if (syntax is TypeDeclarationSyntax type && type.ParameterList != null)
                {
                    hasExplicitInitializer = true;
                    _limitations.Add("Primary constructor base calls require manual review: " + constructor.ContainingType.ToDisplayString());
                }
            }
            if (!hasExplicitInitializer)
            {
                var baseConstructor = constructor.ContainingType.BaseType?.InstanceConstructors.FirstOrDefault(c => c.Parameters.Length == 0);
                if (baseConstructor != null && baseConstructor.ContainingType.SpecialType != SpecialType.System_Object)
                    await FollowSymbolAsync(baseConstructor, solution, depth + 1,
                        "Implicit base constructor of " + constructor.ContainingType.ToDisplayString(), token).ConfigureAwait(false);
            }
        }

        private void Enqueue(Document document, SyntaxNode node, int depth, string origin)
        {
            var key = NodeKey(document, node);
            if (!_visited.Add(key))
                return;
            if (_pending.Count + _declarations >= _maxDeclarations)
            {
                _limitations.Add("Declaration limit reached; additional dependencies were omitted.");
                return;
            }
            _pending.Enqueue(new WorkItem(document, node, depth, origin));
        }

        private bool AppendEvidence(WorkItem item, string annotations)
        {
            var text = item.Node.SyntaxTree.GetText();
            var start = text.Lines.GetLineFromPosition(item.Node.SpanStart).LineNumber;
            var end = text.Lines.GetLineFromPosition(Math.Max(item.Node.SpanStart, item.Node.Span.End - 1)).LineNumber;
            var block = new StringBuilder();
            block.AppendLine("\n### " + Location(item.Document, item.Node) + " (depth " + item.Depth + ")");
            block.AppendLine(item.Origin);
            var type = item.Node.Ancestors().OfType<TypeDeclarationSyntax>().FirstOrDefault();
            if (type != null)
                block.AppendLine("Containing type: " + Clip(type.Identifier.Text + type.TypeParameterList + " " + type.BaseList));
            if (!string.IsNullOrEmpty(annotations))
                block.Append(annotations);
            block.AppendLine("```csharp");
            for (var i = start; i <= end; i++)
            {
                // Keep original line numbers without including neighboring declarations on the same line.
                var line = text.Lines[i];
                var span = TextSpan.FromBounds(Math.Max(line.Start, item.Node.SpanStart), Math.Min(line.End, item.Node.Span.End));
                block.Append(i + 1).Append(": ").AppendLine(text.ToString(span));
                if (_evidence.Length + block.Length > _maxCharacters - 100)
                {
                    _limitations.Add("Source context size limit reached; at least one declaration was truncated and its dependencies were not traversed.");
                    block.AppendLine("// [remaining declaration omitted]").AppendLine("```");
                    // Even a single extremely long source line must not exceed the budget.
                    if (block.Length <= _maxCharacters - _evidence.Length)
                        _evidence.Append(block);
                    return false;
                }
            }
            block.AppendLine("```");
            _evidence.Append(block);
            _declarations++;
            return true;
        }

        private void AddExternal(string value)
        {
            if (_external.Count < 40)
                _external.Add(value);
            else
                _limitations.Add("External/unresolved dependency list was truncated.");
        }

        private static string Clip(string value) => value.Length <= 400 ? value : value.Substring(0, 400) + "...";

        private static string NodeKey(Document document, SyntaxNode node) => document.Id + ":" + node.Span.Start + ":" + node.Span.Length;

        private static string Location(Document document, SyntaxNode node)
        {
            var line = node.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
            return document.Project.Name + "/" + (document.FilePath ?? document.Name) + ":" + line;
        }

        private sealed class WorkItem
        {
            internal WorkItem(Document document, SyntaxNode node, int depth, string origin)
            {
                Document = document;
                Node = node;
                Depth = depth;
                Origin = origin;
            }
            internal Document Document { get; }
            internal SyntaxNode Node { get; }
            internal int Depth { get; }
            internal string Origin { get; }
        }
    }
}
