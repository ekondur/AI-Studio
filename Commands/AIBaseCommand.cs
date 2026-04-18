using AI_Studio.Helpers;
using EnvDTE;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.AI;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Threading;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace AI_Studio
{
    internal class AIBaseCommand<T> : BaseCommand<T> where T : class, new()
    {
        public string SystemMessage { get; set; }
        public string UserInput { get; set; }
        public List<string> AssistantInputs  { get; set; } = new List<string>();
        public ResponseBehavior ResponseBehavior { get; set; }

        protected bool _addContentTypePrefix = false;
        protected bool _stripResponseMarkdownCode = false;

        protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
        {
            var generalOptions = await General.GetLiveInstanceAsync();

            if (ChatClientFactory.RequiresApiKey(generalOptions.Provider) && string.IsNullOrEmpty(generalOptions.ApiKey))
            {
                await VS.MessageBox.ShowAsync(
                    $"API Key is missing for {generalOptions.Provider}. Go to Tools > Options > AI Studio > General to add your API key.",
                    buttons: OLEMSGBUTTON.OLEMSGBUTTON_OK);

                Package.ShowOptionPage(typeof(General));
                return;
            }

            await VS.StatusBar.StartAnimationAsync(StatusAnimation.General);
            await VS.StatusBar.ShowMessageAsync("AI Studio: Working on it...");

            var statusEnded = false;
            async Task EndStatusOnceAsync()
            {
                if (statusEnded)
                {
                    return;
                }

                statusEnded = true;
                await VS.StatusBar.EndAnimationAsync(StatusAnimation.General);
                await VS.StatusBar.ShowMessageAsync("AI Studio: Done");
            }

            // === Collect all UI state on the main thread ===
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var docView = await VS.Documents.GetActiveDocumentViewAsync();
            var snapshot = docView.TextView.TextBuffer.CurrentSnapshot;
            var selection = docView.TextView.Selection.SelectedSpans.FirstOrDefault();
            if (selection.Snapshot == null)
            {
                var caretPoint = docView.TextView.Caret.Position.BufferPosition;
                selection = new SnapshotSpan(caretPoint, caretPoint);
            }

            if (selection.Length == 0)
            {
                // Roslyn parsing is CPU-intensive — run it on a background thread
                var caretPos = selection.Start.Position;
                var snapshotForParse = snapshot;
                var expandedSpan = await Task.Run(() =>
                    TryExpandSelectionToMethod(snapshotForParse, caretPos, out var span)
                        ? (SnapshotSpan?)span
                        : null);

                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                if (expandedSpan.HasValue)
                {
                    docView.TextView.Selection.Select(expandedSpan.Value, false);
                }
                else
                {
                    var snapshotLine = snapshot.GetLineFromPosition(caretPos);
                    docView.TextView.Selection.Select(new SnapshotSpan(snapshotLine.Start, snapshotLine.End), false);
                }

                selection = docView.TextView.Selection.SelectedSpans.FirstOrDefault();
            }

            if (selection.Snapshot == null)
            {
                var caretPoint = docView.TextView.Caret.Position.BufferPosition;
                selection = new SnapshotSpan(caretPoint, caretPoint);
            }

            var text = docView.TextView.Selection.StreamSelectionSpan.GetText();
            var selectionStartLineNumber = docView.TextView.TextBuffer.CurrentSnapshot.GetLineNumberFromPosition(selection.Start.Position);
            var contentTypeName = docView.TextView.TextDataModel.ContentType.DisplayName;
            var insertionStart = ResponseBehavior == ResponseBehavior.Insert
                ? selection.End.Position
                : selection.Start.Position;
            var originalSelection = selection;

            if (string.IsNullOrEmpty(text))
            {
                await EndStatusOnceAsync();
                await VS.MessageBox.ShowAsync("Nothing Selected!", buttons: OLEMSGBUTTON.OLEMSGBUTTON_OK);
                return;
            }

            var messages = new List<ChatMessage>
            {
                new(ChatRole.System, SystemMessage),
                new(ChatRole.User, _addContentTypePrefix ? $"{contentTypeName}\n{text}" : text)
            };
            if (!string.IsNullOrEmpty(UserInput))
                messages.Add(new(ChatRole.User, UserInput));
            foreach (var input in AssistantInputs)
                messages.Add(new(ChatRole.User, input));

            using IChatClient client = ChatClientFactory.Create(generalOptions);

            // For Message mode: open the tool window and show the thinking indicator
            // before starting the HTTP call, so the user has immediate feedback.
            if (ResponseBehavior == ResponseBehavior.Message)
                await PrepareToolWindowAsync();

            // Switch to a background thread so HTTP and streaming do not block the UI.
            // We only jump back to the main thread when editing the text buffer.
            await TaskScheduler.Default;

            var cts = RequestCancellationManager.Begin();
            try
            {
                var responseBuilder = new StringBuilder();
                var currentLength = 0;
                var hasReplacedInitial = false;
                var wasCancelled = false;

                try
                {
                    await foreach (var update in client.GetStreamingResponseAsync(messages, cancellationToken: cts.Token))
                    {
                        var chunk = update?.Text;
                        if (string.IsNullOrEmpty(chunk))
                            continue;

                        var chunkOffset = currentLength;
                        var chunkToApply = ResponseBehavior == ResponseBehavior.Insert && currentLength == 0
                            ? Environment.NewLine + chunk
                            : chunk;

                        await EndStatusOnceAsync();
                        responseBuilder.Append(chunkToApply);

                        if (ResponseBehavior == ResponseBehavior.Message)
                        {
                            await ShowResponseInToolWindowAsync(responseBuilder.ToString(), isStreaming: true);
                            await TaskScheduler.Default; // release main thread before next chunk
                            continue;
                        }

                        // Grab main thread only for the buffer edit, then release it
                        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                        if (ResponseBehavior == ResponseBehavior.Replace && !hasReplacedInitial)
                        {
                            docView.TextBuffer.Replace(originalSelection, chunkToApply);
                            hasReplacedInitial = true;
                            currentLength = chunkToApply.Length;
                        }
                        else
                        {
                            docView.TextBuffer.Insert(insertionStart + chunkOffset, chunkToApply);
                            currentLength += chunkToApply.Length;
                        }

                        await TaskScheduler.Default; // release main thread before next chunk
                    }
                }
                catch (OperationCanceledException) when (cts.IsCancellationRequested)
                {
                    wasCancelled = true;
                }

                var response = responseBuilder.ToString();
                if (_stripResponseMarkdownCode)
                    response = StripResponseMarkdownCode(response);

                if (ResponseBehavior == ResponseBehavior.Message)
                {
                    if (wasCancelled)
                        response = response.Length > 0 ? response + "\n\n_(stopped)_" : "_(stopped)_";
                    await ShowResponseInToolWindowAsync(response);
                }
                else
                {
                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                    docView.TextBuffer.Replace(new Span(insertionStart, currentLength), response);
                    var finalSnapshot = docView.TextBuffer.CurrentSnapshot;
                    var finalSelection = new SnapshotSpan(finalSnapshot, Span.FromBounds(insertionStart, insertionStart + response.Length));
                    docView.TextView.Selection.Select(finalSelection, false);
                }

                if (wasCancelled)
                    await VS.StatusBar.ShowMessageAsync("AI Studio: Stopped");
            }
            catch (Exception ex)
            {
                await VS.MessageBox.ShowAsync(ex.Message, buttons: OLEMSGBUTTON.OLEMSGBUTTON_OK);
            }
            finally
            {
                RequestCancellationManager.End(cts);
                await EndStatusOnceAsync();
            }

            if (generalOptions.FormatChangedText && ResponseBehavior != ResponseBehavior.Message)
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                selection = docView.TextView.Selection.SelectedSpans.FirstOrDefault();
                if (selection.Length == 0)
                {
                    var startLine = docView.TextView.TextBuffer.CurrentSnapshot.GetLineFromLineNumber(selectionStartLineNumber);
                    var endLine = docView.TextView.TextBuffer.CurrentSnapshot.GetLineFromPosition(selection.End);
                    docView.TextView.Selection.Select(new SnapshotSpan(startLine.Start, endLine.End), false);
                }

                (await VS.GetServiceAsync<DTE, DTE>()).ExecuteCommand("Edit.FormatSelection");
            }
        }

        private static readonly Regex _markdownCodeRegex = new Regex(@"```.*\r?\n?", RegexOptions.Compiled);

        private string StripResponseMarkdownCode(string response)
        {
            return _markdownCodeRegex.Replace(response, "");
        }

        private bool TryExpandSelectionToMethod(ITextSnapshot snapshot, int caretPosition, out SnapshotSpan methodSpan)
        {
            methodSpan = default;

            if (snapshot == null || snapshot.Length == 0)
            {
                return false;
            }

            SyntaxTree syntaxTree;
            try
            {
                syntaxTree = CSharpSyntaxTree.ParseText(snapshot.GetText());
            }
            catch
            {
                return false;
            }

            var root = syntaxTree.GetRoot();
            var safePosition = Math.Max(0, Math.Min(caretPosition, snapshot.Length - 1));
            var token = root.FindToken(safePosition, findInsideTrivia: true);
            var methodNode = token.Parent?
                .AncestorsAndSelf()
                .OfType<BaseMethodDeclarationSyntax>()
                .FirstOrDefault();

            if (methodNode == null)
            {
                return false;
            }

            var span = methodNode.FullSpan;
            if (span.End > snapshot.Length)
            {
                return false;
            }

            methodSpan = new SnapshotSpan(snapshot, Span.FromBounds(span.Start, span.End));
            return true;
        }

        private async Task PrepareToolWindowAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            var toolWindow = await Package.FindToolWindowAsync(
                typeof(OutputToolWindow), 0, true, VsShellUtilities.ShutdownToken);
            var windowFrame = (IVsWindowFrame)toolWindow.Frame;
            ErrorHandler.ThrowOnFailure(windowFrame.Show());
            if (toolWindow is OutputToolWindow outputWindow)
                await outputWindow.BeginStreamingAsync();
        }

        private async Task ShowResponseInToolWindowAsync(string response, bool isStreaming = false)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            var toolWindow = await Package.FindToolWindowAsync(
                typeof(OutputToolWindow), 0, true, VsShellUtilities.ShutdownToken);
            var windowFrame = (IVsWindowFrame)toolWindow.Frame;
            ErrorHandler.ThrowOnFailure(windowFrame.Show());
            if (toolWindow is OutputToolWindow outputWindow)
                await outputWindow.UpdateContentAsync(response, isStreaming);
        }
    }
}
