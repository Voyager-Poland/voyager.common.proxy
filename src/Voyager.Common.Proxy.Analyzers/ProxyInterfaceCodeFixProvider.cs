namespace Voyager.Common.Proxy.Analyzers
{
	using System.Collections.Immutable;
	using System.Composition;
	using System.Linq;
	using System.Threading;
	using System.Threading.Tasks;
	using Microsoft.CodeAnalysis;
	using Microsoft.CodeAnalysis.CodeActions;
	using Microsoft.CodeAnalysis.CodeFixes;
	using Microsoft.CodeAnalysis.CSharp;
	using Microsoft.CodeAnalysis.CSharp.Syntax;

	/// <summary>
	/// Code fix provider for VP0001: replaces [HttpGet] with [HttpPost] or [HttpDelete] with [HttpPost].
	/// </summary>
	[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(ProxyInterfaceCodeFixProvider))]
	[Shared]
	public sealed class ProxyInterfaceCodeFixProvider : CodeFixProvider
	{
		/// <inheritdoc />
		public override ImmutableArray<string> FixableDiagnosticIds =>
			ImmutableArray.Create(DiagnosticDescriptors.CollectionParameterOnGetOrDelete.Id);

		/// <inheritdoc />
		public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

		/// <inheritdoc />
		public override async Task RegisterCodeFixesAsync(CodeFixContext context)
		{
			var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
			if (root == null)
				return;

			var diagnostic = context.Diagnostics.First();
			var diagnosticSpan = diagnostic.Location.SourceSpan;

			// The diagnostic is reported on the parameter. Walk up to find the method declaration.
			var node = root.FindNode(diagnosticSpan);
			var methodDeclaration = node.FirstAncestorOrSelf<MethodDeclarationSyntax>();
			if (methodDeclaration == null)
				return;

			context.RegisterCodeFix(
				CodeAction.Create(
					title: "Change to [HttpPost]",
					createChangedDocument: ct => ChangeToHttpPostAsync(context.Document, methodDeclaration, ct),
					equivalenceKey: "ChangeToHttpPost"),
				diagnostic);
		}

		private static async Task<Document> ChangeToHttpPostAsync(
			Document document,
			MethodDeclarationSyntax methodDeclaration,
			CancellationToken cancellationToken)
		{
			var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
			if (root == null)
				return document;

			var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
			if (semanticModel == null)
				return document;

			// Find the [HttpGet(...)] or [HttpDelete(...)] attribute on the method
			AttributeSyntax? targetAttribute = null;
			foreach (var attrList in methodDeclaration.AttributeLists)
			{
				foreach (var attr in attrList.Attributes)
				{
					var symbolInfo = semanticModel.GetSymbolInfo(attr, cancellationToken);
					var attrSymbol = symbolInfo.Symbol ?? symbolInfo.CandidateSymbols.FirstOrDefault();
					if (attrSymbol == null)
						continue;

					var containingType = attrSymbol.ContainingType?.ToDisplayString();
					if (containingType == "Voyager.Common.Proxy.Abstractions.HttpGetAttribute" ||
						containingType == "Voyager.Common.Proxy.Abstractions.HttpDeleteAttribute")
					{
						targetAttribute = attr;
						break;
					}
				}
				if (targetAttribute != null)
					break;
			}

			if (targetAttribute == null)
			{
				// Convention-based method (no attribute) — add [HttpPost] attribute
				// Preserve leading trivia (indentation) from the method declaration
				var leadingTrivia = methodDeclaration.GetLeadingTrivia();
				var httpPostAttr = SyntaxFactory.Attribute(SyntaxFactory.IdentifierName("HttpPost"));
				var attrList = SyntaxFactory.AttributeList(SyntaxFactory.SingletonSeparatedList(httpPostAttr))
					.WithLeadingTrivia(leadingTrivia)
					.WithTrailingTrivia(SyntaxFactory.ElasticCarriageReturnLineFeed);
				var newMethod = methodDeclaration
					.WithLeadingTrivia(leadingTrivia)
					.WithAttributeLists(methodDeclaration.AttributeLists.Add(attrList));
				var newRoot = root.ReplaceNode(methodDeclaration, newMethod);
				return document.WithSyntaxRoot(newRoot);
			}

			// Replace attribute name with HttpPost, keep arguments (template)
			var newName = SyntaxFactory.IdentifierName("HttpPost");
			var newAttribute = targetAttribute.WithName(newName);
			var updatedRoot = root.ReplaceNode(targetAttribute, newAttribute);
			return document.WithSyntaxRoot(updatedRoot);
		}
	}
}
