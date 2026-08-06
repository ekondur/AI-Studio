using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;

namespace AI_Studio
{
    [Command(PackageIds.NewChat)]
    internal sealed class NewChat : BaseCommand<NewChat>
    {
        protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var toolWindow = await Package.FindToolWindowAsync(
                typeof(OutputToolWindow), 0, true, VsShellUtilities.ShutdownToken);

            var windowFrame = (IVsWindowFrame)toolWindow.Frame;
            ErrorHandler.ThrowOnFailure(windowFrame.Show());

            if (toolWindow is OutputToolWindow outputWindow)
                await outputWindow.ResetChatAsync();
        }
    }
}
