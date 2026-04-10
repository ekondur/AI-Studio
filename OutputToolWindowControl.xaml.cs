using AI_Studio.Helpers;
using Microsoft.Extensions.AI;
using Microsoft.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.Shell.Interop;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using Microsoft.VisualStudio.Threading;

namespace AI_Studio
{
    public partial class OutputToolWindowControl : UserControl
    {
        private enum ViewState { Empty, Content }

        private readonly List<ChatMessage> _conversation = new List<ChatMessage>();
        private bool _isSending;
        private bool _showLoadingBubble;
        private Border _streamingBubble;
        private DateTime _lastStreamRenderTime = DateTime.MinValue;
        private const int StreamRenderThrottleMs = 80;

        // VS theme brushes — resolved once after the control is loaded
        private Brush _textBrush;
        private Brush _windowBrush;

        public OutputToolWindowControl()
        {
            InitializeComponent();
            Loaded += OnLoaded;
            SendButton.Click += OnSendButtonClick;
            PromptInput.KeyDown += PromptInput_KeyDown;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            RefreshThemeBrushes();
            VSColorTheme.ThemeChanged += OnVsThemeChanged;
            Unloaded += OnUnloaded;
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            VSColorTheme.ThemeChanged -= OnVsThemeChanged;
        }

        private void RefreshThemeBrushes()
        {
            _textBrush   = TryFindResource("VsBrush.WindowText") as Brush ?? Brushes.WhiteSmoke;
            _windowBrush = TryFindResource("VsBrush.Window")     as Brush ?? Brushes.Transparent;
        }

        private void OnVsThemeChanged(ThemeChangedEventArgs e)
        {
            _ = ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                RefreshThemeBrushes();
                RebuildPanel();
            });
        }

        private bool IsDarkTheme()
        {
            if (_windowBrush is SolidColorBrush scb)
            {
                var c = scb.Color;
                double lum = 0.2126 * c.R + 0.7152 * c.G + 0.0722 * c.B;
                return lum < 128;
            }
            return true; // default to dark
        }

        // ── Public API (called from AIBaseCommand) ────────────────────────────

        public async Task BeginStreamingAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            _conversation.Clear();
            _showLoadingBubble = true;
            _streamingBubble = null;
            RebuildPanel();
        }

        public async Task UpdateContentAsync(string content, bool isStreaming = false)
        {
            if (isStreaming)
            {
                var elapsed = (DateTime.UtcNow - _lastStreamRenderTime).TotalMilliseconds;
                if (elapsed < StreamRenderThrottleMs)
                    return;
            }

            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            _showLoadingBubble = false;
            var safeContent = string.IsNullOrWhiteSpace(content) ? "(No response returned.)" : content;
            _conversation.Clear();
            _conversation.Add(new ChatMessage(ChatRole.Assistant, safeContent));
            _streamingBubble = null;
            RebuildPanel();
        }

        // ── Chat send ─────────────────────────────────────────────────────────

        private void OnSendButtonClick(object sender, RoutedEventArgs e)
        {
            _ = ThreadHelper.JoinableTaskFactory.RunAsync(HandleSendAsync);
        }

        private void PromptInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.None)
            {
                e.Handled = true;
                _ = ThreadHelper.JoinableTaskFactory.RunAsync(HandleSendAsync);
            }
        }

        private async Task HandleSendAsync()
        {
            if (_isSending)
                return;

            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var userMessage = PromptInput.Text.Trim();
            if (string.IsNullOrWhiteSpace(userMessage))
                return;

            _isSending = true;
            PromptInput.Text = string.Empty;
            PromptInput.IsEnabled = false;
            SendButton.Content = "•••";
            SendButton.IsEnabled = false;

            _conversation.Add(new ChatMessage(ChatRole.User, userMessage));
            _showLoadingBubble = true;
            _streamingBubble = null;
            RebuildPanel();

            try
            {
                var generalOptions = await General.GetLiveInstanceAsync();

                if (ChatClientFactory.RequiresApiKey(generalOptions.Provider) && string.IsNullOrWhiteSpace(generalOptions.ApiKey))
                {
                    await VS.MessageBox.ShowAsync(
                        $"Add an API key for {generalOptions.Provider} in Tools > Options > AI Studio > General.",
                        buttons: OLEMSGBUTTON.OLEMSGBUTTON_OK);
                    return;
                }

                IChatClient client = ChatClientFactory.Create(generalOptions);

                var requestMessages = new List<ChatMessage>
                {
                    new ChatMessage(ChatRole.System,
                        "You are AI Studio inside Visual Studio. Respond with concise, markdown-formatted answers suited for developers.")
                };
                requestMessages.AddRange(_conversation);

                var responseBuilder = new StringBuilder();

                await foreach (var update in client.GetStreamingResponseAsync(requestMessages))
                {
                    if (string.IsNullOrEmpty(update?.Text))
                        continue;

                    responseBuilder.Append(update.Text);
                    await UpdateStreamingBubbleAsync(responseBuilder.ToString());
                }

                var assistantMessage = responseBuilder.Length == 0
                    ? "(No response returned.)"
                    : responseBuilder.ToString();

                _conversation.Add(new ChatMessage(ChatRole.Assistant, assistantMessage));
                _streamingBubble = null;
                _showLoadingBubble = false;
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                RebuildPanel();
            }
            catch (Exception ex)
            {
                await VS.MessageBox.ShowAsync(ex.Message, buttons: OLEMSGBUTTON.OLEMSGBUTTON_OK);
            }
            finally
            {
                _isSending = false;
                _showLoadingBubble = false;
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                SetViewState(_conversation.Count > 0 ? ViewState.Content : ViewState.Empty);
                PromptInput.IsEnabled = true;
                SendButton.Content = "Send";
                SendButton.IsEnabled = true;
                PromptInput.Focus();
            }
        }

        // ── Streaming bubble ──────────────────────────────────────────────────

        private async Task UpdateStreamingBubbleAsync(string markdown)
        {
            var elapsed = (DateTime.UtcNow - _lastStreamRenderTime).TotalMilliseconds;
            if (elapsed < StreamRenderThrottleMs)
                return;

            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            _lastStreamRenderTime = DateTime.UtcNow;
            _showLoadingBubble = false;

            if (_streamingBubble == null)
            {
                RebuildPanel(streamingMarkdown: markdown);
            }
            else
            {
                UpdateStreamingBubbleContent(markdown);
                ScrollToBottom();
            }
        }

        private void UpdateStreamingBubbleContent(string markdown)
        {
            if (_streamingBubble?.Child is StackPanel sp && sp.Children.Count >= 2)
            {
                var contentPanel = sp.Children[1] as StackPanel;
                if (contentPanel != null)
                {
                    contentPanel.Children.Clear();
                    RenderMarkdownInto(contentPanel, markdown);
                }
            }
        }

        // ── Panel rendering ───────────────────────────────────────────────────

        private void RebuildPanel(string streamingMarkdown = null)
        {
            ConversationPanel.Children.Clear();
            _streamingBubble = null;

            foreach (var msg in _conversation)
            {
                var bubble = CreateMessageBubble(msg.Role == ChatRole.User, msg.Text ?? string.Empty);
                ConversationPanel.Children.Add(bubble);
            }

            if (!string.IsNullOrEmpty(streamingMarkdown))
            {
                _showLoadingBubble = false;
                _streamingBubble = CreateMessageBubble(isUser: false, markdown: streamingMarkdown, isStreaming: true);
                ConversationPanel.Children.Add(_streamingBubble);
                SetViewState(ViewState.Content);
            }
            else if (_showLoadingBubble)
            {
                ConversationPanel.Children.Add(CreateLoadingBubble());
                SetViewState(ViewState.Content);
            }
            else
            {
                SetViewState(_conversation.Count > 0 ? ViewState.Content : ViewState.Empty);
            }

            ScrollToBottom();
        }

        private Border CreateMessageBubble(bool isUser, string markdown, bool isStreaming = false)
        {
            bool dark = IsDarkTheme();
            var mutedColor    = dark ? Color.FromRgb(0x9c, 0xa3, 0xaf) : Color.FromRgb(0x6b, 0x72, 0x80);
            var copyBorderColor = dark ? Color.FromRgb(0x55, 0x55, 0x55) : Color.FromRgb(0xd1, 0xd5, 0xdb);

            var roleLabel = new TextBlock
            {
                Text = isUser ? "You" : "AI",
                FontSize = 10,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(mutedColor),
                VerticalAlignment = VerticalAlignment.Center,
            };

            // Copy button next to the role label
            var copyButton = new Button
            {
                Content = "Copy",
                FontSize = 10,
                Padding = new Thickness(6, 1, 6, 1),
                Cursor = Cursors.Hand,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(1),
                BorderBrush = new SolidColorBrush(copyBorderColor),
                Foreground = new SolidColorBrush(mutedColor),
                VerticalAlignment = VerticalAlignment.Center,
                Focusable = false,
            };
            var capturedMarkdown = markdown;
            copyButton.Click += (s, e) => Clipboard.SetText(capturedMarkdown);

            // Header row: role label on the left, copy button on the right
            var headerGrid = new Grid { Margin = new Thickness(0, 0, 0, 5) };
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            Grid.SetColumn(roleLabel, 0);
            Grid.SetColumn(copyButton, 1);
            headerGrid.Children.Add(roleLabel);
            headerGrid.Children.Add(copyButton);

            var contentPanel = new StackPanel();
            RenderMarkdownInto(contentPanel, markdown);

            var innerStack = new StackPanel();
            innerStack.Children.Add(headerGrid);
            innerStack.Children.Add(contentPanel);

            // Right-click context menu
            var contextMenu = new ContextMenu();
            var copyMenuItem = new MenuItem { Header = "Copy" };
            copyMenuItem.Click += (s, e) => Clipboard.SetText(capturedMarkdown);
            contextMenu.Items.Add(copyMenuItem);

            return new Border
            {
                Child = innerStack,
                CornerRadius = new CornerRadius(8),
                Background = isUser
                    ? new SolidColorBrush(dark ? Color.FromRgb(0x1c, 0x45, 0x6b) : Color.FromRgb(0xdb, 0xe4, 0xfc))
                    : new SolidColorBrush(dark ? Color.FromRgb(0x2d, 0x2d, 0x30) : Color.FromRgb(0xf3, 0xf4, 0xf6)),
                BorderBrush = isUser
                    ? new SolidColorBrush(dark ? Color.FromRgb(0x2d, 0x5f, 0x8a) : Color.FromRgb(0x93, 0xc5, 0xfd))
                    : new SolidColorBrush(dark ? Color.FromRgb(0x3c, 0x3c, 0x3c) : Color.FromRgb(0xd1, 0xd5, 0xdb)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(12, 10, 12, 10),
                Margin = new Thickness(0, 0, 0, 10),
                Opacity = isStreaming ? 0.9 : 1.0,
                ContextMenu = contextMenu,
            };
        }

        private UIElement CreateLoadingBubble()
        {
            bool dark = IsDarkTheme();
            var mutedColor = dark ? Color.FromRgb(0x9c, 0xa3, 0xaf) : Color.FromRgb(0x6b, 0x72, 0x80);

            var dotsPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 2) };

            for (int i = 0; i < 3; i++)
            {
                var dot = new Ellipse
                {
                    Width = 7,
                    Height = 7,
                    Fill = new SolidColorBrush(mutedColor),
                    Margin = new Thickness(2, 0, 2, 0),
                    Opacity = 0.3,
                };

                var anim = new DoubleAnimation
                {
                    From = 0.3,
                    To = 1.0,
                    Duration = TimeSpan.FromMilliseconds(600),
                    AutoReverse = true,
                    RepeatBehavior = RepeatBehavior.Forever,
                    BeginTime = TimeSpan.FromMilliseconds(i * 160),
                };
                dot.BeginAnimation(OpacityProperty, anim);
                dotsPanel.Children.Add(dot);
            }

            var roleLabel = new TextBlock
            {
                Text = "AI",
                FontSize = 10,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(mutedColor),
                Margin = new Thickness(0, 0, 0, 5),
            };

            var inner = new StackPanel();
            inner.Children.Add(roleLabel);
            inner.Children.Add(dotsPanel);

            return new Border
            {
                Child = inner,
                CornerRadius = new CornerRadius(8),
                Background = new SolidColorBrush(dark ? Color.FromRgb(0x2d, 0x2d, 0x30) : Color.FromRgb(0xf3, 0xf4, 0xf6)),
                BorderBrush = new SolidColorBrush(dark ? Color.FromRgb(0x3c, 0x3c, 0x3c) : Color.FromRgb(0xd1, 0xd5, 0xdb)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(12, 10, 12, 10),
                Margin = new Thickness(0, 0, 0, 10),
            };
        }

        // ── Markdown → WPF ────────────────────────────────────────────────────

        // Matches fenced code blocks: ```[lang]\n...\n```
        private static readonly Regex _codeBlockRegex = new Regex(
            @"```[^\n]*\n([\s\S]*?)```", RegexOptions.Compiled);

        // Matches **bold**, `inline code`, *italic*
        private static readonly Regex _inlineRegex = new Regex(
            @"\*\*(.+?)\*\*|`([^`]+)`|\*(.+?)\*", RegexOptions.Compiled);

        private void RenderMarkdownInto(StackPanel panel, string markdown)
        {
            if (string.IsNullOrEmpty(markdown)) return;

            // Split on fenced code blocks; capture groups are interleaved in result
            var parts = _codeBlockRegex.Split(markdown);

            for (int i = 0; i < parts.Length; i++)
            {
                if (i % 2 == 0)
                    RenderTextSection(panel, parts[i]);
                else
                    RenderCodeBlock(panel, parts[i]);
            }
        }

        private void RenderTextSection(StackPanel panel, string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;

            var lines = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            var buffer = new List<string>();

            foreach (var line in lines)
            {
                if (line.StartsWith("### "))
                {
                    FlushBuffer(panel, buffer);
                    panel.Children.Add(MakeHeading(line.Substring(4), 14, FontWeights.SemiBold));
                }
                else if (line.StartsWith("## "))
                {
                    FlushBuffer(panel, buffer);
                    panel.Children.Add(MakeHeading(line.Substring(3), 16, FontWeights.Bold));
                }
                else if (line.StartsWith("# "))
                {
                    FlushBuffer(panel, buffer);
                    panel.Children.Add(MakeHeading(line.Substring(2), 18, FontWeights.Bold));
                }
                else if (line.StartsWith("- ") || line.StartsWith("* "))
                {
                    FlushBuffer(panel, buffer);
                    panel.Children.Add(MakeBulletRow(line.Substring(2)));
                }
                else
                {
                    buffer.Add(line);
                }
            }

            FlushBuffer(panel, buffer);
        }

        private void FlushBuffer(StackPanel panel, List<string> buffer)
        {
            if (buffer.Count == 0) return;

            // Trim surrounding blank lines
            int s = 0, e = buffer.Count - 1;
            while (s <= e && string.IsNullOrWhiteSpace(buffer[s])) s++;
            while (e >= s && string.IsNullOrWhiteSpace(buffer[e])) e--;

            if (s <= e)
            {
                var joined = string.Join("\n", buffer.GetRange(s, e - s + 1));
                if (!string.IsNullOrWhiteSpace(joined))
                {
                    var tb = MakeInlineTextBlock(joined);
                    tb.Margin = new Thickness(0, 0, 0, 6);
                    panel.Children.Add(tb);
                }
            }

            buffer.Clear();
        }

        private FrameworkElement MakeHeading(string text, double size, FontWeight weight)
        {
            return MakeSelectableRtb(text, fontSize: size, fontWeight: weight,
                                     margin: new Thickness(0, 8, 0, 4));
        }

        private UIElement MakeBulletRow(string text)
        {
            var grid = new Grid { Margin = new Thickness(0, 0, 0, 2) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var bullet = new TextBlock
            {
                Text = "•",
                Foreground = _textBrush ?? Brushes.WhiteSmoke,
                VerticalAlignment = VerticalAlignment.Top,
            };
            Grid.SetColumn(bullet, 0);

            var content = MakeInlineTextBlock(text);
            Grid.SetColumn(content, 1);

            grid.Children.Add(bullet);
            grid.Children.Add(content);
            return grid;
        }

        private void RenderCodeBlock(StackPanel panel, string code)
        {
            bool dark = IsDarkTheme();
            var codeTextColor  = dark ? Color.FromRgb(0xf3, 0xf3, 0xf3) : Color.FromRgb(0x1f, 0x29, 0x37);
            var codeBgColor    = dark ? Color.FromRgb(0x1e, 0x1e, 0x1e) : Color.FromRgb(0xf0, 0xf0, 0xf0);
            var copyBgColor    = dark ? Color.FromArgb(200, 0x3c, 0x3c, 0x3c) : Color.FromArgb(200, 0xe5, 0xe7, 0xeb);
            var copyBorderColor = dark ? Color.FromRgb(0x55, 0x55, 0x55) : Color.FromRgb(0xd1, 0xd5, 0xdb);
            var mutedColor     = dark ? Color.FromRgb(0x9c, 0xa3, 0xaf) : Color.FromRgb(0x6b, 0x72, 0x80);

            var codeText = code.TrimEnd('\r', '\n');

            var tb = new TextBox
            {
                Text = codeText,
                FontFamily = new FontFamily("Consolas, Courier New"),
                FontSize = 12,
                Foreground = new SolidColorBrush(codeTextColor),
                TextWrapping = TextWrapping.Wrap,
                Padding = new Thickness(10),
                IsReadOnly = true,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                IsTabStop = false,
            };

            var copyBtn = new Button
            {
                Content = "Copy",
                FontSize = 10,
                Padding = new Thickness(6, 2, 6, 2),
                Cursor = Cursors.Hand,
                Background = new SolidColorBrush(copyBgColor),
                BorderThickness = new Thickness(1),
                BorderBrush = new SolidColorBrush(copyBorderColor),
                Foreground = new SolidColorBrush(mutedColor),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 6, 6, 0),
                Focusable = false,
            };
            copyBtn.Click += (s, e) =>
            {
                _ = ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
                {
                    Clipboard.SetText(codeText);
                    copyBtn.Content = "Copied!";
                    await Task.Delay(1500);
                    copyBtn.Content = "Copy";
                });
            };

            var overlay = new Grid();
            overlay.Children.Add(tb);
            overlay.Children.Add(copyBtn);

            panel.Children.Add(new Border
            {
                Child = overlay,
                Background = new SolidColorBrush(codeBgColor),
                CornerRadius = new CornerRadius(4),
                Margin = new Thickness(0, 4, 0, 6),
            });
        }

        private FrameworkElement MakeInlineTextBlock(string text)
        {
            return MakeSelectableRtb(text, fontSize: 13);
        }

        // Creates a read-only, transparent RichTextBox that supports text selection
        // while preserving inline formatting (bold, italic, inline code).
        private RichTextBox MakeSelectableRtb(string text, double fontSize = 13,
                                              FontWeight? fontWeight = null,
                                              Thickness? margin = null)
        {
            var para = new Paragraph { Margin = new Thickness(0) };
            AddInlines(para.Inlines, text);

            var doc = new FlowDocument(para) { PagePadding = new Thickness(0) };

            var rtb = new RichTextBox(doc)
            {
                IsReadOnly = true,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(0),
                FontSize = fontSize,
                Foreground = _textBrush ?? Brushes.WhiteSmoke,
                VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                IsTabStop = false,
                Margin = margin ?? new Thickness(0),
            };

            if (fontWeight.HasValue)
                rtb.FontWeight = fontWeight.Value;

            return rtb;
        }

        private void AddInlines(InlineCollection inlines, string text)
        {
            int last = 0;
            foreach (Match m in _inlineRegex.Matches(text))
            {
                if (m.Index > last)
                    inlines.Add(new Run(text.Substring(last, m.Index - last)));

                if (m.Groups[1].Success)                    // **bold**
                {
                    inlines.Add(new Bold(new Run(m.Groups[1].Value)));
                }
                else if (m.Groups[2].Success)               // `inline code`
                {
                    inlines.Add(new Run(m.Groups[2].Value)
                    {
                        FontFamily = new FontFamily("Consolas, Courier New"),
                        FontSize = 12,
                        Background = IsDarkTheme()
                            ? new SolidColorBrush(Color.FromArgb(50, 255, 255, 255))
                            : new SolidColorBrush(Color.FromArgb(40, 0, 0, 0)),
                    });
                }
                else if (m.Groups[3].Success)               // *italic*
                {
                    inlines.Add(new Italic(new Run(m.Groups[3].Value)));
                }

                last = m.Index + m.Length;
            }

            if (last < text.Length)
                inlines.Add(new Run(text.Substring(last)));
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private void SetViewState(ViewState state)
        {
            EmptyStatePanel.Visibility = state == ViewState.Empty ? Visibility.Visible : Visibility.Collapsed;
        }

        private void ScrollToBottom()
        {
            ConversationScroll.UpdateLayout();
            ConversationScroll.ScrollToBottom();
        }
    }
}
