using DeviceService;
using Microsoft.Extensions.Logging;
using System;

namespace VirdhiService
{
    public class FormLoggerProvider : ILoggerProvider
    {
        public static StatusForm? ActiveForm { get; set; }

        public ILogger CreateLogger(string categoryName) => new FormLogger(categoryName);

        public void Dispose() { }

        private class FormLogger : ILogger
        {
            private readonly string _category;
            public FormLogger(string category) => _category = category;

            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
                Exception? exception, Func<TState, Exception?, string> formatter)
            {
                var message = formatter(state, exception);
                ActiveForm?.AppendLog($"[{logLevel}] {_category}: {message}");
                if (exception != null)
                    ActiveForm?.AppendLog(exception.ToString());
            }
        }
    }
}