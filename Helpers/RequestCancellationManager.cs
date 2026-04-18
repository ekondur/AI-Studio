using System.Threading;

namespace AI_Studio.Helpers
{
    /// <summary>
    /// Tracks the currently active AI request so it can be cancelled from
    /// the tool window (Stop button, Escape key) even when the request was
    /// started from a code-editor command.
    /// </summary>
    internal static class RequestCancellationManager
    {
        private static volatile CancellationTokenSource _current;

        public static bool IsActive => _current != null;

        public static CancellationTokenSource Begin()
        {
            _current?.Cancel();
            _current?.Dispose();

            var cts = new CancellationTokenSource();
            _current = cts;
            return cts;
        }

        public static void End(CancellationTokenSource cts)
        {
            if (ReferenceEquals(_current, cts))
                _current = null;
            cts?.Dispose();
        }

        public static void Cancel()
        {
            _current?.Cancel();
        }
    }
}
