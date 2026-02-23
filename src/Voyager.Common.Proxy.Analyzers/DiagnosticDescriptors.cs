namespace Voyager.Common.Proxy.Analyzers
{
	using Microsoft.CodeAnalysis;

	/// <summary>
	/// Diagnostic descriptors for Voyager.Common.Proxy analyzer rules.
	/// </summary>
	internal static class DiagnosticDescriptors
	{
		/// <summary>
		/// VP0001: Array or collection parameter on GET/DELETE method is not supported.
		/// </summary>
		public static readonly DiagnosticDescriptor CollectionParameterOnGetOrDelete = new DiagnosticDescriptor(
			id: "VP0001",
			title: "Collection parameter not supported on GET/DELETE",
			messageFormat: "Parameter '{0}' of type '{1}' is not supported for {2} requests. Use [HttpPost] or change to a simple type.",
			category: "Voyager.Proxy.Usage",
			defaultSeverity: DiagnosticSeverity.Error,
			isEnabledByDefault: true,
			description: "Arrays and collections of simple types (int[], List<string>, etc.) cannot be serialized as query string parameters on GET/DELETE requests. The values will be silently lost at runtime. Use [HttpPost] with a request body instead.",
			helpLinkUri: "https://github.com/Voyager-Poland/voyager.common.proxy/blob/main/docs/analyzers/VP0001.md");
	}
}
