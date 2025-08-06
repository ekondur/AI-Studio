﻿using EnvDTE;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using OpenAI;
using OpenAI.Chat;
using System.ClientModel;
using System.Collections.Generic;
using System.Linq;
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

            var docView = await VS.Documents.GetActiveDocumentViewAsync();
            var selection = docView.TextView.Selection.SelectedSpans.FirstOrDefault();
            if (selection.Length == 0)
            {
                var textBuffer = docView.TextView.TextBuffer;
                var line = textBuffer.CurrentSnapshot.GetLineFromPosition(selection.Start.Position);
                var snapshotSpan = new SnapshotSpan(line.Start, line.End);
                docView.TextView.Selection.Select(snapshotSpan, false);
                selection = docView.TextView.Selection.SelectedSpans.FirstOrDefault();
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
                    new SystemChatMessage(SystemMessage),
                    new UserChatMessage(_addContentTypePrefix
                        ? $"{docView.TextView.TextDataModel.ContentType.DisplayName}\n{text}"
                        : text)
                };

            if (!string.IsNullOrEmpty(UserInput))
            {
                messages.Add(new UserChatMessage(UserInput));
            }

            foreach (var input in AssistantInputs)
            {
                messages.Add(new UserChatMessage(input));
            }

            ChatClient client = new ChatClient(
                model: generalOptions.LanguageModel,
                credential: new ApiKeyCredential(generalOptions.ApiKey),
                options: new OpenAIClientOptions
                {
                    Endpoint = new Uri(generalOptions.ApiEndpoint)
                }
            );

            try
            {
                var completion = await client.CompleteChatAsync(messages);
                var response = completion.Value.Content[0].Text;

                if (_stripResponseMarkdownCode)
                {
                    response = StripResponseMarkdownCode(response);
                }

                twd.EndWaitDialog();

                switch (ResponseBehavior)
                {
                    case ResponseBehavior.Insert:
                        docView.TextBuffer.Insert(selection.End, Environment.NewLine + response);
                        break;
                    case ResponseBehavior.Replace:
                        docView.TextBuffer.Replace(selection, response);
                        break;
                    case ResponseBehavior.Message:
                        await ShowResponseInToolWindowAsync(response);
                        break;
                }
            }
            catch (Exception ex)
            {
                twd.EndWaitDialog();
                await VS.MessageBox.ShowAsync(ex.Message, buttons: OLEMSGBUTTON.OLEMSGBUTTON_OK);
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
