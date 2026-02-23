namespace Voyager.Common.Proxy.Analyzers.Tests
{
	using System.Threading.Tasks;
	using Microsoft.CodeAnalysis.CSharp.Testing;
	using Microsoft.CodeAnalysis.Testing;
	using Xunit;

	using AnalyzerVerifier = Microsoft.CodeAnalysis.CSharp.Testing.CSharpAnalyzerVerifier<
		ProxyInterfaceAnalyzer,
		Microsoft.CodeAnalysis.Testing.DefaultVerifier>;

	public class ProxyInterfaceAnalyzerTests
	{
		/// <summary>
		/// Stub attribute definitions that mirror the real Voyager.Common.Proxy.Abstractions types.
		/// All using directives must precede namespace declarations.
		/// </summary>
		private const string Usings = @"
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Voyager.Common.Proxy.Abstractions;
";

		private const string AttributeStubs = @"
namespace Voyager.Common.Proxy.Abstractions
{
	using System;

	public enum HttpMethod { Get, Post, Put, Delete, Patch }

	[AttributeUsage(AttributeTargets.Method)]
	public class HttpMethodAttribute : Attribute
	{
		public HttpMethod Method { get; }
		public string Template { get; }
		public HttpMethodAttribute(HttpMethod method, string template = null) { Method = method; Template = template; }
	}

	public sealed class HttpGetAttribute : HttpMethodAttribute
	{
		public HttpGetAttribute(string template = null) : base(HttpMethod.Get, template) { }
	}

	public sealed class HttpPostAttribute : HttpMethodAttribute
	{
		public HttpPostAttribute(string template = null) : base(HttpMethod.Post, template) { }
	}

	public sealed class HttpDeleteAttribute : HttpMethodAttribute
	{
		public HttpDeleteAttribute(string template = null) : base(HttpMethod.Delete, template) { }
	}

	[AttributeUsage(AttributeTargets.Interface)]
	public sealed class ServiceRouteAttribute : Attribute
	{
		public string Prefix { get; }
		public ServiceRouteAttribute(string prefix) { Prefix = prefix; }
	}
}
";

		private static string Source(string code) => Usings + AttributeStubs + code;

		#region Positive — diagnostic expected

		[Fact]
		public async Task HttpGet_WithIntArray_ReportsDiagnostic()
		{
			var source = Source(@"
[ServiceRoute(""invoice"")]
public interface IInvoiceService
{
	[HttpGet(""/invoices"")]
	Task GetInvoicesAsync(int[] {|#0:ids|});
}
");
			await AnalyzerVerifier.VerifyAnalyzerAsync(source,
				AnalyzerVerifier.Diagnostic(DiagnosticDescriptors.CollectionParameterOnGetOrDelete)
					.WithLocation(0).WithArguments("ids", "int[]", "GET"));
		}

		[Fact]
		public async Task HttpGet_WithListOfInt_ReportsDiagnostic()
		{
			var source = Source(@"
[ServiceRoute(""invoice"")]
public interface IInvoiceService
{
	[HttpGet]
	Task GetInvoicesAsync(List<int> {|#0:ids|});
}
");
			await AnalyzerVerifier.VerifyAnalyzerAsync(source,
				AnalyzerVerifier.Diagnostic(DiagnosticDescriptors.CollectionParameterOnGetOrDelete)
					.WithLocation(0).WithArguments("ids", "List<int>", "GET"));
		}

		[Fact]
		public async Task HttpGet_WithIEnumerableOfString_ReportsDiagnostic()
		{
			var source = Source(@"
[ServiceRoute(""test"")]
public interface ITestService
{
	[HttpGet]
	Task SearchAsync(IEnumerable<string> {|#0:names|});
}
");
			await AnalyzerVerifier.VerifyAnalyzerAsync(source,
				AnalyzerVerifier.Diagnostic(DiagnosticDescriptors.CollectionParameterOnGetOrDelete)
					.WithLocation(0).WithArguments("names", "IEnumerable<string>", "GET"));
		}

		[Fact]
		public async Task HttpGet_WithIReadOnlyListOfInt_ReportsDiagnostic()
		{
			var source = Source(@"
[ServiceRoute(""test"")]
public interface ITestService
{
	[HttpGet]
	Task ListAsync(IReadOnlyList<int> {|#0:ids|});
}
");
			await AnalyzerVerifier.VerifyAnalyzerAsync(source,
				AnalyzerVerifier.Diagnostic(DiagnosticDescriptors.CollectionParameterOnGetOrDelete)
					.WithLocation(0).WithArguments("ids", "IReadOnlyList<int>", "GET"));
		}

		[Fact]
		public async Task HttpDelete_WithStringArray_ReportsDiagnostic()
		{
			var source = Source(@"
[ServiceRoute(""test"")]
public interface ITestService
{
	[HttpDelete]
	Task DeleteManyAsync(string[] {|#0:codes|});
}
");
			await AnalyzerVerifier.VerifyAnalyzerAsync(source,
				AnalyzerVerifier.Diagnostic(DiagnosticDescriptors.CollectionParameterOnGetOrDelete)
					.WithLocation(0).WithArguments("codes", "string[]", "DELETE"));
		}

		[Fact]
		public async Task ConventionGet_WithIntArray_ReportsDiagnostic()
		{
			var source = Source(@"
[ServiceRoute(""invoice"")]
public interface IInvoiceService
{
	Task GetInvoicesAsync(int[] {|#0:ids|});
}
");
			await AnalyzerVerifier.VerifyAnalyzerAsync(source,
				AnalyzerVerifier.Diagnostic(DiagnosticDescriptors.CollectionParameterOnGetOrDelete)
					.WithLocation(0).WithArguments("ids", "int[]", "GET"));
		}

		[Fact]
		public async Task ConventionFind_WithListOfGuid_ReportsDiagnostic()
		{
			var source = Source(@"
[ServiceRoute(""test"")]
public interface ITestService
{
	Task FindItemsAsync(List<Guid> {|#0:ids|});
}
");
			await AnalyzerVerifier.VerifyAnalyzerAsync(source,
				AnalyzerVerifier.Diagnostic(DiagnosticDescriptors.CollectionParameterOnGetOrDelete)
					.WithLocation(0).WithArguments("ids", "List<Guid>", "GET"));
		}

		[Fact]
		public async Task ConventionDelete_WithListOfInt_ReportsDiagnostic()
		{
			var source = Source(@"
[ServiceRoute(""test"")]
public interface ITestService
{
	Task DeleteItemsAsync(List<int> {|#0:ids|});
}
");
			await AnalyzerVerifier.VerifyAnalyzerAsync(source,
				AnalyzerVerifier.Diagnostic(DiagnosticDescriptors.CollectionParameterOnGetOrDelete)
					.WithLocation(0).WithArguments("ids", "List<int>", "DELETE"));
		}

		[Fact]
		public async Task ConventionRemove_WithIntArray_ReportsDiagnostic()
		{
			var source = Source(@"
[ServiceRoute(""test"")]
public interface ITestService
{
	Task RemoveItemsAsync(int[] {|#0:ids|});
}
");
			await AnalyzerVerifier.VerifyAnalyzerAsync(source,
				AnalyzerVerifier.Diagnostic(DiagnosticDescriptors.CollectionParameterOnGetOrDelete)
					.WithLocation(0).WithArguments("ids", "int[]", "DELETE"));
		}

		[Fact]
		public async Task HttpGet_WithMultipleCollectionParams_ReportsMultipleDiagnostics()
		{
			var source = Source(@"
[ServiceRoute(""test"")]
public interface ITestService
{
	[HttpGet]
	Task SearchAsync(int[] {|#0:ids|}, string name, List<string> {|#1:tags|});
}
");
			await AnalyzerVerifier.VerifyAnalyzerAsync(source,
				AnalyzerVerifier.Diagnostic(DiagnosticDescriptors.CollectionParameterOnGetOrDelete)
					.WithLocation(0).WithArguments("ids", "int[]", "GET"),
				AnalyzerVerifier.Diagnostic(DiagnosticDescriptors.CollectionParameterOnGetOrDelete)
					.WithLocation(1).WithArguments("tags", "List<string>", "GET"));
		}

		[Fact]
		public async Task ConventionList_WithICollectionOfInt_ReportsDiagnostic()
		{
			var source = Source(@"
[ServiceRoute(""test"")]
public interface ITestService
{
	Task ListItemsAsync(ICollection<int> {|#0:ids|});
}
");
			await AnalyzerVerifier.VerifyAnalyzerAsync(source,
				AnalyzerVerifier.Diagnostic(DiagnosticDescriptors.CollectionParameterOnGetOrDelete)
					.WithLocation(0).WithArguments("ids", "ICollection<int>", "GET"));
		}

		[Fact]
		public async Task ConventionSearch_WithIListOfLong_ReportsDiagnostic()
		{
			var source = Source(@"
[ServiceRoute(""test"")]
public interface ITestService
{
	Task SearchByIdsAsync(IList<long> {|#0:ids|});
}
");
			await AnalyzerVerifier.VerifyAnalyzerAsync(source,
				AnalyzerVerifier.Diagnostic(DiagnosticDescriptors.CollectionParameterOnGetOrDelete)
					.WithLocation(0).WithArguments("ids", "IList<long>", "GET"));
		}

		[Fact]
		public async Task HttpGet_WithIReadOnlyCollectionOfDecimal_ReportsDiagnostic()
		{
			var source = Source(@"
[ServiceRoute(""test"")]
public interface ITestService
{
	[HttpGet]
	Task GetItemsAsync(IReadOnlyCollection<decimal> {|#0:prices|});
}
");
			await AnalyzerVerifier.VerifyAnalyzerAsync(source,
				AnalyzerVerifier.Diagnostic(DiagnosticDescriptors.CollectionParameterOnGetOrDelete)
					.WithLocation(0).WithArguments("prices", "IReadOnlyCollection<decimal>", "GET"));
		}

		[Fact]
		public async Task InterfaceWithHttpMethodAttribute_IsRecognizedAsProxyService()
		{
			var source = Source(@"
public interface ITestService
{
	[HttpGet]
	Task GetItemsAsync(int[] {|#0:ids|});
}
");
			await AnalyzerVerifier.VerifyAnalyzerAsync(source,
				AnalyzerVerifier.Diagnostic(DiagnosticDescriptors.CollectionParameterOnGetOrDelete)
					.WithLocation(0).WithArguments("ids", "int[]", "GET"));
		}

		[Fact]
		public async Task HttpMethodAttribute_Get_WithIntArray_ReportsDiagnostic()
		{
			var source = Source(@"
[ServiceRoute(""test"")]
public interface ITestService
{
	[HttpMethod(HttpMethod.Get, ""items"")]
	Task GetItemsAsync(int[] {|#0:ids|});
}
");
			await AnalyzerVerifier.VerifyAnalyzerAsync(source,
				AnalyzerVerifier.Diagnostic(DiagnosticDescriptors.CollectionParameterOnGetOrDelete)
					.WithLocation(0).WithArguments("ids", "int[]", "GET"));
		}

		[Fact]
		public async Task HttpGet_WithListOfEnum_ReportsDiagnostic()
		{
			var source = Source(@"
public enum Status { Active, Inactive }

[ServiceRoute(""test"")]
public interface ITestService
{
	[HttpGet]
	Task GetByStatusAsync(List<Status> {|#0:statuses|});
}
");
			await AnalyzerVerifier.VerifyAnalyzerAsync(source,
				AnalyzerVerifier.Diagnostic(DiagnosticDescriptors.CollectionParameterOnGetOrDelete)
					.WithLocation(0).WithArguments("statuses", "List<Status>", "GET"));
		}

		#endregion

		#region Negative — no diagnostic expected

		[Fact]
		public async Task HttpGet_WithSimpleParams_NoDiagnostic()
		{
			var source = Source(@"
[ServiceRoute(""test"")]
public interface ITestService
{
	[HttpGet]
	Task GetItemAsync(int id, string name, bool active);
}
");
			await AnalyzerVerifier.VerifyAnalyzerAsync(source);
		}

		[Fact]
		public async Task HttpPost_WithIntArray_NoDiagnostic()
		{
			var source = Source(@"
[ServiceRoute(""test"")]
public interface ITestService
{
	[HttpPost]
	Task CreateItemsAsync(int[] ids);
}
");
			await AnalyzerVerifier.VerifyAnalyzerAsync(source);
		}

		[Fact]
		public async Task ConventionPost_WithIntArray_NoDiagnostic()
		{
			var source = Source(@"
[ServiceRoute(""test"")]
public interface ITestService
{
	Task CreateItemsAsync(int[] ids);
}
");
			await AnalyzerVerifier.VerifyAnalyzerAsync(source);
		}

		[Fact]
		public async Task ConventionUpdate_WithIntArray_NoDiagnostic()
		{
			var source = Source(@"
[ServiceRoute(""test"")]
public interface ITestService
{
	Task UpdateItemsAsync(int[] ids);
}
");
			await AnalyzerVerifier.VerifyAnalyzerAsync(source);
		}

		[Fact]
		public async Task HttpGet_WithComplexTypeCollection_NoDiagnostic()
		{
			var source = Source(@"
public class OrderFilter { public string Name { get; set; } }

[ServiceRoute(""test"")]
public interface ITestService
{
	[HttpGet]
	Task GetOrdersAsync(List<OrderFilter> filters);
}
");
			await AnalyzerVerifier.VerifyAnalyzerAsync(source);
		}

		[Fact]
		public async Task NonInterface_WithIntArrayOnGet_NoDiagnostic()
		{
			var source = Source(@"
public class NotAnInterface
{
	public Task GetItemsAsync(int[] ids) => Task.CompletedTask;
}
");
			await AnalyzerVerifier.VerifyAnalyzerAsync(source);
		}

		[Fact]
		public async Task InterfaceWithoutProxyAttributes_NoDiagnostic()
		{
			var source = Source(@"
public interface IPlainService
{
	Task DoSomethingAsync(int[] ids);
}
");
			await AnalyzerVerifier.VerifyAnalyzerAsync(source);
		}

		[Fact]
		public async Task HttpGet_WithNullableSimpleParam_NoDiagnostic()
		{
			var source = Source(@"
[ServiceRoute(""test"")]
public interface ITestService
{
	[HttpGet]
	Task GetItemAsync(int? id, string name);
}
");
			await AnalyzerVerifier.VerifyAnalyzerAsync(source);
		}

		[Fact]
		public async Task HttpGet_WithEnumParam_NoDiagnostic()
		{
			var source = Source(@"
public enum Status { Active, Inactive }

[ServiceRoute(""test"")]
public interface ITestService
{
	[HttpGet]
	Task GetItemAsync(Status status);
}
");
			await AnalyzerVerifier.VerifyAnalyzerAsync(source);
		}

		[Fact]
		public async Task HttpGet_WithDateTimeParam_NoDiagnostic()
		{
			var source = Source(@"
[ServiceRoute(""test"")]
public interface ITestService
{
	[HttpGet]
	Task GetItemsAsync(DateTime from, DateTimeOffset to, TimeSpan duration, Guid id);
}
");
			await AnalyzerVerifier.VerifyAnalyzerAsync(source);
		}

		#endregion
	}
}
