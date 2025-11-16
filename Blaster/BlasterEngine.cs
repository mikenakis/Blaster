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
		foreach( FileSystem.Item templateItem in templateFileSystem.EnumerateItems() )
		{
			if( templateItem.FileName.Extension == ".html" )
				extractViews( rootView, templateItem );
			else
				outputFileSystem.CopyFrom( templateItem );
		}

		Helpers.PrintTree( rootView, view => getChildViews( view, views ), view => view.ToString(), s => Log.Debug( s ) );

		foreach( FileSystem.Item contentItem in contentFileSystem.EnumerateItems() )
		{
			if( contentItem.FileName.Content.StartsWith2( "_" ) ) //TODO: get rid of
				continue;
			if( contentItem.FileName.Extension == ".md" )
				htmlFromMarkdown( contentItem );
			else
				outputFileSystem.CopyFrom( contentItem );
		}
	}

	void htmlFromMarkdown( FileSystem.Item contentItem )
	{
		ViewModel viewModel = getViewModel( contentItem );
		View view = tryFindView( viewModel, rootView ).OrThrow(); //a view is guaranteed to be found because the root view matches everything
		string htmlText = view.Apply( viewModel );
		FileSystem.Item outputItem = outputFileSystem.CreateItem( contentItem.FileName.WithoutExtension.WithExtension( ".html" ) );
		outputItem.WriteAllText( htmlText );
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

	void extractViews( View rootView, FileSystem.Item templateItem )
	{
		Html.HtmlNode htmlNode = getHtmlNode( templateItem );
		View newRootView = new ContentView( rootView, htmlNode, Name.Of( templateItem.FileName.Content ), new RegEx.Regex( ".*" ) );
		views.Add( newRootView );
		recurse( newRootView, htmlNode, templateItem );
		foreach( View view in views )
			view.HtmlNode.Remove(); //FIXME this is wrong: the nodes should have been removed earlier.
		return;

		Html.HtmlNode getHtmlNode( FileSystem.Item templateItem )
		{
			string template = templateItem.ReadAllText();
			Html.HtmlDocument templateDocument = new Html.HtmlDocument();
			templateDocument.LoadHtml( template );
			foreach( Html.HtmlParseError parseError in templateDocument.ParseErrors )
				issueDiagnostic( new HtmlParseDiagnostic( templateItem, parseError ) );
			return templateDocument.DocumentNode;
		}

		void recurse( View parentView, Html.HtmlNode parentNode, FileSystem.Item templateItem )
		{
			foreach( Html.HtmlNode childNode in parentNode.ChildNodes.ToImmutableArray() )
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
					string pattern = appliesToAttribute?.Value ?? "*";
					if( pattern.StartsWith2( "*." ) )
						pattern = ".*\\." + pattern[2..];
					else if( pattern.StartsWith2( "*" ) )
						pattern = ".*" + pattern[1..];
					return new RegEx.Regex( pattern );
				}

				Name getElementViewName( Html.HtmlNode htmlNode, FileSystem.Item templateItem )
				{
					Html.HtmlAttribute? elementViewAttribute = htmlNode.Attributes.AttributesWithName( Constants.ElementViewAttributeName ).SingleOrDefault();
					if( elementViewAttribute == null )
					{
						issueDiagnostic( new CustomDiagnostic( Severity.Error, templateItem, htmlNode.Line, htmlNode.LinePosition, 1, $"Collection view is missing a '{Constants.ElementViewAttributeName}' attribute" ) );
						return Name.Of( "" );
					}
					return Name.Of( elementViewAttribute.Value );
				}
			}
		}
	}

	ViewModel getViewModel( FileSystem.Item contentItem )
	{
		string markdownText = contentItem.ReadAllText();

		if( markdownText.IsWhitespace() )
		{
			SysText.StringBuilder stringBuilder = new();
			ImmutableArray<FileSystem.Item> siblingItems = contentItem.FileSystem //
				.EnumerateItems( contentItem.FileName.DirectoryName ) //
				.Where( siblingItem => siblingItem.FileName.HasExtension( ".md" ) ) //
				.ToImmutableArray();
			return new CollectionViewModel( contentItem, siblingItems );
		}

		string htmlText = convert( markdownText, contentItem );
		htmlText = fixLinks( htmlText, contentItem );
		return new ContentViewModel( contentItem, htmlText );
	}

	View? tryFindView( ViewModel viewModel, View parentView )
	{
		IEnumerable<View> visibleViews = getChildViews( parentView, views ).Collect();
		IReadOnlyList<View> applicableViews = visibleViews.Where( a => a.IsApplicableTo( viewModel ) ).Collect();
		if( applicableViews.Count > 0 )
		{
			if( applicableViews.Count > 1 )
			{
				Log.Warn( $"More than one view is applicable to {viewModel}" );
				string underMessage = parentView == null ? "" : $" under {parentView.Name.Content}";
				issueDiagnostic( new CustomDiagnostic( Severity.Warn, viewModel.Item, 1, 1, 1, $"More than one view{underMessage} is applicable to {viewModel}" ) );
			}
			return applicableViews[0];
		}
		if( parentView.IsApplicableTo( viewModel ) )
			return parentView;
		return null;
	}

	IEnumerable<View> getChildViews( View parentView, IReadOnlyList<View> views )
	{
		return views.Where( view => view.Parent == parentView );
	}

	static string fixLinks( string htmlText, FileSystem.Item contentItem )
	{
		FileSystem.DirectoryName directoryName = contentItem.FileName.DirectoryName;
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
				FileSystem.FileName fileName = FileSystem.FileName.AbsoluteOrRelative( hrefAttribute.Value, directoryName );
				hrefAttribute.Value = fileName.WithoutExtension.WithExtension( ".html" ).Content;
			}
		}
		return htmlDocument.DocumentNode.OuterHtml;
	}

	string convert( string markdownText, FileSystem.Item contentItem )
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
				FileSystem.FileName fileName = FileSystem.FileName.AbsoluteOrRelative( url, contentItem.FileName.DirectoryName );
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
