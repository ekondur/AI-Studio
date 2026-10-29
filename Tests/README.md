# Source analysis regression checks

Run with the .NET 8 SDK or later:

```powershell
dotnet run --project Tests/SourceContextCollector.Tests.csproj
```

The executable uses Roslyn `AdhocWorkspace` fixtures and the production collector source. It checks cross-project and transitive resolution, overload disambiguation, recursion, interface/virtual dispatch, selected interface declarations, constructors and initializers, implicit base construction, fields/properties, extension/local/partial methods, unsaved snapshots, source locations, multi-member selections, unavailable source, context limits, timeout and user cancellation. It does not call an AI provider or execute fixture code.

Security-specific fixtures also verify cross-project upstream callers and validation helpers, caller overload isolation, interface/base-contract call sites, partial/containing/base type attributes and their source locations, reverse-call cycles, caller depth/count limits, annotation bounds, and that performance analysis does not collect callers.

## Visual Studio integration smoke test

Use Visual Studio 2022 17.10+ with the built VSIX in an experimental instance:

1. Open a C# solution with a project reference. Leave changes unsaved in both the selected method and a dependency.
2. Select the calling method and choose **AI Studio > Analyze Performance**. Confirm a response appears in Chat with dependency evidence, original file/line locations and scope limitations.
3. Ask a follow-up about a dependency. Confirm the original analysis context is retained.
4. Start another analysis and use **Stop** during collection, then during streaming. Confirm no cancelled result can be applied and another request can start normally.
5. Start **New Chat** while collection is in progress. Confirm the old request cannot overwrite the new conversation.
6. Try a non-C# file and a C# file outside a loaded project. Confirm a clear unsupported-document message appears without sending an AI request.
7. Run **Security Check** on a service called by a controller with a validation helper and class/method attributes. Confirm the response includes the caller/guard context and qualifies possible dispatch and missing policy/configuration evidence. Ask a follow-up about an upstream guard.
8. For a linked C# file, switch its active project context and confirm dependency resolution follows that project's symbols.
9. Run **Security Check** on a non-C# file and a C# file outside a loaded project. Confirm selection-only review still works and its limited scope is stated. **Analyze Performance** should still require a loaded C# project.
10. Cancel **Security Check** during caller collection, restart it, and open **New Chat** during collection. Confirm cancellation and conversation isolation work. Confirm **Explain** and existing editor diff actions still work.

These UI/provider checks require a running Visual Studio instance and a configured provider; the executable tests do not cover them.
