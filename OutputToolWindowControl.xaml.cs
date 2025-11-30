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

            var safeContent = content ?? "(No response returned.)";
            var bodyHtml = Markdig.Markdown.ToHtml(safeContent, _markdownPipeline);

            var colorizedHtml = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset=""utf-8"" />
    <style>
        :root {{
            color-scheme: light dark;
        }}
        body {{
            margin: 0;
            padding: 12px 16px;
            font-family: 'Segoe UI', 'Helvetica Neue', Arial, sans-serif;
            font-size: 14px;
            line-height: 1.5;
            background: #1e1e1e;
            color: #f3f3f3;
        }}
        @media (prefers-color-scheme: light) {{
            body {{
                background: #ffffff;
                color: #1e1e1e;
            }}
            pre code {{
                background: #f6f8fa;
                color: #1e1e1e;
            }}
            blockquote {{
                border-left-color: #d0d7de;
            }}
            table, th, td {{
                border-color: #d0d7de;
            }}
        }}
        pre {{
            overflow-x: auto;
        }}
        pre code {{
            display: block;
            padding: 12px;
            border-radius: 6px;
            background: #252526;
            color: #f3f3f3;
            font-family: Consolas, 'Courier New', monospace;
            font-size: 13px;
        }}
        code {{
            font-family: Consolas, 'Courier New', monospace;
            background: rgba(255,255,255,0.08);
            padding: 0 3px;
            border-radius: 3px;
        }}
        blockquote {{
            margin: 0 0 12px 0;
            padding: 8px 12px;
            border-left: 4px solid #3c3c3c;
            background: rgba(255,255,255,0.04);
        }}
        table {{
            border-collapse: collapse;
            margin: 12px 0;
            width: 100%;
        }}
        th, td {{
            border: 1px solid #3c3c3c;
            padding: 6px 8px;
            text-align: left;
        }}
        a {{
            color: #4aa3ff;
            text-decoration: none;
        }}
        a:hover {{
            text-decoration: underline;
        }}
        h1, h2, h3, h4, h5, h6 {{
            margin-top: 1.4em;
            margin-bottom: 0.6em;
        }}
        p {{
            margin: 0 0 0.75em 0;
        }}
    </style>
</head>
<body>
{bodyHtml}
</body>
</html>";

            _webBrowser.NavigateToString(colorizedHtml);
        }
    }
}
