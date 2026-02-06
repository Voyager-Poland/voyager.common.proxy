namespace Voyager.Common.Proxy.Diagnostics.Tests
{
    using System;
    using System.Collections.Generic;
    using FluentAssertions;
    using Microsoft.Extensions.Logging;
    using Xunit;

    /// <summary>
    /// Tests that <see cref="LoggingProxyDiagnostics"/> correctly formats structured log
    /// message templates using the standard Microsoft.Extensions.Logging formatter.
    /// This verifies that named placeholders like {HttpMethod}, {Url} etc. are correctly
    /// replaced with actual values, regardless of the logging provider (not just Serilog).
    /// </summary>
    public class LoggingProxyDiagnosticsTests
    {
        private readonly ListLogger _logger;
        private readonly LoggingProxyDiagnostics _sut;

        public LoggingProxyDiagnosticsTests()
        {
            _logger = new ListLogger();
            _sut = new LoggingProxyDiagnostics(_logger);
        }

        #region OnRequestStarting

        [Fact]
        public void OnRequestStarting_FormatsMessageTemplate_WithActualValues()
        {
            var e = new RequestStartingEvent
            {
                HttpMethod = "GET",
                Url = "/api/users",
                ServiceName = "UserService",
                MethodName = "GetAll",
                TraceId = "abc123",
                SpanId = "def456",
                ParentSpanId = "parent789",
                UserLogin = "admin",
                UnitId = "U1",
                UnitType = "Agent"
            };

            _sut.OnRequestStarting(e);

            _logger.Messages.Should().HaveCount(1);
            var msg = _logger.Messages[0];
            msg.Should().Contain("GET");
            msg.Should().Contain("/api/users");
            msg.Should().Contain("UserService");
            msg.Should().Contain("GetAll");
            msg.Should().Contain("abc123");
            msg.Should().Contain("def456");
            msg.Should().Contain("parent789");
            msg.Should().Contain("admin");
            msg.Should().Contain("U1");
            msg.Should().Contain("Agent");

            // The key assertion: ensure template placeholders are NOT in the output
            msg.Should().NotContain("{HttpMethod}");
            msg.Should().NotContain("{Url}");
            msg.Should().NotContain("{ServiceName}");
            msg.Should().NotContain("{MethodName}");
            msg.Should().NotContain("{TraceId}");
        }

        [Fact]
        public void OnRequestStarting_NullFields_UsesFallbacks()
        {
            var e = new RequestStartingEvent
            {
                HttpMethod = "POST",
                Url = "/api/orders",
                ServiceName = "OrderService",
                MethodName = "Create",
                TraceId = "t1",
                SpanId = "s1",
                ParentSpanId = null,
                UserLogin = null,
                UnitId = null,
                UnitType = null
            };

            _sut.OnRequestStarting(e);

            var msg = _logger.Messages[0];
            msg.Should().Contain("(root)");
            msg.Should().Contain("(anonymous)");
            msg.Should().Contain("(none)");
        }

        #endregion

        #region OnRequestCompleted

        [Fact]
        public void OnRequestCompleted_Success_FormatsCorrectly()
        {
            var e = new RequestCompletedEvent
            {
                HttpMethod = "GET",
                Url = "/api/users/1",
                StatusCode = 200,
                Duration = TimeSpan.FromMilliseconds(150),
                IsSuccess = true,
                ServiceName = "UserService",
                MethodName = "GetById",
                TraceId = "t1",
                SpanId = "s1",
                UserLogin = "admin",
                UnitId = "U1",
                UnitType = "Agent"
            };

            _sut.OnRequestCompleted(e);

            _logger.Messages.Should().HaveCount(1);
            var msg = _logger.Messages[0];
            msg.Should().Contain("200");
            msg.Should().Contain("150");
            msg.Should().Contain("UserService");
            msg.Should().NotContain("{StatusCode}");
            msg.Should().NotContain("{Duration}");
            _logger.Levels[0].Should().Be(LogLevel.Debug);
        }

        [Fact]
        public void OnRequestCompleted_Failure_FormatsErrorFields()
        {
            var e = new RequestCompletedEvent
            {
                HttpMethod = "POST",
                Url = "/api/orders",
                StatusCode = 500,
                Duration = TimeSpan.FromMilliseconds(200),
                IsSuccess = false,
                ServiceName = "OrderService",
                MethodName = "Create",
                TraceId = "t1",
                SpanId = "s1",
                ErrorType = "BusinessError",
                ErrorMessage = "Something went wrong"
            };

            _sut.OnRequestCompleted(e);

            var msg = _logger.Messages[0];
            msg.Should().Contain("500");
            msg.Should().Contain("BusinessError");
            msg.Should().Contain("Something went wrong");
            msg.Should().NotContain("{ErrorType}");
            msg.Should().NotContain("{ErrorMessage}");
            _logger.Levels[0].Should().Be(LogLevel.Warning);
        }

        #endregion

        #region OnRequestFailed

        [Fact]
        public void OnRequestFailed_FormatsExceptionFields()
        {
            var e = new RequestFailedEvent
            {
                HttpMethod = "GET",
                Url = "/api/data",
                Duration = TimeSpan.FromMilliseconds(100),
                ServiceName = "DataService",
                MethodName = "Fetch",
                ExceptionType = "HttpRequestException",
                ExceptionMessage = "Connection refused",
                TraceId = "t1",
                SpanId = "s1"
            };

            _sut.OnRequestFailed(e);

            var msg = _logger.Messages[0];
            msg.Should().Contain("HttpRequestException");
            msg.Should().Contain("Connection refused");
            msg.Should().NotContain("{ExceptionType}");
            msg.Should().NotContain("{ExceptionMessage}");
            _logger.Levels[0].Should().Be(LogLevel.Error);
        }

        #endregion

        #region OnRetryAttempt

        [Fact]
        public void OnRetryAttempt_FormatsRetryDetails()
        {
            var e = new RetryAttemptEvent
            {
                AttemptNumber = 2,
                MaxAttempts = 3,
                ServiceName = "UserService",
                MethodName = "GetAll",
                ErrorType = "Timeout",
                ErrorMessage = "Request timed out",
                Delay = TimeSpan.FromMilliseconds(500),
                TraceId = "t1",
                SpanId = "s1"
            };

            _sut.OnRetryAttempt(e);

            var msg = _logger.Messages[0];
            msg.Should().Contain("2");
            msg.Should().Contain("3");
            msg.Should().Contain("Timeout");
            msg.Should().Contain("Request timed out");
            msg.Should().Contain("500");
            msg.Should().NotContain("{AttemptNumber}");
            msg.Should().NotContain("{MaxAttempts}");
            _logger.Levels[0].Should().Be(LogLevel.Warning);
        }

        #endregion

        #region OnCircuitBreakerStateChanged

        [Fact]
        public void OnCircuitBreakerStateChanged_Open_FormatsCorrectly()
        {
            var e = new CircuitBreakerStateChangedEvent
            {
                ServiceName = "UserService",
                OldState = "Closed",
                NewState = "Open",
                FailureCount = 5,
                LastErrorType = "Timeout",
                LastErrorMessage = "Timed out",
                UserLogin = "admin",
                UnitId = "U1",
                UnitType = "Agent"
            };

            _sut.OnCircuitBreakerStateChanged(e);

            var msg = _logger.Messages[0];
            msg.Should().Contain("UserService");
            msg.Should().Contain("Closed");
            msg.Should().Contain("Open");
            msg.Should().Contain("5");
            msg.Should().NotContain("{ServiceName}");
            msg.Should().NotContain("{OldState}");
            _logger.Levels[0].Should().Be(LogLevel.Warning);
        }

        [Fact]
        public void OnCircuitBreakerStateChanged_Closed_FormatsCorrectly()
        {
            var e = new CircuitBreakerStateChangedEvent
            {
                ServiceName = "UserService",
                OldState = "Open",
                NewState = "Closed",
                FailureCount = 0
            };

            _sut.OnCircuitBreakerStateChanged(e);

            var msg = _logger.Messages[0];
            msg.Should().Contain("UserService");
            msg.Should().Contain("Closed");
            _logger.Levels[0].Should().Be(LogLevel.Information);
        }

        #endregion

        #region ListLogger helper

        /// <summary>
        /// Minimal ILogger that captures formatted messages using the standard formatter delegate.
        /// This simulates what non-Serilog loggers do: they call the formatter function
        /// to produce the final string from the message template + arguments.
        /// </summary>
        private sealed class ListLogger : ILogger<LoggingProxyDiagnostics>
        {
            public List<string> Messages { get; } = new List<string>();
            public List<LogLevel> Levels { get; } = new List<LogLevel>();

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                Levels.Add(logLevel);
                Messages.Add(formatter(state, exception));
            }

            public bool IsEnabled(LogLevel logLevel) => true;

            public IDisposable? BeginScope<TState>(TState state)
                where TState : notnull
                => null;
        }

        #endregion
    }
}
