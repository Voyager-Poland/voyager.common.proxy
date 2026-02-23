namespace Voyager.Common.Proxy.Analyzers
{
	using System.Collections.Immutable;
	using System.Linq;
	using Microsoft.CodeAnalysis;
	using Microsoft.CodeAnalysis.Diagnostics;

	/// <summary>
	/// Roslyn analyzer that validates Voyager.Common.Proxy service interface contracts.
	/// </summary>
	[DiagnosticAnalyzer(LanguageNames.CSharp)]
	public sealed class ProxyInterfaceAnalyzer : DiagnosticAnalyzer
	{
		private const string HttpMethodAttributeFullName = "Voyager.Common.Proxy.Abstractions.HttpMethodAttribute";
		private const string HttpGetAttributeFullName = "Voyager.Common.Proxy.Abstractions.HttpGetAttribute";
		private const string HttpDeleteAttributeFullName = "Voyager.Common.Proxy.Abstractions.HttpDeleteAttribute";
		private const string ServiceRouteAttributeFullName = "Voyager.Common.Proxy.Abstractions.ServiceRouteAttribute";
		private const string HttpMethodEnumFullName = "Voyager.Common.Proxy.Abstractions.HttpMethod";

		/// <inheritdoc />
		public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
			ImmutableArray.Create(DiagnosticDescriptors.CollectionParameterOnGetOrDelete);

		/// <inheritdoc />
		public override void Initialize(AnalysisContext context)
		{
			context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
			context.EnableConcurrentExecution();
			context.RegisterSymbolAction(AnalyzeMethod, SymbolKind.Method);
		}

		private static void AnalyzeMethod(SymbolAnalysisContext context)
		{
			var method = (IMethodSymbol)context.Symbol;

			// Only analyze methods on interfaces that look like proxy service interfaces
			if (method.ContainingType.TypeKind != TypeKind.Interface)
				return;

			if (!IsProxyServiceInterface(method.ContainingType))
				return;

			var httpMethod = GetHttpMethod(method);
			if (httpMethod == null)
				return;

			// VP0001: Only check GET and DELETE
			if (httpMethod != "GET" && httpMethod != "DELETE")
				return;

			foreach (var parameter in method.Parameters)
			{
				if (IsCollectionOfSimpleType(parameter.Type))
				{
					var diagnostic = Diagnostic.Create(
						DiagnosticDescriptors.CollectionParameterOnGetOrDelete,
						parameter.Locations.FirstOrDefault() ?? method.Locations.FirstOrDefault(),
						parameter.Name,
						parameter.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
						httpMethod);

					context.ReportDiagnostic(diagnostic);
				}
			}
		}

		/// <summary>
		/// Checks if the interface is a proxy service interface (has [ServiceRoute] or
		/// any method has [HttpMethod] derivatives).
		/// </summary>
		private static bool IsProxyServiceInterface(INamedTypeSymbol interfaceType)
		{
			// Check for [ServiceRoute] attribute
			foreach (var attr in interfaceType.GetAttributes())
			{
				if (attr.AttributeClass?.ToDisplayString() == ServiceRouteAttributeFullName)
					return true;
			}

			// Check if any method has [HttpGet], [HttpPost], etc.
			foreach (var member in interfaceType.GetMembers())
			{
				if (member is IMethodSymbol m)
				{
					foreach (var attr in m.GetAttributes())
					{
						var attrClass = attr.AttributeClass;
						while (attrClass != null)
						{
							if (attrClass.ToDisplayString() == HttpMethodAttributeFullName)
								return true;
							attrClass = attrClass.BaseType;
						}
					}
				}
			}

			return false;
		}

		/// <summary>
		/// Determines the HTTP method for the given method symbol.
		/// Returns "GET", "POST", "DELETE", etc. or null if not determinable.
		/// </summary>
		private static string? GetHttpMethod(IMethodSymbol method)
		{
			// Check for explicit [HttpGet], [HttpDelete], [HttpMethod(...)] attributes
			var hasHttpMethodAttribute = false;

			foreach (var attr in method.GetAttributes())
			{
				var attrFullName = attr.AttributeClass?.ToDisplayString();

				if (attrFullName == HttpGetAttributeFullName)
					return "GET";
				if (attrFullName == HttpDeleteAttributeFullName)
					return "DELETE";

				// Check base HttpMethodAttribute with explicit HttpMethod enum
				if (IsHttpMethodAttribute(attr.AttributeClass))
				{
					if (attr.ConstructorArguments.Length > 0 && attr.ConstructorArguments[0].Value is int enumValue)
					{
						// HttpMethod enum: Get=0, Post=1, Put=2, Delete=3, Patch=4
						switch (enumValue)
						{
							case 0: return "GET";
							case 3: return "DELETE";
							default: return "OTHER";
						}
					}

					// Derived attribute (HttpPostAttribute, etc.) — not GET/DELETE
					hasHttpMethodAttribute = true;
				}
			}

			// Any HttpMethodAttribute derivative found that wasn't GET/DELETE
			if (hasHttpMethodAttribute)
				return "OTHER";

			// Convention-based detection from method name
			var name = method.Name;

			if (name.StartsWith("Get") || name.StartsWith("Find") || name.StartsWith("List") || name.StartsWith("Search"))
				return "GET";

			if (name.StartsWith("Delete") || name.StartsWith("Remove"))
				return "DELETE";

			// POST, PUT, etc. — not our concern for VP0001
			return "OTHER";
		}

		private static bool IsHttpMethodAttribute(INamedTypeSymbol? attrClass)
		{
			while (attrClass != null)
			{
				if (attrClass.ToDisplayString() == HttpMethodAttributeFullName)
					return true;
				attrClass = attrClass.BaseType;
			}
			return false;
		}

		/// <summary>
		/// Checks if a type is an array or collection of a simple/primitive type.
		/// </summary>
		private static bool IsCollectionOfSimpleType(ITypeSymbol type)
		{
			// T[]
			if (type is IArrayTypeSymbol arrayType)
			{
				return IsSimpleType(arrayType.ElementType);
			}

			// List<T>, IEnumerable<T>, IReadOnlyList<T>, ICollection<T>, IList<T>, IReadOnlyCollection<T>
			if (type is INamedTypeSymbol namedType && namedType.IsGenericType)
			{
				var originalDef = namedType.ConstructedFrom?.ToDisplayString();
				if (originalDef != null && IsKnownCollectionType(originalDef))
				{
					var elementType = namedType.TypeArguments.FirstOrDefault();
					if (elementType != null)
					{
						return IsSimpleType(elementType);
					}
				}
			}

			return false;
		}

		private static bool IsKnownCollectionType(string typeFullName)
		{
			return typeFullName == "System.Collections.Generic.List<T>" ||
				   typeFullName == "System.Collections.Generic.IEnumerable<T>" ||
				   typeFullName == "System.Collections.Generic.ICollection<T>" ||
				   typeFullName == "System.Collections.Generic.IList<T>" ||
				   typeFullName == "System.Collections.Generic.IReadOnlyList<T>" ||
				   typeFullName == "System.Collections.Generic.IReadOnlyCollection<T>";
		}

		/// <summary>
		/// Mirrors the IsComplexType logic from RouteBuilder/ServiceScanner (inverted).
		/// Simple types: primitives, string, decimal, DateTime, DateTimeOffset, TimeSpan, Guid, enums.
		/// </summary>
		private static bool IsSimpleType(ITypeSymbol type)
		{
			// Unwrap Nullable<T>
			if (type is INamedTypeSymbol nullable &&
				nullable.ConstructedFrom?.SpecialType == SpecialType.System_Nullable_T &&
				nullable.TypeArguments.Length == 1)
			{
				type = nullable.TypeArguments[0];
			}

			switch (type.SpecialType)
			{
				case SpecialType.System_Boolean:
				case SpecialType.System_Byte:
				case SpecialType.System_SByte:
				case SpecialType.System_Int16:
				case SpecialType.System_UInt16:
				case SpecialType.System_Int32:
				case SpecialType.System_UInt32:
				case SpecialType.System_Int64:
				case SpecialType.System_UInt64:
				case SpecialType.System_Single:
				case SpecialType.System_Double:
				case SpecialType.System_Decimal:
				case SpecialType.System_String:
				case SpecialType.System_DateTime:
					return true;
			}

			if (type.TypeKind == TypeKind.Enum)
				return true;

			var fullName = type.ToDisplayString();
			return fullName == "System.DateTimeOffset" ||
				   fullName == "System.TimeSpan" ||
				   fullName == "System.Guid";
		}
	}
}
