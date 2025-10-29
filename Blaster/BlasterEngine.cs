namespace Blaster;

using System.Collections.Immutable;
using MarkdigExtensions;
using MikeNakis.Kit;
using MikeNakis.Kit.Collections;
using MikeNakis.Kit.Extensions;
using static Markdig.MarkdownExtensions;
using static MikeNakis.Kit.GlobalStatics;
using Html = HtmlAgilityPack;
using Markdig = Markdig;
using MarkdigSyntax = Markdig.Syntax;

sealed class View
{
	public readonly View? Parent;
	public readonly Html.HtmlNode Element;

	public View( View? parent, Html.HtmlNode element )
	{
		Parent = parent;
		Element = element;
	}
}

public sealed class BlasterEngine
{
	public static void Run( IFileSystem contentFileSystem, IFileSystem templateFileSystem, IFileSystem targetFileSystem )
	{
		string template = templateFileSystem.ReadAllText( IFileSystem.Path.Of( "template.html" ) );
		ImmutableArray<View> views = collectViews( template );
		printTree( views[0], view => views.Where( v => v.Parent == view ), view => view.Element!.Name, s => Log.Info( s ) );

		foreach( IFileSystem.Path contentPath in contentFileSystem.EnumerateItems() )
		{
			if( contentPath.Content.StartsWith2( "_" ) ) //TODO: get rid of
				continue;
			if( contentPath.Extension != ".md" )
			{
				contentFileSystem.Copy( contentPath, targetFileSystem, contentPath );
			}
			else
			{
				string markdownText = contentFileSystem.ReadAllText( contentPath );

				if( markdownText.IsWhitespace() )
				{
					SysText.StringBuilder stringBuilder = new();
					foreach( IFileSystem.Path childPath in contentFileSystem.EnumerateItems( contentPath ) )
					{
						if( !childPath.Content.EndsWith2( ".md" ) )
							continue;
						stringBuilder.Append( '[' ).Append( childPath.Content ).Append( "](" ).Append( childPath.Content ).Append( ")\r\n\r\n" );
					}
					markdownText = stringBuilder.ToString();
				}

				string htmlText = convert( markdownText );
				htmlText = fixLinks( htmlText );

				IFileSystem.Path targetPath = contentPath.WithExtension( ".html" );
				targetFileSystem.WriteAllText( targetPath, htmlText );
			}
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

	static ImmutableArray<View> collectViews( string template )
	{
		Html.HtmlDocument templateDocument = new Html.HtmlDocument();
		templateDocument.LoadHtml( template );
		Html.HtmlNode rootNode = templateDocument.DocumentNode;
		View rootView = new View( null, rootNode );
		List<View> allViews = new List<View>();
		allViews.Add( rootView );
		recurse( rootView, rootNode, allViews );
		foreach( View view in allViews )
			view.Element.Remove();
		return allViews.ToImmutableArray();

		static void recurse( View parentView, Html.HtmlNode parentNode, List<View> allViews )
		{
			foreach( Html.HtmlNode childNode in parentNode.ChildNodes )
			{
				if( childNode.Name == "section" )
				{
					View childView = new View( parentView, childNode );
					allViews.Add( childView );
					recurse( childView, childNode, allViews );
				}
				else
				{
					recurse( parentView, childNode, allViews );
				}
			}
		}
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

	static void printTree<T>( T rootNode, Sys.Func<T, IEnumerable<T>> breeder, Sys.Func<T, string> stringizer, Sys.Action<string> emitter, int indentation = 1 )
	{
		Assert( indentation >= 1 );

		const char verticalAndRight = '\u251c'; // Unicode U+251C "Box Drawings Light Vertical and Right"
		const char horizontal = '\u2500'; // Unicode U+2500 "Box Drawings Light Horizontal"
		const char upAndRight = '\u2514'; // Unicode U+2514 "Box Drawings Light Up and Right"
		const char vertical = '\u2502'; // Unicode U+2502 "Box Drawings Light Vertical"
		const char blackSquare = '\u25a0'; // Unicode U+25A0 "Black Square"

		string parentIndentation = new string( horizontal, indentation );
		string childIndentation = new string( ' ', indentation );
		string nonLastParentPrefix = $"{verticalAndRight}{parentIndentation}";
		string lastParentPrefix = $"{upAndRight}{parentIndentation}";
		string nonLastChildPrefix = $"{vertical}{childIndentation}";
		string lastChildPrefix = $" {childIndentation}";
		string terminal = $"{blackSquare} ";

		SysText.StringBuilder stringBuilder = new();
		recurse( "", rootNode, "" );
		return;

		void recurse( string parentPrefix, T node, string childPrefix )
		{
			int position = stringBuilder.Length;
			stringBuilder.Append( parentPrefix ).Append( terminal );
			stringBuilder.Append( stringizer.Invoke( node ) );
			emitter.Invoke( stringBuilder.ToString() );
			stringBuilder.Length = position;
			stringBuilder.Append( childPrefix );
			IReadOnlyList<T> children = breeder.Invoke( node ).Collect();
			foreach( T childNode in children.Take( children.Count - 1 ) )
				recurse( nonLastParentPrefix, childNode, nonLastChildPrefix );
			if( children.Count > 0 )
				recurse( lastParentPrefix, children[^1], lastChildPrefix );
			stringBuilder.Length = position;
		}
	}
}
