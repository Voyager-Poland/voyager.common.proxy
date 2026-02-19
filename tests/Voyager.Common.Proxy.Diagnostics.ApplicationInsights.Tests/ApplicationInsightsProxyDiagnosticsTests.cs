namespace Voyager.Common.Proxy.Diagnostics.ApplicationInsights.Tests
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using FluentAssertions;
	using Microsoft.ApplicationInsights;
	using Microsoft.ApplicationInsights.Channel;
	using Microsoft.ApplicationInsights.DataContracts;
	using Microsoft.ApplicationInsights.Extensibility;
	using Xunit;

	public class ApplicationInsightsProxyDiagnosticsTests
	{
		private readonly List<ITelemetry> _sentTelemetry;
		private readonly ApplicationInsightsProxyDiagnostics _sut;

		public ApplicationInsightsProxyDiagnosticsTests()
		{
			_sentTelemetry = new List<ITelemetry>();
			var channel = new StubTelemetryChannel { OnSend = t => _sentTelemetry.Add(t) };
			var config = new TelemetryConfiguration { TelemetryChannel = channel };
			var client = new TelemetryClient(config);
			_sut = new ApplicationInsightsProxyDiagnostics(client);
		}

		#region OnRequestStarting

		[Fact]
		public void OnRequestStarting_IsNoOp()
		{
			var e = new RequestStartingEvent
			{
				HttpMethod = "GET",
				Url = "/api/users",
				ServiceName = "UserService",
				MethodName = "GetAll",
				TraceId = "abc123",
				SpanId = "def456",
			};

			_sut.OnRequestStarting(e);

			_sentTelemetry.Should().BeEmpty();
		}

		#endregion

		#region OnRequestCompleted

		[Fact]
		public void OnRequestCompleted_Success_TracksDependency()
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
				TraceId = "abc123def456789012345678901234ab",
				SpanId = "1234567890abcdef",
				UserLogin = "admin",
				UnitId = "U1",
				UnitType = "Agent",
			};

			_sut.OnRequestCompleted(e);

			_sentTelemetry.Should().HaveCount(1);
			var dep = _sentTelemetry[0].Should().BeOfType<DependencyTelemetry>().Subject;
			dep.Type.Should().Be("VoyagerProxy");
			dep.Name.Should().Be("GET /api/users/1");
			dep.Target.Should().Be("UserService");
			dep.ResultCode.Should().Be("200");
			dep.Success.Should().BeTrue();
			dep.Duration.Should().Be(TimeSpan.FromMilliseconds(150));
			dep.Context.Operation.Id.Should().Be("abc123def456789012345678901234ab");
			dep.Context.Operation.ParentId.Should().Be("1234567890abcdef");
			dep.Properties["ServiceName"].Should().Be("UserService");
			dep.Properties["MethodName"].Should().Be("GetById");
			dep.Properties["HttpMethod"].Should().Be("GET");
			dep.Properties["Url"].Should().Be("/api/users/1");
			dep.Properties["UserLogin"].Should().Be("admin");
			dep.Properties["UnitId"].Should().Be("U1");
			dep.Properties["UnitType"].Should().Be("Agent");
		}

		[Fact]
		public void OnRequestCompleted_Failure_IncludesErrorProperties()
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
				ErrorMessage = "Something went wrong",
			};

			_sut.OnRequestCompleted(e);

			var dep = _sentTelemetry.OfType<DependencyTelemetry>().Single();
			dep.Success.Should().BeFalse();
			dep.ResultCode.Should().Be("500");
			dep.Properties["ErrorType"].Should().Be("BusinessError");
			dep.Properties["ErrorMessage"].Should().Be("Something went wrong");
		}

		[Fact]
		public void OnRequestCompleted_MapsCustomProperties()
		{
			var e = new RequestCompletedEvent
			{
				HttpMethod = "GET",
				Url = "/api/data",
				StatusCode = 200,
				Duration = TimeSpan.FromMilliseconds(50),
				IsSuccess = true,
				ServiceName = "DataService",
				MethodName = "Fetch",
				TraceId = "t1",
				SpanId = "s1",
				CustomProperties = new Dictionary<string, string>
				{
					["CorrelationId"] = "corr-123",
					["Region"] = "EU",
				},
			};

			_sut.OnRequestCompleted(e);

			var dep = _sentTelemetry.OfType<DependencyTelemetry>().Single();
			dep.Properties["CorrelationId"].Should().Be("corr-123");
			dep.Properties["Region"].Should().Be("EU");
		}

		#endregion

		#region OnRequestFailed

		[Fact]
		public void OnRequestFailed_TracksExceptionAndDependency()
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
				SpanId = "s1",
				UserLogin = "admin",
				UnitId = "U1",
				UnitType = "Agent",
			};

			_sut.OnRequestFailed(e);

			_sentTelemetry.Should().HaveCount(2);

			var exc = _sentTelemetry.OfType<ExceptionTelemetry>().Single();
			exc.Message.Should().Contain("HttpRequestException");
			exc.Message.Should().Contain("Connection refused");
			exc.SeverityLevel.Should().Be(SeverityLevel.Error);
			exc.Properties["ServiceName"].Should().Be("DataService");
			exc.Properties["ExceptionType"].Should().Be("HttpRequestException");
			exc.Context.Operation.Id.Should().Be("t1");

			var dep = _sentTelemetry.OfType<DependencyTelemetry>().Single();
			dep.Success.Should().BeFalse();
			dep.Type.Should().Be("VoyagerProxy");
			dep.Properties["ExceptionType"].Should().Be("HttpRequestException");
		}

		#endregion

		#region OnRetryAttempt

		[Fact]
		public void OnRetryAttempt_TracksEvent()
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
				WillRetry = true,
				TraceId = "t1",
				SpanId = "s1",
			};

			_sut.OnRetryAttempt(e);

			_sentTelemetry.Should().HaveCount(1);
			var evt = _sentTelemetry.OfType<EventTelemetry>().Single();
			evt.Name.Should().Be("ProxyRetryAttempt");
			evt.Properties["AttemptNumber"].Should().Be("2");
			evt.Properties["MaxAttempts"].Should().Be("3");
			evt.Properties["DelayMs"].Should().Be("500");
			evt.Properties["WillRetry"].Should().Be("True");
			evt.Properties["ErrorType"].Should().Be("Timeout");
			evt.Properties["ErrorMessage"].Should().Be("Request timed out");
			evt.Properties["ServiceName"].Should().Be("UserService");
			evt.Properties["MethodName"].Should().Be("GetAll");
			evt.Context.Operation.Id.Should().Be("t1");
		}

		#endregion

		#region OnCircuitBreakerStateChanged

		[Fact]
		public void OnCircuitBreakerStateChanged_TracksEvent()
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
				UnitType = "Agent",
			};

			_sut.OnCircuitBreakerStateChanged(e);

			_sentTelemetry.Should().HaveCount(1);
			var evt = _sentTelemetry.OfType<EventTelemetry>().Single();
			evt.Name.Should().Be("ProxyCircuitBreakerStateChanged");
			evt.Properties["ServiceName"].Should().Be("UserService");
			evt.Properties["OldState"].Should().Be("Closed");
			evt.Properties["NewState"].Should().Be("Open");
			evt.Properties["FailureCount"].Should().Be("5");
			evt.Properties["LastErrorType"].Should().Be("Timeout");
			evt.Properties["LastErrorMessage"].Should().Be("Timed out");
			evt.Properties["UserLogin"].Should().Be("admin");
			evt.Properties["UnitId"].Should().Be("U1");
			evt.Properties["UnitType"].Should().Be("Agent");
		}

		[Fact]
		public void OnCircuitBreakerStateChanged_NullOptionalFields_OmitsProperties()
		{
			var e = new CircuitBreakerStateChangedEvent
			{
				ServiceName = "UserService",
				OldState = "Open",
				NewState = "Closed",
				FailureCount = 0,
			};

			_sut.OnCircuitBreakerStateChanged(e);

			var evt = _sentTelemetry.OfType<EventTelemetry>().Single();
			evt.Properties.Should().NotContainKey("LastErrorType");
			evt.Properties.Should().NotContainKey("LastErrorMessage");
			evt.Properties.Should().NotContainKey("UserLogin");
			evt.Properties.Should().NotContainKey("UnitId");
			evt.Properties.Should().NotContainKey("UnitType");
		}

		#endregion

		#region CloudRoleName

		[Fact]
		public void CloudRoleName_WhenConfigured_SetsOnAllTelemetry()
		{
			var channel = new StubTelemetryChannel { OnSend = t => _sentTelemetry.Add(t) };
			var config = new TelemetryConfiguration { TelemetryChannel = channel };
			var client = new TelemetryClient(config);
			var options = new ApplicationInsightsOptions { CloudRoleName = "MyService-Prod" };
			var sut = new ApplicationInsightsProxyDiagnostics(client, options);

			sut.OnRequestCompleted(new RequestCompletedEvent
			{
				HttpMethod = "GET",
				Url = "/test",
				StatusCode = 200,
				IsSuccess = true,
				ServiceName = "Svc",
				MethodName = "M",
				TraceId = "t1",
				SpanId = "s1",
			});

			// Find the telemetry sent by this specific sut (skip any from constructor _sut)
			var dep = _sentTelemetry.OfType<DependencyTelemetry>().Last();
			dep.Context.Cloud.RoleName.Should().Be("MyService-Prod");
		}

		#endregion

		#region Error safety

		[Fact]
		public void AllMethods_NeverThrow_EvenWithNullEvent()
		{
			// Passing a well-formed but minimal event should not throw
			var act1 = () => _sut.OnRequestStarting(new RequestStartingEvent());
			var act2 = () => _sut.OnRequestCompleted(new RequestCompletedEvent());
			var act3 = () => _sut.OnRequestFailed(new RequestFailedEvent());
			var act4 = () => _sut.OnRetryAttempt(new RetryAttemptEvent());
			var act5 = () => _sut.OnCircuitBreakerStateChanged(new CircuitBreakerStateChangedEvent());

			act1.Should().NotThrow();
			act2.Should().NotThrow();
			act3.Should().NotThrow();
			act4.Should().NotThrow();
			act5.Should().NotThrow();
		}

		#endregion

		#region StubTelemetryChannel

		private sealed class StubTelemetryChannel : ITelemetryChannel
		{
			public Action<ITelemetry>? OnSend { get; set; }
			public bool? DeveloperMode { get; set; }
			public string? EndpointAddress { get; set; }

			public void Send(ITelemetry item)
			{
				OnSend?.Invoke(item);
			}

			public void Flush() { }
			public void Dispose() { }
		}

		#endregion
	}
}
