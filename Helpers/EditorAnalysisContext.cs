using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Threading;
using System.Threading;
using System.Threading.Tasks;

namespace AI_Studio.Helpers
{
    internal static class EditorAnalysisContext
    {
        internal static async Task<string> CollectAsync(ITextSnapshot snapshot, SnapshotSpan selection,
            bool securityAnalysis, CancellationToken cancellationToken)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
            // Preserve the active project context for linked files and include unsaved editor changes.
            var document = snapshot.GetOpenDocumentInCurrentContextWithChanges();
            if (document == null || document.Project.Language != LanguageNames.CSharp)
            {
                if (securityAnalysis)
                    return "\n\n## Security analysis scope\nSelection-only review: no loaded C# solution document was available. No callers, dependencies, or containing-type attributes were inspected. Do not infer that missing guards or configuration are absent from the application.";
                throw new InvalidOperationException("Analyze Performance requires a C# document in a loaded solution. Wait for the project to load and try again.");
            }

            var span = new TextSpan(selection.Start.Position, selection.Length);
            await VS.StatusBar.ShowMessageAsync(securityAnalysis
                ? "AI Studio: Collecting security dependencies and callers..."
                : "AI Studio: Collecting performance analysis dependencies...");
            await TaskScheduler.Default;
            return await new SourceContextCollector(securityAnalysis: securityAnalysis)
                .CollectAsync(document, span, cancellationToken);
        }
    }
}
