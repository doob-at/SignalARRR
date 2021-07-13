﻿using Microsoft.Extensions.Logging;

namespace doob.SignalARRR.Client.ExtensionMethods {

    
    public static class LoggerExtensions {

        public static ILoggerProvider AsLoggerProvider(this ILogger logger) {
            return new ExistingLoggerProvider(logger);
        }

        private class ExistingLoggerProvider : ILoggerProvider {
            public ExistingLoggerProvider(ILogger logger) {
                _logger = logger;
            }

            public ILogger CreateLogger(string categoryName) {
                return _logger;
            }

            public void Dispose() {
                return;
            }

            private readonly ILogger _logger;
        }
    }
}
