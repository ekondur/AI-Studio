using Markdig;
using Markdown.ColorCode;
using Microsoft.Extensions.AI;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.ClientModel;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace AI_Studio
{
    /// <summary>
    /// Interaction logic for OutputToolWindowControl.xaml
    /// </summary>
    public partial class OutputToolWindowControl : UserControl
    {
        private readonly MarkdownPipeline _markdownPipeline;
        private readonly List<ChatMessage> _conversation = new();
        private bool _isSending;
        private bool _isBrowserReady;
        private string _pendingInnerHtml = string.Empty;

        public OutputToolWindowControl()
        {
            InitializeComponent();

            _markdownPipeline = new MarkdownPipelineBuilder()
               .UseAdvancedExtensions()
               .UseColorCode()
               .Build();

            SendButton.Click += OnSendButtonClick;
            PromptInput.KeyDown += PromptInput_KeyDown;
            ResponseBrowser.LoadCompleted += ResponseBrowser_LoadCompleted;

            // Prime the browser once; later updates reuse the same document to avoid focus jumps.
            ResponseBrowser.NavigateToString(BuildPageHtml(string.Empty));
        }

        private void ResponseBrowser_LoadCompleted(object sender, System.Windows.Navigation.NavigationEventArgs e)
        {
            _isBrowserReady = true;
            if (!string.IsNullOrEmpty(_pendingInnerHtml))
            {
                ApplyContent(_pendingInnerHtml);
            }
        }

        private void OnSendButtonClick(object sender, RoutedEventArgs e)
        {
            _ = ThreadHelper.JoinableTaskFactory.RunAsync(HandleSendAsync);
        }

        public async Task UpdateContentAsync(string content)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var safeContent = string.IsNullOrWhiteSpace(content) ? "(No response returned.)" : content;
            _conversation.Clear();
            _conversation.Add(new ChatMessage(ChatRole.Assistant, safeContent));

            await RenderConversationAsync();
        }

        private async Task HandleSendAsync()
        {
            if (_isSending)
            {
                return;
            }

            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            var userMessage = PromptInput.Text.Trim();
            if (string.IsNullOrWhiteSpace(userMessage))
            {
                return;
            }

            _isSending = true;
            PromptInput.Text = string.Empty;
            PromptInput.IsEnabled = false;
            SendButton.IsEnabled = false;

            _conversation.Add(new ChatMessage(ChatRole.User, userMessage));
            await RenderConversationAsync();

            try
            {
                var generalOptions = await General.GetLiveInstanceAsync();

                if (string.IsNullOrWhiteSpace(generalOptions.ApiKey))
                {
                    await VS.MessageBox.ShowAsync("Add an API key in Tools > Options > AI Studio > General before continuing.",
                        buttons: OLEMSGBUTTON.OLEMSGBUTTON_OK);
                    return;
                }

                IChatClient client = new OpenAI.Chat.ChatClient(
                    model: generalOptions.LanguageModel,
                    credential: new ApiKeyCredential(generalOptions.ApiKey),
                    options: new OpenAI.OpenAIClientOptions
                    {
                        Endpoint = new Uri(generalOptions.ApiEndpoint)
                    }
                ).AsIChatClient();

                var requestMessages = new List<ChatMessage>
                {
                    new ChatMessage(ChatRole.System, "You are AI Studio inside Visual Studio. Respond with concise, markdown-formatted answers suited for developers.")
                };
                requestMessages.AddRange(_conversation);

                var responseBuilder = new StringBuilder();

                await foreach (var update in client.GetStreamingResponseAsync(requestMessages))
                {
                    if (string.IsNullOrEmpty(update?.Text))
                    {
                        continue;
                    }

                    responseBuilder.Append(update.Text);
                    await RenderConversationAsync(responseBuilder.ToString());
                }

                var assistantMessage = responseBuilder.Length == 0
                    ? "(No response returned.)"
                    : responseBuilder.ToString();

                _conversation.Add(new ChatMessage(ChatRole.Assistant, assistantMessage));
                await RenderConversationAsync();
            }
            catch (Exception ex)
            {
                await VS.MessageBox.ShowAsync(ex.Message, buttons: OLEMSGBUTTON.OLEMSGBUTTON_OK);
            }
            finally
            {
                _isSending = false;
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                PromptInput.IsEnabled = true;
                SendButton.IsEnabled = true;
                PromptInput.Focus();
            }
        }

        private void PromptInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.None)
            {
                e.Handled = true;
                _ = HandleSendAsync();
            }
        }

        private async Task RenderConversationAsync(string streamingAssistant = null)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var bodyBuilder = new StringBuilder();
            foreach (var message in _conversation)
            {
                var roleClass = message.Role == ChatRole.User ? "user" : "assistant";
                var roleName = message.Role == ChatRole.User ? "You" : "AI";
                var html = Markdig.Markdown.ToHtml(message.Text ?? string.Empty, _markdownPipeline);
                bodyBuilder.AppendLine($"<div class=\"message {roleClass}\"><div class=\"role\">{roleName}</div><div class=\"content\">{html}</div></div>");
            }

            if (!string.IsNullOrEmpty(streamingAssistant))
            {
                var streamingHtml = Markdig.Markdown.ToHtml(streamingAssistant, _markdownPipeline);
                bodyBuilder.AppendLine($"<div class=\"message assistant live\"><div class=\"role\">AI</div><div class=\"content\">{streamingHtml}</div></div>");
            }

            UpdateBrowserContent(bodyBuilder.ToString());
        }

        private void UpdateBrowserContent(string innerHtml)
        {
            _pendingInnerHtml = innerHtml;

            if (!_isBrowserReady || ResponseBrowser.Document == null)
            {
                ResponseBrowser.NavigateToString(BuildPageHtml(innerHtml));
                return;
            }

            ApplyContent(innerHtml);
        }

        private void ApplyContent(string innerHtml)
        {
            try
            {
                dynamic doc = ResponseBrowser.Document;
                dynamic window = doc?.parentWindow;

                if (window?.setContent != null)
                {
                    window.setContent(innerHtml);
                    return;
                }

                dynamic log = doc?.getElementById("log");
                if (log != null)
                {
                    log.innerHTML = innerHtml;
                    window?.scrollTo(0, doc?.body?.scrollHeight ?? 0);
                    return;
                }
            }
            catch
            {
                // Fallback to navigation below.
            }

            ResponseBrowser.NavigateToString(BuildPageHtml(innerHtml));
        }

        private string BuildPageHtml(string innerContent)
        {
            return $@"<!DOCTYPE html>
<html>
<head>
    <meta charset=""utf-8"" />
    <style>
        :root {{
            color-scheme: light dark;
        }}
        body {{
            margin: 0;
            padding: 0;
            font-family: 'Segoe UI', 'Helvetica Neue', Arial, sans-serif;
            font-size: 14px;
            line-height: 1.5;
            background: #1b1b1d;
            color: #f3f3f3;
        }}
        .container {{
            padding: 12px 16px 20px 16px;
            max-width: 1100px;
            margin: 0 auto;
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
            .message {{
                background: #f5f7fb;
                border-color: #d0d7de;
            }}
            .message.user {{
                background: #e8f1ff;
                border-color: #b7cff9;
            }}
        }}
        .message {{
            margin: 0 0 12px 0;
            padding: 10px 12px;
            border-radius: 8px;
            border: 1px solid #3c3c3c;
            background: #232323;
            box-shadow: 0 1px 2px rgba(0,0,0,0.35);
        }}
        .message.user {{
            background: #11385a;
            border-color: #2d5f8a;
        }}
        .message.assistant.live {{
            opacity: 0.9;
        }}
        .role {{
            font-size: 11px;
            letter-spacing: 0.03em;
            text-transform: uppercase;
            color: #9ca3af;
            margin-bottom: 6px;
        }}
        .content p {{
            margin: 0 0 0.75em 0;
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
    </style>
</head>
<body>
<div id=""log"" class=""container"">
{innerContent}
</div>
<script>
    function setContent(html) {{
        var log = document.getElementById('log');
        if (!log) return;
        log.innerHTML = html;
        window.scrollTo(0, document.body.scrollHeight);
    }}
</script>
</body>
</html>";
        }
    }
}
