namespace Voyager.Common.Proxy.Server.Core.Diagnostics
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using Voyager.Common.Proxy.Diagnostics;

    /// <summary>
    /// Helper class for emitting diagnostic events safely on the server side.
    /// Ensures diagnostics never break the main flow.
    /// </summary>
    internal sealed class ServerDiagnosticsEmitter
    {
        private readonly IEnumerable<IProxyDiagnostics> _handlers;
        private readonly IProxyRequestContext _context;

        public ServerDiagnosticsEmitter(
            IEnumerable<IProxyDiagnostics>? handlers,
            IProxyRequestContext? context)
        {
            _handlers = handlers ?? Array.Empty<IProxyDiagnostics>();
            _context = context ?? NullProxyRequestContext.Instance;
        }

        /// <summary>
        /// Gets a new correlation ID for the current request.
        /// Uses Activity.Current.TraceId if available (OpenTelemetry), otherwise generates new GUID.
        /// </summary>
        public static Guid GetCorrelationId()
        {
            var activity = Activity.Current;
            if (activity != null)
            {
                var traceId = activity.TraceId.ToString();
                if (Guid.TryParse(traceId, out var guid))
                {
                    return guid;
                }
            }
            return Guid.NewGuid();
        }

        /// <summary>
        /// Captures current user context from IProxyRequestContext.
        /// </summary>
        public UserContext CaptureUserContext()
        {
            return new UserContext
            {
                UserLogin = _context.UserLogin,
                UnitId = _context.UnitId,
                UnitType = _context.UnitType,
                CustomProperties = _context.CustomProperties
            };
        }

        public void EmitRequestStarting(RequestStartingEvent e)
        {
            foreach (var handler in _handlers)
            {
                try
                {
                    handler.OnRequestStarting(e);
                }
                catch
                {
                    // Diagnostics should never break the main flow
                }
            }
        }

        public void EmitRequestCompleted(RequestCompletedEvent e)
        {
            foreach (var handler in _handlers)
            {
                try
                {
                    handler.OnRequestCompleted(e);
                }
                catch
                {
                    // Diagnostics should never break the main flow
                }
            }
        }

        public void EmitRequestFailed(RequestFailedEvent e)
        {
            foreach (var handler in _handlers)
            {
                try
                {
                    handler.OnRequestFailed(e);
                }
                catch
                {
                    // Diagnostics should never break the main flow
                }
            }
        }

        /// <summary>
        /// Captured user context for a request.
        /// </summary>
        public sealed class UserContext
        {
            public string? UserLogin { get; set; }
            public string? UnitId { get; set; }
            public string? UnitType { get; set; }
            public IReadOnlyDictionary<string, string>? CustomProperties { get; set; }
        }
    }
}
