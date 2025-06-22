using Markdig;
using System.Windows;
using System.Windows.Controls;

namespace AI_Studio
{
    /// <summary>
    /// Interaction logic for OutputToolWindowControl.xaml
    /// </summary>
    public partial class OutputToolWindowControl : UserControl
    {
        private readonly WebBrowser _webBrowser;

        /// <summary>
        /// Initializes a new instance of the <see cref="OutputToolWindowControl"/> class.
        /// </summary>
        public OutputToolWindowControl()
        {
            _webBrowser = new WebBrowser
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };
            this.Content = _webBrowser;
        }

        public async Task UpdateContentAsync(string content)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            // Convert markdown to HTML (using Markdig)
            var html = Markdown.ToHtml(content);

            // Add basic CSS
            var styledHtml = $@"
            <html>
            <head>
                <style>
                    body {{ font-family: 'Segoe UI', Arial; padding: 10px; }}
                    pre {{ background: #f5f5f5; padding: 10px; }}
                    code {{ background: #f5f5f5; }}
                </style>
            </head>
            <body>{html}</body>
            </html>";

            _webBrowser.NavigateToString(styledHtml);
        }
    }
}
