namespace Voyager.Common.Proxy.Analyzers.Tests
{
	using System.Threading.Tasks;
	using Microsoft.CodeAnalysis.CSharp.Testing;
	using Microsoft.CodeAnalysis.Testing;
	using Xunit;

	using CodeFixVerifier = Microsoft.CodeAnalysis.CSharp.Testing.CSharpCodeFixVerifier<
		ProxyInterfaceAnalyzer,
		ProxyInterfaceCodeFixProvider,
		Microsoft.CodeAnalysis.Testing.DefaultVerifier>;

	public class ProxyInterfaceCodeFixProviderTests
	{
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

		[Fact]
		public async Task HttpGet_ReplacedWithHttpPost()
		{
			var source = Source(@"
[ServiceRoute(""invoice"")]
public interface IInvoiceService
{
	[HttpGet(""/invoices"")]
	Task GetInvoicesAsync(int[] {|#0:ids|});
}
");

			var fixedSource = Source(@"
[ServiceRoute(""invoice"")]
public interface IInvoiceService
{
	[HttpPost(""/invoices"")]
	Task GetInvoicesAsync(int[] ids);
}
");

			await CodeFixVerifier.VerifyCodeFixAsync(source,
				CodeFixVerifier.Diagnostic(DiagnosticDescriptors.CollectionParameterOnGetOrDelete)
					.WithLocation(0).WithArguments("ids", "int[]", "GET"),
				fixedSource);
		}

		[Fact]
		public async Task HttpDelete_ReplacedWithHttpPost()
		{
			var source = Source(@"
[ServiceRoute(""test"")]
public interface ITestService
{
	[HttpDelete(""/items"")]
	Task DeleteManyAsync(string[] {|#0:codes|});
}
");

			var fixedSource = Source(@"
[ServiceRoute(""test"")]
public interface ITestService
{
	[HttpPost(""/items"")]
	Task DeleteManyAsync(string[] codes);
}
");

			await CodeFixVerifier.VerifyCodeFixAsync(source,
				CodeFixVerifier.Diagnostic(DiagnosticDescriptors.CollectionParameterOnGetOrDelete)
					.WithLocation(0).WithArguments("codes", "string[]", "DELETE"),
				fixedSource);
		}

		[Fact]
		public async Task ConventionGet_AddsHttpPostAttribute()
		{
			var source = Source(@"
[ServiceRoute(""invoice"")]
public interface IInvoiceService
{
	Task GetInvoicesAsync(int[] {|#0:ids|});
}
");

			var fixedSource = Source(@"
[ServiceRoute(""invoice"")]
public interface IInvoiceService
{
    [HttpPost]
    Task GetInvoicesAsync(int[] ids);
}
");

			await CodeFixVerifier.VerifyCodeFixAsync(source,
				CodeFixVerifier.Diagnostic(DiagnosticDescriptors.CollectionParameterOnGetOrDelete)
					.WithLocation(0).WithArguments("ids", "int[]", "GET"),
				fixedSource);
		}

		[Fact]
		public async Task HttpGetNoTemplate_ReplacedWithHttpPost()
		{
			var source = Source(@"
[ServiceRoute(""test"")]
public interface ITestService
{
	[HttpGet]
	Task GetItemsAsync(List<int> {|#0:ids|});
}
");

			var fixedSource = Source(@"
[ServiceRoute(""test"")]
public interface ITestService
{
	[HttpPost]
	Task GetItemsAsync(List<int> ids);
}
");

			await CodeFixVerifier.VerifyCodeFixAsync(source,
				CodeFixVerifier.Diagnostic(DiagnosticDescriptors.CollectionParameterOnGetOrDelete)
					.WithLocation(0).WithArguments("ids", "List<int>", "GET"),
				fixedSource);
		}
	}
}
