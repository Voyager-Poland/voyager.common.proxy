namespace Voyager.Common.Proxy.Diagnostics.ApplicationInsights
{
	using System;
	using System.Collections.Generic;
	using Microsoft.ApplicationInsights;
	using Microsoft.ApplicationInsights.Channel;
	using Microsoft.ApplicationInsights.DataContracts;

	/// <summary>
	/// Diagnostics handler that sends proxy events to Azure Application Insights.
	/// </summary>
	/// <remarks>
	/// Event mapping:
	/// <list type="bullet">
	/// <item>OnRequestStarting → no-op (covered by DependencyTelemetry in Completed/Failed)</item>
	/// <item>OnRequestCompleted → DependencyTelemetry (type="VoyagerProxy")</item>
	/// <item>OnRequestFailed → ExceptionTelemetry + failed DependencyTelemetry</item>
	/// <item>OnRetryAttempt → EventTelemetry ("ProxyRetryAttempt")</item>
	/// <item>OnCircuitBreakerStateChanged → EventTelemetry ("ProxyCircuitBreakerStateChanged")</item>
	/// </list>
	/// </remarks>
	public sealed class ApplicationInsightsProxyDiagnostics : IProxyDiagnostics
	{
		private readonly TelemetryClient _telemetryClient;
		private readonly ApplicationInsightsOptions _options;

		/// <summary>
		/// Initializes a new instance of <see cref="ApplicationInsightsProxyDiagnostics"/>.
		/// </summary>
		/// <param name="telemetryClient">The Application Insights telemetry client.</param>
		/// <param name="options">Optional configuration.</param>
		public ApplicationInsightsProxyDiagnostics(TelemetryClient telemetryClient, ApplicationInsightsOptions? options = null)
		{
			_telemetryClient = telemetryClient ?? throw new ArgumentNullException(nameof(telemetryClient));
			_options = options ?? new ApplicationInsightsOptions();
		}

		/// <inheritdoc />
		public void OnRequestStarting(RequestStartingEvent e)
		{
			// No-op: DependencyTelemetry in OnRequestCompleted/OnRequestFailed covers this.
		}

		/// <inheritdoc />
		public void OnRequestCompleted(RequestCompletedEvent e)
		{
			try
			{
				var dependency = CreateDependencyTelemetry(
					e.ServiceName, e.MethodName, e.HttpMethod, e.Url,
					e.Duration, e.Timestamp, e.IsSuccess,
					e.TraceId, e.SpanId,
					e.UserLogin, e.UnitId, e.UnitType, e.CustomProperties);

				dependency.ResultCode = e.StatusCode.ToString();

				if (!e.IsSuccess)
				{
					if (e.ErrorType != null)
						dependency.Properties["ErrorType"] = e.ErrorType;
					if (e.ErrorMessage != null)
						dependency.Properties["ErrorMessage"] = e.ErrorMessage;
				}

				_telemetryClient.TrackDependency(dependency);
			}
			catch
			{
				// Diagnostics must never throw.
			}
		}

		/// <inheritdoc />
		public void OnRequestFailed(RequestFailedEvent e)
		{
			try
			{
				var exception = new ExceptionTelemetry
				{
					Message = $"{e.ExceptionType}: {e.ExceptionMessage}",
					Timestamp = e.Timestamp,
					SeverityLevel = SeverityLevel.Error,
				};

				SetTraceContext(exception, e.TraceId, e.SpanId);
				SetCloudRoleName(exception);
				SetCommonProperties(exception.Properties, e.ServiceName, e.MethodName, e.HttpMethod, e.Url, e.UserLogin, e.UnitId, e.UnitType, e.CustomProperties);
				exception.Properties["ExceptionType"] = e.ExceptionType;
				exception.Properties["ExceptionMessage"] = e.ExceptionMessage;

				_telemetryClient.TrackException(exception);

				var dependency = CreateDependencyTelemetry(
					e.ServiceName, e.MethodName, e.HttpMethod, e.Url,
					e.Duration, e.Timestamp, false,
					e.TraceId, e.SpanId,
					e.UserLogin, e.UnitId, e.UnitType, e.CustomProperties);

				dependency.Properties["ExceptionType"] = e.ExceptionType;
				dependency.Properties["ExceptionMessage"] = e.ExceptionMessage;

				_telemetryClient.TrackDependency(dependency);
			}
			catch
			{
				// Diagnostics must never throw.
			}
		}

		/// <inheritdoc />
		public void OnRetryAttempt(RetryAttemptEvent e)
		{
			try
			{
				var evt = new EventTelemetry("ProxyRetryAttempt")
				{
					Timestamp = e.Timestamp,
				};

				SetTraceContext(evt, e.TraceId, e.SpanId);
				SetCloudRoleName(evt);
				SetCommonProperties(evt.Properties, e.ServiceName, e.MethodName, null, null, e.UserLogin, e.UnitId, e.UnitType, e.CustomProperties);
				evt.Properties["AttemptNumber"] = e.AttemptNumber.ToString();
				evt.Properties["MaxAttempts"] = e.MaxAttempts.ToString();
				evt.Properties["DelayMs"] = e.Delay.TotalMilliseconds.ToString();
				evt.Properties["WillRetry"] = e.WillRetry.ToString();
				if (e.ErrorType != null)
					evt.Properties["ErrorType"] = e.ErrorType;
				if (e.ErrorMessage != null)
					evt.Properties["ErrorMessage"] = e.ErrorMessage;

				_telemetryClient.TrackEvent(evt);
			}
			catch
			{
				// Diagnostics must never throw.
			}
		}

		/// <inheritdoc />
		public void OnCircuitBreakerStateChanged(CircuitBreakerStateChangedEvent e)
		{
			try
			{
				var evt = new EventTelemetry("ProxyCircuitBreakerStateChanged")
				{
					Timestamp = e.Timestamp,
				};

				SetTraceContext(evt, null, null);
				SetCloudRoleName(evt);

				evt.Properties["ServiceName"] = e.ServiceName;
				evt.Properties["OldState"] = e.OldState;
				evt.Properties["NewState"] = e.NewState;
				evt.Properties["FailureCount"] = e.FailureCount.ToString();

				if (e.LastErrorType != null)
					evt.Properties["LastErrorType"] = e.LastErrorType;
				if (e.LastErrorMessage != null)
					evt.Properties["LastErrorMessage"] = e.LastErrorMessage;
				if (e.UserLogin != null)
					evt.Properties["UserLogin"] = e.UserLogin;
				if (e.UnitId != null)
					evt.Properties["UnitId"] = e.UnitId;
				if (e.UnitType != null)
					evt.Properties["UnitType"] = e.UnitType;

				_telemetryClient.TrackEvent(evt);
			}
			catch
			{
				// Diagnostics must never throw.
			}
		}

		private DependencyTelemetry CreateDependencyTelemetry(
			string serviceName, string methodName, string httpMethod, string url,
			TimeSpan duration, DateTimeOffset timestamp, bool success,
			string? traceId, string? spanId,
			string? userLogin, string? unitId, string? unitType,
			IReadOnlyDictionary<string, string>? customProperties)
		{
			var dependency = new DependencyTelemetry
			{
				Type = "VoyagerProxy",
				Name = $"{httpMethod} {url}",
				Target = serviceName,
				Data = $"{httpMethod} {url} [{serviceName}.{methodName}]",
				Duration = duration,
				Timestamp = timestamp,
				Success = success,
			};

			SetTraceContext(dependency, traceId, spanId);
			SetCloudRoleName(dependency);
			SetCommonProperties(dependency.Properties, serviceName, methodName, httpMethod, url, userLogin, unitId, unitType, customProperties);

			return dependency;
		}

		private static void SetTraceContext(ITelemetry telemetry, string? traceId, string? spanId)
		{
			if (!string.IsNullOrEmpty(traceId))
				telemetry.Context.Operation.Id = traceId;
			if (!string.IsNullOrEmpty(spanId))
				telemetry.Context.Operation.ParentId = spanId;
		}

		private void SetCloudRoleName(ITelemetry telemetry)
		{
			if (!string.IsNullOrEmpty(_options.CloudRoleName))
				telemetry.Context.Cloud.RoleName = _options.CloudRoleName;
		}

		private static void SetCommonProperties(
			IDictionary<string, string> properties,
			string serviceName,
			string methodName,
			string? httpMethod,
			string? url,
			string? userLogin,
			string? unitId,
			string? unitType,
			IReadOnlyDictionary<string, string>? customProperties)
		{
			properties["ServiceName"] = serviceName;
			properties["MethodName"] = methodName;

			if (httpMethod != null)
				properties["HttpMethod"] = httpMethod;
			if (url != null)
				properties["Url"] = url;
			if (userLogin != null)
				properties["UserLogin"] = userLogin;
			if (unitId != null)
				properties["UnitId"] = unitId;
			if (unitType != null)
				properties["UnitType"] = unitType;

			if (customProperties != null)
			{
				foreach (var kvp in customProperties)
				{
					properties[kvp.Key] = kvp.Value;
				}
			}
		}
	}
}
