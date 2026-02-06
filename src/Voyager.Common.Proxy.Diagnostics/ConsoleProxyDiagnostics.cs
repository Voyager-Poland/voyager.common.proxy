namespace Voyager.Common.Proxy.Diagnostics
{
    using System;
    using System.IO;

    /// <summary>
    /// Diagnostics handler that writes all proxy events directly to <see cref="Console"/>.
    /// Uses string interpolation instead of message templates, so it works independently of any logging framework.
    /// </summary>
    public sealed class ConsoleProxyDiagnostics : IProxyDiagnostics
    {
        private readonly TextWriter _writer;

        /// <summary>
        /// Initializes a new instance of <see cref="ConsoleProxyDiagnostics"/> that writes to <see cref="Console.Out"/>.
        /// </summary>
        public ConsoleProxyDiagnostics()
            : this(Console.Out)
        {
        }

        /// <summary>
        /// Initializes a new instance of <see cref="ConsoleProxyDiagnostics"/> with a custom <see cref="TextWriter"/>.
        /// </summary>
        /// <param name="writer">The text writer to write diagnostics output to.</param>
        public ConsoleProxyDiagnostics(TextWriter writer)
        {
            _writer = writer ?? throw new ArgumentNullException(nameof(writer));
        }

        /// <inheritdoc />
        public void OnRequestStarting(RequestStartingEvent e)
        {
            _writer.WriteLine(
                $"[DBG] Proxy request starting: {e.HttpMethod} {e.Url} [{e.ServiceName}.{e.MethodName}] TraceId={e.TraceId} SpanId={e.SpanId} ParentSpanId={e.ParentSpanId ?? "(root)"} User={e.UserLogin ?? "(anonymous)"} Unit={e.UnitId ?? "(none)"}/{e.UnitType ?? "(none)"}");
        }

        /// <inheritdoc />
        public void OnRequestCompleted(RequestCompletedEvent e)
        {
            if (e.IsSuccess)
            {
                _writer.WriteLine(
                    $"[DBG] Proxy request completed: {e.HttpMethod} {e.Url} {e.StatusCode} in {e.Duration.TotalMilliseconds}ms [{e.ServiceName}.{e.MethodName}] TraceId={e.TraceId} SpanId={e.SpanId} ParentSpanId={e.ParentSpanId ?? "(root)"} User={e.UserLogin ?? "(anonymous)"} Unit={e.UnitId ?? "(none)"}/{e.UnitType ?? "(none)"}");
            }
            else
            {
                _writer.WriteLine(
                    $"[WRN] Proxy request failed: {e.HttpMethod} {e.Url} {e.StatusCode} in {e.Duration.TotalMilliseconds}ms [{e.ServiceName}.{e.MethodName}] Error={e.ErrorType}: {e.ErrorMessage} TraceId={e.TraceId} SpanId={e.SpanId} ParentSpanId={e.ParentSpanId ?? "(root)"} User={e.UserLogin ?? "(anonymous)"} Unit={e.UnitId ?? "(none)"}/{e.UnitType ?? "(none)"}");
            }
        }

        /// <inheritdoc />
        public void OnRequestFailed(RequestFailedEvent e)
        {
            _writer.WriteLine(
                $"[ERR] Proxy request exception: {e.HttpMethod} {e.Url} in {e.Duration.TotalMilliseconds}ms [{e.ServiceName}.{e.MethodName}] Exception={e.ExceptionType}: {e.ExceptionMessage} TraceId={e.TraceId} SpanId={e.SpanId} ParentSpanId={e.ParentSpanId ?? "(root)"} User={e.UserLogin ?? "(anonymous)"} Unit={e.UnitId ?? "(none)"}/{e.UnitType ?? "(none)"}");
        }

        /// <inheritdoc />
        public void OnRetryAttempt(RetryAttemptEvent e)
        {
            _writer.WriteLine(
                $"[WRN] Proxy retry attempt {e.AttemptNumber}/{e.MaxAttempts} for [{e.ServiceName}.{e.MethodName}] after {e.ErrorType}: {e.ErrorMessage}. Waiting {e.Delay.TotalMilliseconds}ms. TraceId={e.TraceId} SpanId={e.SpanId} ParentSpanId={e.ParentSpanId ?? "(root)"} User={e.UserLogin ?? "(anonymous)"} Unit={e.UnitId ?? "(none)"}/{e.UnitType ?? "(none)"}");
        }

        /// <inheritdoc />
        public void OnCircuitBreakerStateChanged(CircuitBreakerStateChangedEvent e)
        {
            if (e.NewState == "Open")
            {
                _writer.WriteLine(
                    $"[WRN] Circuit breaker OPENED for {e.ServiceName}: {e.OldState} -> {e.NewState}. Failures={e.FailureCount}. LastError={e.LastErrorType}: {e.LastErrorMessage} User={e.UserLogin ?? "(anonymous)"} Unit={e.UnitId ?? "(none)"}/{e.UnitType ?? "(none)"}");
            }
            else
            {
                _writer.WriteLine(
                    $"[INF] Circuit breaker state changed for {e.ServiceName}: {e.OldState} -> {e.NewState}. Failures={e.FailureCount} User={e.UserLogin ?? "(anonymous)"} Unit={e.UnitId ?? "(none)"}/{e.UnitType ?? "(none)"}");
            }
        }
    }
}
