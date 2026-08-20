using System;

namespace Samba.Presentation.Common.Services
{
    public enum ToastType
    {
        Info,
        Success,
        Warning,
        Error
    }

    public class ToastArgs
    {
        public string Message { get; set; }
        public ToastType Type { get; set; }
        public int DurationMs { get; set; }
    }

    public static class ToastService
    {
        public static event Action<ToastArgs> ShowRequested;

        public static void Show(string message, ToastType type = ToastType.Info, int durationMs = 3000)
        {
            var handler = ShowRequested;
            if (handler == null) return;
            handler(new ToastArgs { Message = message, Type = type, DurationMs = durationMs });
        }
    }
}