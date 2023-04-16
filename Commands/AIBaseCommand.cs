using EnvDTE;
using Microsoft.VisualStudio.Shell.Interop;
using OpenAI_API;
using OpenAI_API.Chat;
using System.Linq;

namespace AI_Studio
{
    internal class AIBaseCommand<T> : BaseCommand<T> where T : class, new()
    {
        public string SystemMessage { get; set; }

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

            var api = new OpenAIAPI(generalOptions.ApiKey);
            var chat = api.Chat.CreateConversation();

            chat.AppendSystemMessage(SystemMessage);
            chat.AppendUserInput(text);

            var commandsOptions = await Commands.GetLiveInstanceAsync();
            var unitTestsOptions = await UnitTests.GetLiveInstanceAsync();
            AddSettingsInputs(chat, commandsOptions, unitTestsOptions);

            string response = await chat.GetResponseFromChatbotAsync();

            twd.EndWaitDialog();

            if (typeof(T).Name == "Explain")
            {
                await VS.MessageBox.ShowAsync(response, buttons: OLEMSGBUTTON.OLEMSGBUTTON_OK);
            }
            else if (typeof(T).Name == "AddUnitTests" || typeof(T).Name == "CodeIt")
            {
                docView.TextBuffer.Insert(selection.End, response);
            }
            else
            {
                docView.TextBuffer.Replace(selection, response);
            }

            if (generalOptions.FormatDocument)
            {
                (await VS.GetServiceAsync<DTE, DTE>()).ExecuteCommand("Edit.FormatDocument", string.Empty);
            }
        }

        private void AddSettingsInputs(Conversation chat, Commands commandsOptions, UnitTests unitTestsOptions)
        {
            switch (typeof(T).Name)
            {
                case "AddSummary":
                    chat.AppendUserInput(commandsOptions.AddSummary);
                    break;
                case "AddComments":
                    chat.AppendUserInput(commandsOptions.AddComments);
                    break;
                case "Refactor":
                    chat.AppendUserInput(commandsOptions.Refactor);
                    break;
                case "Explain":
                    chat.AppendUserInput(commandsOptions.Explain);
                    break;
                case "CodeIt":
                    chat.AppendUserInput(commandsOptions.CodeIt);
                    break;
                case "AddUnitTests":
                    chat.AppendExampleChatbotOutput(unitTestsOptions.UnitTestingFramework.GetEnumDescription());
                    chat.AppendExampleChatbotOutput(unitTestsOptions.IsolationFramework.GetEnumDescription());
                    chat.AppendExampleChatbotOutput(unitTestsOptions.TestDataFramework.GetEnumDescription());
                    chat.AppendExampleChatbotOutput(unitTestsOptions.FluentAssertionFramework.GetEnumDescription());
                    if (!string.IsNullOrEmpty(unitTestsOptions.Others))
                    {
                        chat.AppendUserInput(unitTestsOptions.Others);
                    }
                    break;
            }
        }
    }
}
