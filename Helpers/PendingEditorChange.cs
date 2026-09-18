using EnvDTE;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.PlatformUI;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace AI_Studio.Helpers
{
    // The destination belongs to the request, never to the currently focused editor.
    internal sealed class PendingEditorChange
    {
        private readonly DocumentView _document;
        private readonly ITrackingSpan _selection;
        private readonly string _originalText;
        private readonly ResponseBehavior _behavior;
        private readonly bool _format;

        internal PendingEditorChange(DocumentView document, SnapshotSpan selection,
            ResponseBehavior behavior, bool format)
        {
            _document = document;
            _selection = selection.Snapshot.CreateTrackingSpan(selection.Span, SpanTrackingMode.EdgeExclusive);
            _originalText = selection.GetText();
            _behavior = behavior;
            _format = format;
        }

        internal string Response { get; set; }
        internal bool IsApplied { get; private set; }
        internal string ActionLabel => _behavior == ResponseBehavior.Insert ? "Insert into Editor" : "Replace Selection";

        internal async Task PreviewAndApplyAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            if (IsApplied || string.IsNullOrWhiteSpace(Response))
                return;
            if (_document.TextView.IsClosed)
                throw new InvalidOperationException("The original editor was closed. Run the command again to apply this change.");

            var snapshot = _document.TextBuffer.CurrentSnapshot;
            var selection = _selection.GetSpan(snapshot);
            if (selection.GetText() != _originalText)
                throw new InvalidOperationException("The original selection has changed. Run the command again to avoid overwriting your edits.");

            var newline = snapshot.Lines.Select(line => line.GetLineBreakText()).FirstOrDefault(value => value.Length > 0)
                ?? Environment.NewLine;
            var response = Response.Replace("\r\n", "\n").Replace("\n", newline);
            var replacement = _behavior == ResponseBehavior.Insert ? _originalText + newline + response : response;
            var preview = new DialogWindow
            {
                Title = "AI Studio — Preview changes",
                Width = 900,
                Height = 600,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                ResizeMode = ResizeMode.CanResize
            };
            var panel = new DockPanel { Margin = new Thickness(12) };
            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var apply = new Button { Content = "Apply", MinWidth = 80, Margin = new Thickness(8), IsDefault = true };
            var cancel = new Button { Content = "Cancel", MinWidth = 80, Margin = new Thickness(8), IsCancel = true };
            apply.Click += (sender, args) => preview.DialogResult = true;
            buttons.Children.Add(apply);
            buttons.Children.Add(cancel);
            DockPanel.SetDock(buttons, Dock.Bottom);
            panel.Children.Add(buttons);
            var fileName = _document.FilePath ?? "Original document";
            var label = new TextBlock { Text = fileName + "\n− Removed   + Added", Margin = new Thickness(0, 0, 0, 8) };
            DockPanel.SetDock(label, Dock.Top);
            panel.Children.Add(label);
            panel.Children.Add(new TextBox
            {
                Text = CreateDiff(_originalText, replacement),
                IsReadOnly = true,
                FontFamily = new FontFamily("Consolas"),
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto
            });
            preview.Content = panel;
            if (preview.ShowModal() != true)
                return;

            // A modal preview can pump editor events. Recheck before committing.
            if (_document.TextView.IsClosed || _document.TextBuffer.CurrentSnapshot != snapshot)
                throw new InvalidOperationException("The document changed while the preview was open. Review the change again.");

            var editSpan = _behavior == ResponseBehavior.Insert ? new Span(selection.End.Position, 0) : selection.Span;
            var editText = _behavior == ResponseBehavior.Insert ? newline + response : response;
            using (var edit = _document.TextBuffer.CreateEdit())
            {
                if (!edit.Replace(editSpan, editText))
                    throw new InvalidOperationException("The original selection is read-only.");
                edit.Apply();
                if (edit.Canceled)
                    throw new InvalidOperationException("The editor could not apply the change.");
            }
            IsApplied = true;
            var appliedSpan = new SnapshotSpan(_document.TextBuffer.CurrentSnapshot, editSpan.Start, editText.Length);
            _document.TextView.Selection.Select(appliedSpan, false);
            if (_format && !string.IsNullOrEmpty(_document.FilePath))
            {
                await VS.Documents.OpenAsync(_document.FilePath);
                var active = await VS.Documents.GetActiveDocumentViewAsync();
                if (active?.TextBuffer == _document.TextBuffer)
                    (await VS.GetServiceAsync<DTE, DTE>()).ExecuteCommand("Edit.FormatSelection");
            }
        }

        // Keep unchanged prefix/suffix lines as context; mark the changed section.
        internal static string CreateDiff(string before, string after)
        {
            var oldLines = before.Replace("\r\n", "\n").Split('\n');
            var newLines = after.Replace("\r\n", "\n").Split('\n');
            var prefix = 0;
            while (prefix < oldLines.Length && prefix < newLines.Length && oldLines[prefix] == newLines[prefix]) prefix++;
            var suffix = 0;
            while (suffix < oldLines.Length - prefix && suffix < newLines.Length - prefix
                && oldLines[oldLines.Length - suffix - 1] == newLines[newLines.Length - suffix - 1]) suffix++;
            var result = new StringBuilder();
            for (var i = 0; i < prefix; i++) result.AppendLine("  " + oldLines[i]);
            for (var i = prefix; i < oldLines.Length - suffix; i++) result.AppendLine("- " + oldLines[i]);
            for (var i = prefix; i < newLines.Length - suffix; i++) result.AppendLine("+ " + newLines[i]);
            for (var i = newLines.Length - suffix; i < newLines.Length; i++) result.AppendLine("  " + newLines[i]);
            return result.ToString();
        }
    }
}
