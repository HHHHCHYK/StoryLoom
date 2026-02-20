using System;

namespace StoryLoom.Services
{
    public class ToastService
    {
        private readonly LogService _logger;

        public event Action<string>? OnShowToast;

        public ToastService(LogService logger)
        {
            _logger = logger;
        }

        public void ShowToast(string message)
        {
            _logger.Log($"[{nameof(ToastService)}] ShowToast called with message: {message}");
            OnShowToast?.Invoke(message);
        }
    }
}
