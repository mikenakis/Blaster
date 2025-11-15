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
	public static readonly Sys.Action<Diagnostic> DefaultDiagnosticConsumer = diagnostic =>
	{
		foreach( string line in diagnostic.ToString().Split( "\n" ) )
			Log.Info( line );
	};

	public static void Run( FileSystem contentFileSystem, FileSystem templateFileSystem, FileSystem outputFileSystem, Sys.Action<Diagnostic> diagnosticConsumer )
	{
		BlasterEngine blasterEngine = new( contentFileSystem, templateFileSystem, outputFileSystem, diagnosticConsumer );
		blasterEngine.Run();
	}

	readonly FileSystem contentFileSystem;
	readonly FileSystem templateFileSystem;
	readonly FileSystem outputFileSystem;
	readonly Sys.Action<Diagnostic> diagnosticConsumer;
	readonly List<View> views = new List<View>();
	readonly View rootView = new ContentView( null, getRootViewHtmlNode(), Name.Of( "root" ), new RegEx.Regex( ".*" ) );

	BlasterEngine( FileSystem contentFileSystem, FileSystem templateFileSystem, FileSystem outputFileSystem, Sys.Action<Diagnostic> diagnosticConsumer )
	{
		this.contentFileSystem = contentFileSystem;
		this.templateFileSystem = templateFileSystem;
		this.outputFileSystem = outputFileSystem;
		this.diagnosticConsumer = diagnosticConsumer;
		views.Add( rootView );
	}

	void issueDiagnostic( Diagnostic diagnostic )
	{
		diagnosticConsumer.Invoke( diagnostic );
	}

	public void Run()
	{
		foreach( FileSystem.Item contentItem in templateFileSystem.EnumerateItems() )
		{
			if( contentItem.Path.Extension != ".html" )
			{
				outputFileSystem.CopyFrom( contentItem );
				continue;
			}
			extractViews( rootView, contentItem );
		}

		Helpers.PrintTree( rootView, view => views.Where( v => v.Parent == view ), view => view.ToString(), s => Log.Info( s ) );

		foreach( FileSystem.Item contentItem in contentFileSystem.EnumerateItems() )
		{
			if( contentItem.Path.Content.StartsWith2( "_" ) ) //TODO: get rid of
				continue;
			if( contentItem.Path.Extension != ".md" )
			{
				outputFileSystem.CopyFrom( contentItem );
				continue;
			}
			ViewModel viewModel = getViewModel( contentItem, contentFileSystem );
			View view = findView( viewModel, rootView );
			string htmlText = view.Apply( viewModel );
			FileSystem.Item outputItem = outputFileSystem.CreateItem( contentItem.Path.WithExtension( ".html" ) );
			outputItem.WriteAllText( htmlText );
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

	void extractViews( View parentView, FileSystem.Item templateItem )
	{
		string template = templateItem.ReadAllText();
		Html.HtmlDocument templateDocument = new Html.HtmlDocument();
		templateDocument.LoadHtml( template );
		foreach( Html.HtmlParseError parseError in templateDocument.ParseErrors )
			issueDiagnostic( new HtmlParseDiagnostic( Severity.Error, templateItem, parseError.Line, parseError.LinePosition, parseError.Code, parseError.Reason ) );
		Html.HtmlNode htmlNode = templateDocument.DocumentNode;
		htmlNode.Remove(); //try this
		View documentView = new ContentView( parentView, htmlNode, Name.Of( templateItem.Path.Content ), new RegEx.Regex( ".*" ) );
		views.Add( documentView );
		recurse( documentView, htmlNode, templateItem );
		foreach( View view in views )
			view.HtmlNode.Remove();
		return;

		void recurse( View parentView, Html.HtmlNode parentNode, FileSystem.Item templateItem )
		{
			foreach( Html.HtmlNode childNode in parentNode.ChildNodes )
			{
				View? childView = createChildView( parentView, childNode, templateItem );
				if( childView != null )
					views.Add( childView );
				recurse( childView ?? parentView, childNode, templateItem );
			}

			View? createChildView( View parentView, Html.HtmlNode htmlNode, FileSystem.Item templateItem )
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
					Name elementViewName = getElementViewName( htmlNode, templateItem );
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

				Name getElementViewName( Html.HtmlNode htmlNode, FileSystem.Item templateItem )
				{
					Html.HtmlAttribute? elementViewAttribute = htmlNode.Attributes.AttributesWithName( Constants.ElementViewAttributeName ).SingleOrDefault();
					if( elementViewAttribute == null )
					{
						issueDiagnostic( new CustomDiagnostic( Severity.Error, templateItem, htmlNode.Line, htmlNode.LinePosition, $"Collection view is missing a '{Constants.ElementViewAttributeName}' attribute" ) );
						return Name.Of( "" );
					}
					return Name.Of( elementViewAttribute.Value );
				}
			}
		}
	}

	static ViewModel getViewModel( FileSystem.Item contentItem, FileSystem contentFileSystem )
	{
		string markdownText = contentItem.ReadAllText();

		if( markdownText.IsWhitespace() )
		{
			SysText.StringBuilder stringBuilder = new();
			ImmutableArray<FileSystem.Item> siblingItems = contentFileSystem.EnumerateSiblingItems( contentItem ).Where( siblingItem => siblingItem.Path.Content.EndsWith2( ".md" ) ).ToImmutableArray();
			return new CollectionViewModel( contentItem, siblingItems );
		}

		string htmlText = convert( markdownText );
		htmlText = fixLinks( htmlText );
		return new ContentViewModel( contentItem, htmlText );
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
					issueDiagnostic( new CustomDiagnostic( Severity.Warn, viewModel.Item, 1, 1, $"More than one view{underMessage} is applicable to {viewModel}" ) );
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
				href = FileSystem.Path.Of( hrefAttribute.Value ).WithExtension( ".html" ).Content;
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
