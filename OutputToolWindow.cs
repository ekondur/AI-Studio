using System.Runtime.InteropServices;

namespace AI_Studio
{
    /// <summary>
    /// This class implements the tool window exposed by this package and hosts a user control.
    /// </summary>
    /// <remarks>
    /// In Visual Studio tool windows are composed of a frame (implemented by the shell) and a pane,
    /// usually implemented by the package implementer.
    /// <para>
    /// This class derives from the ToolWindowPane class provided from the MPF in order to use its
    /// implementation of the IVsUIElementPane interface.
    /// </para>
    /// </remarks>
    [Guid("1f5ba8dc-bc8a-4020-b03c-7ba9e804ae83")]
    public class OutputToolWindow : ToolWindowPane
    {
        private OutputToolWindowControl _control;
        /// <summary>
        /// Initializes a new instance of the <see cref="OutputToolWindow"/> class.
        /// </summary>
        public OutputToolWindow() : base(null)
        {
            this.Caption = "AI Studio Response";
            _control = new OutputToolWindowControl();
            this.Content = _control;
        }

        public async Task UpdateContentAsync(string content)
        {
            await _control.UpdateContentAsync(content);
        }
    }
}
