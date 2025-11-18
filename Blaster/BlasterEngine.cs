namespace Blaster;

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using MarkdigExtensions;
using MikeNakis.Kit;
using MikeNakis.Kit.Collections;
using MikeNakis.Kit.Extensions;
using static Markdig.MarkdownExtensions;
using static Markdig.Syntax.MarkdownObjectExtensions;
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

	BlasterEngine( FileSystem contentFileSystem, FileSystem templateFileSystem, FileSystem outputFileSystem, Sys.Action<Diagnostic> diagnosticConsumer )
	{
		this.contentFileSystem = contentFileSystem;
		this.templateFileSystem = templateFileSystem;
		this.outputFileSystem = outputFileSystem;
		this.diagnosticConsumer = diagnosticConsumer;
	}

	void issueDiagnostic( Diagnostic diagnostic )
	{
		diagnosticConsumer.Invoke( diagnostic );
	}

	public void Run()
	{
		outputFileSystem.Clear();
		List<View> topLevelViews = collectTopLevelViews();
		generateHtmlFiles( topLevelViews );
	}

	List<View> collectTopLevelViews()
	{
		List<View> topLevelViews = new();

		foreach( FileItem templateItem in templateFileSystem.EnumerateItems() )
		{
			if( templateItem.FileName.Extension == ".html" )
			{
				View topLevelView = extractTopLevelView( templateItem );
				topLevelViews.Add( topLevelView );
			}
			else
				outputFileSystem.CopyFrom( templateItem );
		}

		foreach( View topLevelView in topLevelViews )
			Helpers.PrintTree( topLevelView, view => view.Children, view => $"{view}", s => Log.Debug( s ) );

		return topLevelViews;
	}

	View extractTopLevelView( FileItem templateItem )
	{
		Name name = Name.Of( templateItem.FileName.Content );
		Html.HtmlNode htmlNode = getHtmlNode( templateItem );
		ImmutableArray<View> childViews = extractChildViews( name.Content, htmlNode, templateItem );
		htmlNode.Remove();
		return new ContentView( name, new RegEx.Regex( ".*" ), childViews, htmlNode.InnerHtml );
	}

	Html.HtmlNode getHtmlNode( FileItem templateItem )
	{
		string template = templateItem.ReadAllText();
		Html.HtmlDocument templateDocument = new Html.HtmlDocument();
		templateDocument.LoadHtml( template );
		foreach( Html.HtmlParseError parseError in templateDocument.ParseErrors )
			issueDiagnostic( new HtmlParseDiagnostic( templateItem, parseError.Line, parseError.LinePosition, 1, parseError.Reason ) );
		return templateDocument.DocumentNode;
	}

	ImmutableArray<View> extractChildViews( string namePrefix, Html.HtmlNode parentHtmlNode, FileItem templateItem )
	{
		List<View> allChildViews = new();
		foreach( Html.HtmlNode htmlNode in parentHtmlNode.ChildNodes.ToImmutableArray() )
		{
			if( htmlNode.Name == Constants.ContentViewTagName )
			{
				RegEx.Regex appliesTo = getAppliesTo( htmlNode );
				Name name = Name.Of( $"{namePrefix} / {appliesTo}" );
				ImmutableArray<View> childViews = extractChildViews( name.Content, htmlNode, templateItem );
				htmlNode.Remove();
				View childView = new ContentView( name, appliesTo, childViews, htmlNode.InnerHtml );
				allChildViews.Add( childView );
			}
			else if( htmlNode.Name == Constants.CollectionViewTagName )
			{
				RegEx.Regex appliesTo = getAppliesTo( htmlNode );
				Name name = Name.Of( $"{namePrefix} / {appliesTo}" );
				ImmutableArray<View> childViews = extractChildViews( name.Content, htmlNode, templateItem );
				Name elementViewName = getElementViewName( htmlNode, templateItem );
				htmlNode.Remove();
				View childView = new CollectionView( name, appliesTo, childViews, htmlNode.InnerHtml, elementViewName );
				allChildViews.Add( childView );
			}
			else
			{
				ImmutableArray<View> childViews = extractChildViews( namePrefix, htmlNode, templateItem );
				allChildViews.AddRange( childViews );
			}
		}
		return allChildViews.ToImmutableArray();

		static RegEx.Regex getAppliesTo( Html.HtmlNode htmlNode )
		{
			Html.HtmlAttribute? appliesToAttribute = htmlNode.Attributes.AttributesWithName( Constants.AppliesToAttributeName ).SingleOrDefault();
			string pattern = appliesToAttribute?.Value ?? "*";
			if( pattern.StartsWith2( "*." ) )
				pattern = ".*\\." + pattern[2..];
			else if( pattern.StartsWith2( "*" ) )
				pattern = ".*" + pattern[1..];
			return new RegEx.Regex( pattern );
		}
	}

	void generateHtmlFiles( IReadOnlyList<View> topLevelViews )
	{
		foreach( FileItem contentItem in contentFileSystem.EnumerateItems() )
		{
			if( contentItem.FileName.Content.StartsWith2( "_" ) ) //TODO: get rid of
				continue;
			if( contentItem.FileName.Extension == ".md" )
				htmlFromMarkdown( topLevelViews, contentItem );
			else
				outputFileSystem.CopyFrom( contentItem );
		}
	}

	void htmlFromMarkdown( IReadOnlyList<View> topLevelViews, FileItem contentItem )
	{
		ViewModel viewModel = getViewModel( contentItem );
		View view = findView( viewModel, topLevelViews );
		string htmlText = applyViewToViewModel( viewModel, view, topLevelViews );
		FileItem outputItem = outputFileSystem.CreateItem( contentItem.FileName.WithReplacedExtension( ".html" ) );
		outputItem.WriteAllText( htmlText );
	}

	static readonly View noView = getNoView();

	static View getNoView()
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
		return new ContentView( Name.Of( "noView" ), new RegEx.Regex( ".*" ), ImmutableArray<View>.Empty, templateDocument.DocumentNode.InnerHtml );
	}

	View findView( ViewModel viewModel, IReadOnlyList<View> topLevelViews )
	{
		List<View> views = new();
		foreach( View topLevelView in topLevelViews )
			findViews( viewModel, topLevelView, views );
		if( views.Count == 0 )
		{
			issueDiagnostic( new CustomDiagnostic( Severity.Warn, viewModel.Item, 1, 1, 1, $"No view found for {viewModel}" ) );
			return noView;
		}
		if( views.Count > 1 )
			issueDiagnostic( new CustomDiagnostic( Severity.Warn, viewModel.Item, 1, 1, 1, $"Multiple views found for {viewModel}: {views.Select( view => $"\"{view.Name}\"" ).MakeString( ", " )}" ) );
		return views[0];
	}

	static void findViews( ViewModel viewModel, View parentView, List<View> views )
	{
		if( parentView.IsApplicableTo( viewModel ) )
		{
			int n = views.Count;
			foreach( View childView in parentView.Children )
				findViews( viewModel, childView, views );
			if( views.Count == n )
				views.Add( parentView );
		}
	}

	string applyViewToViewModel( ContentBase viewModel, View view, IReadOnlyList<View> topLevelViews )
	{
		while( true )
		{
			string htmlText = view.Apply( viewModel );
			View? parentView = getParentView( view, topLevelViews );
			if( parentView == null )
				return htmlText;
			view = parentView;
			viewModel = new HtmlContent( viewModel, htmlText );
		}
	}

	static View? getParentView( View view, IReadOnlyList<View> topLevelViews )
	{
		foreach( View topLevelView in topLevelViews )
		{
			View? found = getParentView( view, topLevelView );
			if( found != null )
				return found;
		}
		return null;

		static View? getParentView( View view, View parentView )
		{
			foreach( View childView in parentView.Children )
			{
				if( childView == view )
					return parentView;
				View? found = getParentView( view, childView );
				if( found != null )
					return found;
			}
			return null;
		}
	}

	Name getElementViewName( Html.HtmlNode htmlNode, FileItem templateItem )
	{
		Html.HtmlAttribute? elementViewAttribute = htmlNode.Attributes.AttributesWithName( Constants.ElementViewAttributeName ).SingleOrDefault();
		if( elementViewAttribute == null )
		{
			issueDiagnostic( new CustomDiagnostic( Severity.Error, templateItem, htmlNode.Line, htmlNode.LinePosition, 1, $"Collection view is missing a '{Constants.ElementViewAttributeName}' attribute" ) );
			return Name.Of( "" );
		}
		return Name.Of( elementViewAttribute.Value );
	}

	ViewModel getViewModel( FileItem contentItem )
	{
		string markdownText = contentItem.ReadAllText();

		if( markdownText.IsWhitespace() )
		{
			SysText.StringBuilder stringBuilder = new();
			ImmutableArray<FileItem> siblingItems = contentItem.FileSystem //
				.EnumerateItems( contentItem.FileName.DirectoryName ) //
				.Where( siblingItem => siblingItem.FileName.HasExtension( ".md" ) ) //
				.ToImmutableArray();
			return new CollectionViewModel( contentItem, siblingItems );
		}

		string htmlText = convert( markdownText, contentItem );
		htmlText = fixLinks( htmlText );
		return new ContentViewModel( contentItem, htmlText );
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
				hrefAttribute.Value = href[..^3] + ".html";
		}
		return htmlDocument.DocumentNode.OuterHtml;
	}

	string convert( string markdownText, FileItem contentItem )
	{
		Markdig.MarkdownPipeline pipeline = new Markdig.MarkdownPipelineBuilder()
			//.UseAdvancedExtensions()
			.UseYamlFrontMatter()
			.UseEmphasisExtras()
			.UseGridTables()
			.UseMediaLinks()
			.UsePipeTables()
			.UseFootnotes()
			.UseListExtras()
			//.UseImageAsFigure()
			//.UseUrlRewriter( link =>
			//{
			//	return link.Url.OrThrow();
			//} )
			.UseImageAsFigure() //this is probably only needed for rendering to html
			.UseGenericAttributes() // Must be last as it is one parser that is modifying other parsers
			.Build();
		MarkdigSyntax.MarkdownDocument document = Markdig.Parsers.MarkdownParser.Parse( markdownText, pipeline );
		IEnumerable<MarkdigSyntax.Inlines.LinkInline> links = document.Descendants() //
			.OfType<MarkdigSyntax.Inlines.LinkInline>();
		foreach( MarkdigSyntax.Inlines.LinkInline link in links )
		{
			string? url = link.Url;
			if( url == null )
				continue;
			if( url == "" || url.StartsWith2( "#" ) )
				continue;
			if( url.StartsWith2( "http://" ) || url.StartsWith2( "https://" ) )
				continue;
			if( url.EndsWith2( ".md" ) )
			{
				FileName fileName = FileName.AbsoluteOrRelative( url, contentItem.FileName.DirectoryName );
				if( !contentItem.FileSystem.Exists( fileName ) )
				{
					(int lineNumber, int columnNumber, int length) = Helpers.GetSpanInformation( contentItem, link.UrlSpan.Start, link.UrlSpan.End );
					issueDiagnostic( new BrokenLinkDiagnostic( contentItem, lineNumber, columnNumber, length, fileName ) );
				}
			}
		}
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
