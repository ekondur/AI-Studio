using EnvDTE;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.AI;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using System.ClientModel;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

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

            if (string.IsNullOrEmpty(generalOptions.ApiKey))
            {
                await VS.MessageBox.ShowAsync("API Key is missing, go to Tools/Options/AI Studio/General and add the API Key created from https://platform.openai.com/account/api-keys",
                    buttons: OLEMSGBUTTON.OLEMSGBUTTON_OK);

                Package.ShowOptionPage(typeof(General));
                return;
            }

            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            var fac = (IVsThreadedWaitDialogFactory)await VS.Services.GetThreadedWaitDialogAsync();
            IVsThreadedWaitDialog4 twd = fac.CreateInstance();

            twd.StartWaitDialog("AI Studio", "Working on it...", "", null, "", 1, false, true);
            var waitEnded = false;
            async Task EndWaitDialogOnceAsync()
            {
                if (waitEnded)
                {
                    return;
                }

                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                twd.EndWaitDialog();
                waitEnded = true;
            }

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
                if (TryExpandSelectionToMethod(snapshot, selection.Start.Position, out var methodSpan))
                {
                    docView.TextView.Selection.Select(methodSpan, false);
                }
                else
                {
                    var line = snapshot.GetLineFromPosition(selection.Start.Position);
                    var snapshotSpan = new SnapshotSpan(line.Start, line.End);
                    docView.TextView.Selection.Select(snapshotSpan, false);
                }

                selection = docView.TextView.Selection.SelectedSpans.FirstOrDefault();
            }

            if (selection.Snapshot == null)
            {
                var caretPoint = docView.TextView.Caret.Position.BufferPosition;
                selection = new SnapshotSpan(caretPoint, caretPoint);
            }
            var text = docView.TextView.Selection.StreamSelectionSpan.GetText();
            int selectionStartLineNumber = docView.TextView.TextBuffer.CurrentSnapshot.GetLineNumberFromPosition(selection.Start.Position);

            if (string.IsNullOrEmpty(text))
            {
                twd.EndWaitDialog();
                await VS.MessageBox.ShowAsync("Nothing Selected!", buttons: OLEMSGBUTTON.OLEMSGBUTTON_OK);
            }

            if (_addContentTypePrefix)
            {
                text = $"{docView.TextView.TextDataModel.ContentType.DisplayName}\n{text}";
            }

            // Prepare messages
            var messages = new List<ChatMessage>
                {
                    new(ChatRole.System, SystemMessage),
                    new(ChatRole.User, _addContentTypePrefix
                        ? $"{docView.TextView.TextDataModel.ContentType.DisplayName}\n{text}"
                        : text)
                };

            if (!string.IsNullOrEmpty(UserInput))
            {
                messages.Add(new(ChatRole.User, UserInput));
            }

            foreach (var input in AssistantInputs)
            {
                messages.Add(new(ChatRole.User, input));
            }

            IChatClient client = new OpenAI.Chat.ChatClient(
                model: generalOptions.LanguageModel,
                credential: new ApiKeyCredential(generalOptions.ApiKey),
                options: new OpenAI.OpenAIClientOptions
                {
                    Endpoint = new Uri(generalOptions.ApiEndpoint)
                }
            ).AsIChatClient();

            try
            {
                var responseBuilder = new StringBuilder();
                var insertionStart = ResponseBehavior == ResponseBehavior.Insert
                    ? selection.End.Position
                    : selection.Start.Position;
                var currentLength = 0;
                var hasReplacedInitial = false;

                await foreach (var update in client.GetStreamingResponseAsync(messages))
                {
                    var chunk = update?.Text;
                    if (string.IsNullOrEmpty(chunk))
                    {
                        continue;
                    }

                    var chunkOffset = currentLength;
                    var chunkToApply = chunk;

                    if (ResponseBehavior == ResponseBehavior.Insert && currentLength == 0)
                    {
                        chunkToApply = Environment.NewLine + chunkToApply;
                    }

                    await EndWaitDialogOnceAsync();

                    responseBuilder.Append(chunkToApply);

                    if (ResponseBehavior == ResponseBehavior.Message)
                    {
                        await ShowResponseInToolWindowAsync(responseBuilder.ToString());
                        continue;
                    }

                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                    if (ResponseBehavior == ResponseBehavior.Replace && !hasReplacedInitial)
                    {
                        docView.TextBuffer.Replace(selection, chunkToApply);
                        hasReplacedInitial = true;
                        currentLength = chunkToApply.Length;
                        continue;
                    }

                    docView.TextBuffer.Insert(insertionStart + chunkOffset, chunkToApply);
                    currentLength += chunkToApply.Length;
                }

                var response = responseBuilder.ToString();

                if (_stripResponseMarkdownCode)
                {
                    response = StripResponseMarkdownCode(response);
                }

                if (ResponseBehavior == ResponseBehavior.Message)
                {
                    await ShowResponseInToolWindowAsync(response);
                }
                else
                {
                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                    docView.TextBuffer.Replace(new Span(insertionStart, currentLength), response);

                    var finalSnapshot = docView.TextBuffer.CurrentSnapshot;
                    selection = new SnapshotSpan(finalSnapshot, Span.FromBounds(insertionStart, insertionStart + response.Length));
                    docView.TextView.Selection.Select(selection, false);
                }

            }
            catch (Exception ex)
            {
                await VS.MessageBox.ShowAsync(ex.Message, buttons: OLEMSGBUTTON.OLEMSGBUTTON_OK);
            }
            finally
            {
                await EndWaitDialogOnceAsync();
            }

            if (generalOptions.FormatChangedText && ResponseBehavior != ResponseBehavior.Message)
            {
                selection = docView.TextView.Selection.SelectedSpans.FirstOrDefault();
                if (selection.Length == 0)
                {
                    var startLine = docView.TextView.TextBuffer.CurrentSnapshot.GetLineFromLineNumber(selectionStartLineNumber);
                    var endLine = docView.TextView.TextBuffer.CurrentSnapshot.GetLineFromPosition(selection.End);
                    var snapshotSpan = new SnapshotSpan(startLine.Start, endLine.End);
                    docView.TextView.Selection.Select(snapshotSpan, false);
                }

                (await VS.GetServiceAsync<DTE, DTE>()).ExecuteCommand("Edit.FormatSelection");
            }
        }

        private string StripResponseMarkdownCode(string response)
        {
            var regex = new Regex(@"```.*\r?\n?");
            return regex.Replace(response, "");
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

        private async Task ShowResponseInToolWindowAsync(string response)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            // Create a cancellation token
            var cancellationToken = VsShellUtilities.ShutdownToken;

            // Get the tool window with the cancellation token
            var toolWindow = await Package.FindToolWindowAsync(
                typeof(OutputToolWindow),
                0,
                true,
                cancellationToken);

            // Show the tool window
            var windowFrame = (IVsWindowFrame)toolWindow.Frame;
            ErrorHandler.ThrowOnFailure(windowFrame.Show());

            // Update the content
            if (toolWindow is OutputToolWindow yourWindow)
            {
                await yourWindow.UpdateContentAsync(response);
            }
        }
    }
}
