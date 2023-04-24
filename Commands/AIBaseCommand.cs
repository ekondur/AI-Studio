using EnvDTE;
using Microsoft.VisualStudio.Shell.Interop;
using OpenAI_API;
using System.Collections.Generic;
using System.Linq;

namespace AI_Studio
{
    internal class AIBaseCommand<T> : BaseCommand<T> where T : class, new()
    {
        public string SystemMessage { get; set; }
        public string UserInput { get; set; }
        public List<string> AssistantInputs  { get; set; } = new List<string>();

        protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
        {
            var generalOptions = await General.GetLiveInstanceAsync();

            if (string.IsNullOrEmpty(generalOptions.ApiKey))
            {
                await VS.MessageBox.ShowAsync("API Key is missing, go to Tools/Options/AI Stuido/General and add the API Key created from https://platform.openai.com/account/api-keys",
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
            var text = docView.TextView.Selection.StreamSelectionSpan.GetText();

            if (string.IsNullOrEmpty(text))
            {
                twd.EndWaitDialog();
                await VS.MessageBox.ShowAsync("Nothing Selected!", buttons: OLEMSGBUTTON.OLEMSGBUTTON_OK);
            }

            var api = new OpenAIAPI(generalOptions.ApiKey);
            var chat = api.Chat.CreateConversation();

            chat.AppendSystemMessage(SystemMessage);
            chat.AppendUserInput(text);
            if (!string.IsNullOrEmpty(UserInput))
            {
                chat.AppendUserInput(UserInput);
            }
            foreach (var input in AssistantInputs)
            {
                chat.AppendExampleChatbotOutput(input);
            }

            try
            {
                string response = await chat.GetResponseFromChatbotAsync();

                twd.EndWaitDialog();

                if (typeof(T).Name == "Explain" || typeof(T).Name == "SecurityCheck")
                {
                    await VS.MessageBox.ShowAsync(response, buttons: OLEMSGBUTTON.OLEMSGBUTTON_OK);
                }
                else if (typeof(T).Name == "AddUnitTests" || typeof(T).Name == "CodeIt")
                {
                    docView.TextBuffer.Insert(selection.End, Environment.NewLine + response);
                }
                else
                {
                    docView.TextBuffer.Replace(selection, response);
                }
            }
            catch (Exception ex)
            {
                twd.EndWaitDialog();
                await VS.MessageBox.ShowAsync(ex.Message, buttons: OLEMSGBUTTON.OLEMSGBUTTON_OK);
            }

            if (generalOptions.FormatDocument)
            {
                (await VS.GetServiceAsync<DTE, DTE>()).ExecuteCommand("Edit.FormatDocument", string.Empty);
            }
        }
    }
}
