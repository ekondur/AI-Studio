using Markdig;
using Markdown.ColorCode;
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
        private readonly MarkdownPipeline _markdownPipeline;

        /// <summary>
        /// Initializes a new instance of the <see cref="OutputToolWindowControl"/> class.
        /// </summary>
        public OutputToolWindowControl()
        {
            _markdownPipeline = new MarkdownPipelineBuilder()
               .UseAdvancedExtensions()
               .UseColorCode()
               .Build();
            
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

            var bodyHtml = Markdig.Markdown.ToHtml(content, _markdownPipeline);

            var colorizedHtml = $@"
                <!DOCTYPE html>
                <html>
                <head>
                    <meta charset=""utf-8"" />
                </head>
                <body>
                    {bodyHtml}
                </body>
                </html>";

            _webBrowser.NavigateToString(colorizedHtml);
        }
    }
}
