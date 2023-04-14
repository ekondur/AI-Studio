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
            var userInputs = GetUserInputs(commandsOptions, unitTestsOptions);

            foreach (var userInput in userInputs)
            {
                chat.AppendUserInput(userInput);
            }

            string response = await chat.GetResponseFromChatbotAsync();

            twd.EndWaitDialog();

            if (typeof(T).Name == "Explain")
            {
                await VS.MessageBox.ShowAsync(response, buttons: OLEMSGBUTTON.OLEMSGBUTTON_OK);
            }
            else if (typeof(T).Name == "AddUnitTests")
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

        private IEnumerable<string> GetUserInputs(Commands commandsOptions, UnitTests unitTestsOptions)
        {
            var inputs = new List<string>();

            switch (typeof(T).Name)
            {
                case "AddSummary":
                    inputs.Add(commandsOptions.AddSummary);
                    break;
                case "AddComments":
                    inputs.Add(commandsOptions.AddComments);
                    break;
                case "Refactor":
                    inputs.Add(commandsOptions.Refactor);
                    break;
                case "Explain":
                    inputs.Add(commandsOptions.Explain);
                    break;
                case "AddUnitTests":
                    inputs.Add(unitTestsOptions.UnitTestingFramework.GetEnumDescription());
                    inputs.Add(unitTestsOptions.IsolationFramework.GetEnumDescription());
                    inputs.Add(unitTestsOptions.TestDataFramework.GetEnumDescription());
                    inputs.Add(unitTestsOptions.FluentAssertionFramework.GetEnumDescription());
                    break;
            }

            return inputs;
        }
    }
}
