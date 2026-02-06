namespace Voyager.Common.Proxy.Diagnostics.Tests
{
    using System;
    using System.IO;
    using FluentAssertions;
    using Xunit;

    public class ConsoleProxyDiagnosticsTests
    {
        private readonly StringWriter _writer;
        private readonly ConsoleProxyDiagnostics _sut;

        public ConsoleProxyDiagnosticsTests()
        {
            _writer = new StringWriter();
            _sut = new ConsoleProxyDiagnostics(_writer);
        }

        #region OnRequestStarting

        [Fact]
        public void OnRequestStarting_WritesAllFields()
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

            var output = _writer.ToString();
            output.Should().Contain("[DBG]");
            output.Should().Contain("GET");
            output.Should().Contain("/api/users");
            output.Should().Contain("UserService.GetAll");
            output.Should().Contain("TraceId=abc123");
            output.Should().Contain("SpanId=def456");
            output.Should().Contain("ParentSpanId=parent789");
            output.Should().Contain("User=admin");
            output.Should().Contain("Unit=U1/Agent");
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

            var output = _writer.ToString();
            output.Should().Contain("ParentSpanId=(root)");
            output.Should().Contain("User=(anonymous)");
            output.Should().Contain("Unit=(none)/(none)");
        }

        #endregion

        #region OnRequestCompleted

        [Fact]
        public void OnRequestCompleted_Success_WritesDebugLevel()
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

            var output = _writer.ToString();
            output.Should().Contain("[DBG]");
            output.Should().Contain("200");
            output.Should().Contain("150");
            output.Should().Contain("UserService.GetById");
            output.Should().NotContain("[WRN]");
        }

        [Fact]
        public void OnRequestCompleted_Failure_WritesWarningWithError()
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

            var output = _writer.ToString();
            output.Should().Contain("[WRN]");
            output.Should().Contain("500");
            output.Should().Contain("Error=BusinessError: Something went wrong");
            output.Should().NotContain("[DBG]");
        }

        #endregion

        #region OnRequestFailed

        [Fact]
        public void OnRequestFailed_WritesErrorLevel()
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

            var output = _writer.ToString();
            output.Should().Contain("[ERR]");
            output.Should().Contain("Exception=HttpRequestException: Connection refused");
            output.Should().Contain("DataService.Fetch");
        }

        #endregion

        #region OnRetryAttempt

        [Fact]
        public void OnRetryAttempt_WritesWarningWithRetryDetails()
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

            var output = _writer.ToString();
            output.Should().Contain("[WRN]");
            output.Should().Contain("2/3");
            output.Should().Contain("Timeout: Request timed out");
            output.Should().Contain("500");
        }

        #endregion

        #region OnCircuitBreakerStateChanged

        [Fact]
        public void OnCircuitBreakerStateChanged_Open_WritesWarning()
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

            var output = _writer.ToString();
            output.Should().Contain("[WRN]");
            output.Should().Contain("OPENED");
            output.Should().Contain("Closed -> Open");
            output.Should().Contain("Failures=5");
        }

        [Fact]
        public void OnCircuitBreakerStateChanged_Closed_WritesInfo()
        {
            var e = new CircuitBreakerStateChangedEvent
            {
                ServiceName = "UserService",
                OldState = "Open",
                NewState = "Closed",
                FailureCount = 0
            };

            _sut.OnCircuitBreakerStateChanged(e);

            var output = _writer.ToString();
            output.Should().Contain("[INF]");
            output.Should().Contain("Open -> Closed");
            output.Should().NotContain("OPENED");
        }

        #endregion
    }
}
