namespace Blaster;

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using MarkdigExtensions;
using MikeNakis.Kit;
using MikeNakis.Kit.Collections;
using MikeNakis.Kit.Extensions;
using static Markdig.MarkdownExtensions;
using static MikeNakis.Kit.GlobalStatics;
using Html = HtmlAgilityPack;
using Markdig = Markdig;
using MarkdigSyntax = Markdig.Syntax;
using RegEx = System.Text.RegularExpressions;
using Sys = System;
using SysText = System.Text;

public sealed class BlasterEngine
{
	public static readonly Sys.Action<Diagnostic> DefaultDiagnosticMessageConsumer = diagnosticMessage =>
	{
		d( $"{diagnosticMessage.SourceFilePathName}({diagnosticMessage.LineNumber},{diagnosticMessage.ColumnNumber}): {diagnosticMessage.Message}" );
		if( diagnosticMessage.LineText != null )
		{
			d( $"    {diagnosticMessage.LineText}" );
			d( $"    {new string( ' ', diagnosticMessage.ColumnNumber )}^" );
		}
		return;

		static void d( string s )
		{
			Log.Info( s );
		}
	};

	public static void Run( IFileSystem contentFileSystem, IFileSystem templateFileSystem, IFileSystem outputFileSystem, Sys.Action<Diagnostic> diagnosticMessageConsumer )
	{
		BlasterEngine blasterEngine = new( contentFileSystem, templateFileSystem, outputFileSystem, diagnosticMessageConsumer );
		blasterEngine.Run();
	}

	readonly IFileSystem contentFileSystem;
	readonly IFileSystem templateFileSystem;
	readonly IFileSystem outputFileSystem;
	readonly Sys.Action<Diagnostic> diagnosticMessageConsumer;
	readonly List<View> views = new List<View>();
	readonly View rootView = new ContentView( null, getRootViewHtmlNode(), Name.Of( "root" ), new RegEx.Regex( ".*" ) );

	BlasterEngine( IFileSystem contentFileSystem, IFileSystem templateFileSystem, IFileSystem outputFileSystem, Sys.Action<Diagnostic> diagnosticMessageConsumer )
	{
		this.contentFileSystem = contentFileSystem;
		this.templateFileSystem = templateFileSystem;
		this.outputFileSystem = outputFileSystem;
		this.diagnosticMessageConsumer = diagnosticMessageConsumer;
		views.Add( rootView );
	}

	void issueDiagnostic( string sourceFilePathName, int lineNumber, int columnNumber, Severity severity, string? lineText, string message )
	{
		var diagnostic = new Diagnostic( sourceFilePathName, lineNumber, columnNumber, severity, lineText, message );
		diagnosticMessageConsumer.Invoke( diagnostic );
	}

	public void Run()
	{
		foreach( IFileSystem.Path contentPath in templateFileSystem.EnumerateItems() )
		{
			if( contentPath.Extension != ".html" )
			{
				templateFileSystem.Copy( contentPath, outputFileSystem, contentPath );
				continue;
			}
			extractViews( rootView, contentPath );
		}

		Helpers.PrintTree( rootView, view => views.Where( v => v.Parent == view ), view => view.ToString(), s => Log.Info( s ) );

		foreach( IFileSystem.Path contentPath in contentFileSystem.EnumerateItems() )
		{
			if( contentPath.Content.StartsWith2( "_" ) ) //TODO: get rid of
				continue;
			if( contentPath.Extension != ".md" )
			{
				contentFileSystem.Copy( contentPath, outputFileSystem, contentPath );
				continue;
			}
			ViewModel viewModel = getViewModel( contentPath, contentFileSystem );
			View view = findView( viewModel, rootView );
			string htmlText = view.Apply( viewModel );
			outputFileSystem.WriteAllText( contentPath.WithExtension( ".html" ), htmlText );
		}
	}

	static Html.HtmlNode getRootViewHtmlNode()
	{
		string html = """
<!DOCTYPE html>
<html>
	<head>
		<title>{{title}}</title>
    </head>
    <body>
		{{content}}
	</body>
</html>
""";
		Html.HtmlDocument templateDocument = new Html.HtmlDocument();
		templateDocument.LoadHtml( html );
		return templateDocument.DocumentNode;
	}

	void extractViews( View parentView, IFileSystem.Path templatePath )
	{
		string template = templateFileSystem.ReadAllText( templatePath );
		string diagnosticTemplatePathName = templateFileSystem.GetDiagnosticFullPath( templatePath );
		Html.HtmlDocument templateDocument = new Html.HtmlDocument();
		templateDocument.LoadHtml( template );
		foreach( Html.HtmlParseError parseError in templateDocument.ParseErrors )
			issueDiagnostic( diagnosticTemplatePathName, parseError.Line, parseError.LinePosition, Severity.Error, parseError.SourceText, parseError.Reason );
		Html.HtmlNode htmlNode = templateDocument.DocumentNode;
		htmlNode.Remove(); //try this
		View documentView = new ContentView( parentView, htmlNode, Name.Of( templatePath.Content ), new RegEx.Regex( ".*" ) );
		views.Add( documentView );
		recurse( documentView, htmlNode, diagnosticTemplatePathName );
		foreach( View view in views )
			view.HtmlNode.Remove();
		return;

		void recurse( View parentView, Html.HtmlNode parentNode, string diagnosticTemplatePathName )
		{
			foreach( Html.HtmlNode childNode in parentNode.ChildNodes )
			{
				View? childView = createChildView( parentView, childNode, diagnosticTemplatePathName );
				if( childView != null )
					views.Add( childView );
				recurse( childView ?? parentView, childNode, diagnosticTemplatePathName );
			}

			View? createChildView( View parentView, Html.HtmlNode htmlNode, string diagnosticTemplatePathName )
			{
				if( htmlNode.Name == Constants.ContentViewTagName )
				{
					Name name = getName( htmlNode );
					RegEx.Regex appliesTo = getAppliesTo( htmlNode );
					return new ContentView( parentView, htmlNode, name, appliesTo );
				}
				if( htmlNode.Name == Constants.CollectionViewTagName )
				{
					Name name = getName( htmlNode );
					RegEx.Regex appliesTo = getAppliesTo( htmlNode );
					Name elementViewName = getElementViewName( htmlNode, diagnosticTemplatePathName );
					return new CollectionView( parentView, htmlNode, name, appliesTo, elementViewName );
				}
				return null;

				static Name getName( Html.HtmlNode htmlNode )
				{
					Html.HtmlAttribute? nameAttribute = htmlNode.Attributes.AttributesWithName( Constants.NameAttributeName ).SingleOrDefault();
					return Name.Of( nameAttribute?.Value ?? htmlNode.XPath );
				}

				static RegEx.Regex getAppliesTo( Html.HtmlNode htmlNode )
				{
					Html.HtmlAttribute? appliesToAttribute = htmlNode.Attributes.AttributesWithName( Constants.AppliesToAttributeName ).SingleOrDefault();
					return new RegEx.Regex( appliesToAttribute?.Value ?? ".*" );
				}

				Name getElementViewName( Html.HtmlNode htmlNode, string diagnosticTemplatePathName )
				{
					Html.HtmlAttribute? elementViewAttribute = htmlNode.Attributes.AttributesWithName( Constants.ElementViewAttributeName ).SingleOrDefault();
					if( elementViewAttribute == null )
					{
						issueDiagnostic( diagnosticTemplatePathName, htmlNode.Line, htmlNode.LinePosition, Severity.Error, "", $"Collection view is missing a '{Constants.ElementViewAttributeName}' attribute" );
						return Name.Of( "" );
					}
					return Name.Of( elementViewAttribute.Value );
				}
			}
		}
	}

	static ViewModel getViewModel( IFileSystem.Path contentPath, IFileSystem contentFileSystem )
	{
		string markdownText = contentFileSystem.ReadAllText( contentPath );

		if( markdownText.IsWhitespace() )
		{
			SysText.StringBuilder stringBuilder = new();
			ImmutableArray<IFileSystem.Path> paths = contentFileSystem.EnumerateItems( contentPath ).Where( childPath => childPath.Content.EndsWith2( ".md" ) ).ToImmutableArray();
			return new CollectionViewModel( contentPath, paths );
		}

		string htmlText = convert( markdownText );
		htmlText = fixLinks( htmlText );
		return new ContentViewModel( contentPath, htmlText );
	}

	View findView( ViewModel viewModel, View? parentView )
	{
		while( true )
		{
			IEnumerable<View> visibleViews = views.Where( view => view.Parent == parentView ).Collect();
			IReadOnlyList<View> applicableViews = visibleViews.Where( a => a.IsApplicableTo( viewModel ) ).Collect();
			if( applicableViews.Count > 0 )
			{
				if( applicableViews.Count > 1 )
				{
					Log.Warn( $"More than one view is applicable to {viewModel}" );
					string underMessage = parentView == null ? "" : $" under {parentView.Name.Content}";
					issueDiagnostic( viewModel.Path.Content, 1, 1, Severity.Warn, null, $"More than one view{underMessage} is applicable to {viewModel}" );
				}
				return applicableViews[0];
			}
			Assert( parentView != null ); //a view is guaranteed to be found because the root view matches everything
			parentView = parentView.Parent;
		}
	}

	static string fixLinks( string htmlText )
	{
		if( htmlText == "" )
			return htmlText;
		Html.HtmlDocument htmlDocument = new();
		htmlDocument.LoadHtml( htmlText );
		foreach( Html.HtmlNode linkNode in htmlDocument.DocumentNode.Descendants( "a" ) )
		{
			Html.HtmlAttribute hrefAttribute = linkNode.Attributes["href"];
			string href = hrefAttribute.Value;
			if( href == "" || href.StartsWith2( "#" ) )
				continue;
			if( href.StartsWith2( "http://" ) || href.StartsWith2( "https://" ) )
				continue;
			if( href.EndsWith2( ".md" ) )
			{
				href = IFileSystem.Path.Of( hrefAttribute.Value ).WithExtension( ".html" ).Content;
				hrefAttribute.Value = href;
			}
		}
		return htmlDocument.DocumentNode.OuterHtml;
	}

	static string convert( string markdownText )
	{
		Markdig.MarkdownPipeline pipeline = new Markdig.MarkdownPipelineBuilder()
			//.UseAdvancedExtensions()
			.UseYamlFrontMatter()
			.UsePipeTables()
			.UseFootnotes()
			.UseEmphasisExtras()
			.UseListExtras()
			.UseImageAsFigure()
			//.UseUrlRewriter( link =>
			//{
			//	return link.Url.OrThrow();
			//} )
			.Build();

		MarkdigSyntax.MarkdownDocument document = Markdig.Parsers.MarkdownParser.Parse( markdownText, pipeline );
		return Markdig.Markdown.ToHtml( document, pipeline );
	}

	//static string getOnlyText( MarkdigSyntax.MarkdownDocument document )
	//{
	//	SysText.StringBuilder stringBuilder = new SysText.StringBuilder();
	//	SysIo.StringWriter stringWriter = new( stringBuilder );
	//	MarkDigNormalize.NormalizeOptions options = new();
	//	options.SpaceAfterQuoteBlock = true;
	//	options.EmptyLineAfterCodeBlock = true;
	//	options.EmptyLineAfterHeading = true;
	//	options.EmptyLineAfterThematicBreak = true;
	//	options.ExpandAutoLinks = true;
	//	options.ListItemCharacter = null;
	//	MarkDigNormalize.NormalizeRenderer renderer = new MarkDigNormalize.NormalizeRenderer( stringWriter, options );
	//	Markdig.MarkdownPipeline pipeline = new Markdig.MarkdownPipelineBuilder().Build();
	//	pipeline.Setup( renderer );
	//	MarkdigSyntax.Block? frontMatter = null;
	//	if( document.Count > 0 && document[0] is MarkdigYaml.YamlFrontMatterBlock )
	//	{
	//		frontMatter = document[0];
	//		document.RemoveAt( 0 );
	//	}
	//	renderer.Render( document );
	//	if( frontMatter != null )
	//		document.Insert( 0, frontMatter );
	//	stringWriter.Flush();
	//	return stringBuilder.ToString().Trim();
	//}
}
