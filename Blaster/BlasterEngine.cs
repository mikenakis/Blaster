namespace Blaster;

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Framework.Codecs;
using MarkdigExtensions;
using MikeNakis.Kit;
using MikeNakis.Kit.Collections;
using MikeNakis.Kit.Extensions;
using static System.MemoryExtensions;
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
	const string contentViewTagName = "content-view";
	const string collectionViewTagName = "collection-view";
	const string elementViewAttributeName = "element-view";
	const string nameAttributeName = "name";
	const string appliesToAttributeName = "applies-to";

	readonly struct Name : Sys.IComparable<Name>, Sys.IEquatable<Name>
	{
		public static bool operator ==( Name left, Name right ) => left.Equals( right );
		public static bool operator !=( Name left, Name right ) => !left.Equals( right );

		public static Name Of( string content )
		{
			return new Name( content );
		}

		public static readonly Codec<Name> Codec = new StringRepresentationCodec<Name>( s => new Name( s ), k => k.Content );

		public string Content { get; init; }

		Name( string content )
		{
			Content = content;
		}

		public int CompareTo( Name other ) => StringCompare( Content, other.Content );
		[Sys.Obsolete] public override bool Equals( object? other ) => other is Name kin && Equals( kin );
		public override int GetHashCode() => Content.GetHashCode( Sys.StringComparison.Ordinal );
		public bool Equals( Name other ) => CompareTo( other ) == 0;
		public override string ToString() => Content;
	}

	abstract class View
	{
		public readonly View? Parent;
		public readonly Html.HtmlNode HtmlNode;
		public readonly Name Name;
		readonly RegEx.Regex appliesTo;

		protected View( View? parent, Html.HtmlNode htmlNode, Name name, RegEx.Regex appliesTo )
		{
			Parent = parent;
			HtmlNode = htmlNode;
			Name = name;
			this.appliesTo = appliesTo;
		}

		public virtual bool IsApplicableTo( ViewModel viewModel )
		{
			return appliesTo.IsMatch( viewModel.Path.Content );
		}

		public abstract string Apply( ViewModel viewModel );

		public override string ToString() => $"{GetType().Name} \"{Name}\"";
	}

	sealed class ContentView : View
	{
		public readonly TemplateEngine TemplateEngine;

		public ContentView( View? parent, Html.HtmlNode htmlNode, Name name, RegEx.Regex appliesTo )
			: base( parent, htmlNode, name, appliesTo )
		{
			Assert( htmlNode.Name is contentViewTagName or "#document" );
			TemplateEngine = TemplateEngine.Create( htmlNode.InnerHtml );
		}

		public override bool IsApplicableTo( ViewModel viewModel )
		{
			if( viewModel is not ContentViewModel )
				return false;
			return base.IsApplicableTo( viewModel );
		}

		public override string Apply( ViewModel viewModel )
		{
			ContentViewModel contentViewModel = (ContentViewModel)viewModel;
			return TemplateEngine.GenerateText( name => name switch
				{
					"title" => contentViewModel.Path.Content,
					"content" => contentViewModel.HtmlText,
					_ => "?"
				} );
		}
	}

	sealed class CollectionView : View
	{
		public readonly Name ElementViewName;

		public CollectionView( View? parent, Html.HtmlNode htmlNode, Name name, RegEx.Regex appliesTo, Name elementViewName )
			: base( parent, htmlNode, name, appliesTo )
		{
			Assert( htmlNode.Name == collectionViewTagName );
			ElementViewName = elementViewName;
		}

		public override bool IsApplicableTo( ViewModel viewModel )
		{
			if( viewModel is not CollectionViewModel )
				return false;
			return base.IsApplicableTo( viewModel );
		}

		public override string Apply( ViewModel viewModel )
		{
			CollectionViewModel collectionViewModel = (CollectionViewModel)viewModel;
			return collectionViewModel.Path.Content; //XXX
		}
	}

	abstract class ViewModel
	{
		public readonly IFileSystem.Path Path;

		protected ViewModel( IFileSystem.Path path )
		{
			Path = path;
		}

		public override string ToString() => $"{GetType().Name} \"{Path}\"";
	}

	sealed class ContentViewModel : ViewModel
	{
		public readonly string HtmlText;

		public ContentViewModel( IFileSystem.Path path, string htmlText )
			: base( path )
		{
			HtmlText = htmlText;
		}
	}

	sealed class CollectionViewModel : ViewModel
	{
		public readonly ImmutableArray<IFileSystem.Path> Paths;

		public CollectionViewModel( IFileSystem.Path path, ImmutableArray<IFileSystem.Path> paths )
			: base( path )
		{
			Paths = paths;
		}
	}

	public class DiagnosticMessage
	{
		public string SourceFilePathName { get; }
		public int LineNumber { get; }
		public int ColumnNumber { get; }
		public string LineText { get; }
		public string Message { get; }

		public DiagnosticMessage( string sourceFilePathName, int lineNumber, int columnNumber, string lineText, string message )
		{
			SourceFilePathName = sourceFilePathName;
			LineNumber = lineNumber;
			ColumnNumber = columnNumber;
			LineText = lineText;
			Message = message;
		}
	}

	public static readonly Sys.Action<DiagnosticMessage> DefaultDiagnosticMessageConsumer = diagnosticMessage =>
	{
		d( $"{diagnosticMessage.SourceFilePathName}({diagnosticMessage.LineNumber},{diagnosticMessage.ColumnNumber}): {diagnosticMessage.Message}" );
		d( $"    {diagnosticMessage.LineText}" );
		d( $"    {new string( ' ', diagnosticMessage.ColumnNumber )}^" );
		return;

		static void d( string s )
		{
			Log.Info( s );
		}
	};

	public static void Run( IFileSystem contentFileSystem, IFileSystem templateFileSystem, IFileSystem targetFileSystem, Sys.Action<DiagnosticMessage> diagnosticMessageConsumer )
	{
		List<View> views = new List<View>();
		foreach( IFileSystem.Path contentPath in templateFileSystem.EnumerateItems() )
		{
			if( contentPath.Extension != ".html" )
			{
				templateFileSystem.Copy( contentPath, targetFileSystem, contentPath );
				continue;
			}
			extractViews( templateFileSystem, contentPath, views, diagnosticMessageConsumer );
		}

		//ImmutableArray<View> views = getViews( templateFileSystem, diagnosticMessageConsumer );
		Assert( views.Count > 0 );
		Assert( views[0].Name == Name.Of( "root" ) );
		Assert( views[0] is ContentView );
		Assert( ((ContentView)views[0]).TemplateEngine.FieldNames.Contains( "content" ) );
		printTree( views[0], view => views.Where( v => v.Parent == view ), view => view.HtmlNode!.Name, s => Log.Info( s ) );

		foreach( IFileSystem.Path contentPath in contentFileSystem.EnumerateItems() )
		{
			if( contentPath.Content.StartsWith2( "_" ) ) //TODO: get rid of
				continue;
			if( contentPath.Extension != ".md" )
			{
				contentFileSystem.Copy( contentPath, targetFileSystem, contentPath );
				continue;
			}
			ViewModel viewModel = getViewModel( contentPath, contentFileSystem );
			View view = findView( viewModel, views );
			string htmlText = view.Apply( viewModel );
			targetFileSystem.WriteAllText( contentPath.WithExtension( ".html" ), htmlText );
		}
	}

	static void extractViews( IFileSystem templateFileSystem, IFileSystem.Path templatePath, List<View> allViews, Sys.Action<DiagnosticMessage> diagnosticMessageConsumer )
	{
		string template = templateFileSystem.ReadAllText( templatePath );
		string diagnosticTemplatePathName = templateFileSystem.GetDiagnosticFullPath( templatePath );
		Html.HtmlDocument templateDocument = new Html.HtmlDocument();
		templateDocument.LoadHtml( template );
		foreach( Html.HtmlParseError parseError in templateDocument.ParseErrors )
			diagnosticMessageConsumer.Invoke( new DiagnosticMessage( diagnosticTemplatePathName, parseError.Line, parseError.LinePosition, parseError.SourceText, parseError.Reason ) );
		Html.HtmlNode rootNode = templateDocument.DocumentNode;
		View rootView = new ContentView( null, rootNode, Name.Of( "root" ), new RegEx.Regex( ".*" ) );
		allViews.Add( rootView );
		recurse( rootView, rootNode, allViews, diagnosticMessageConsumer, diagnosticTemplatePathName );
		foreach( View view in allViews )
			view.HtmlNode.Remove();
		return;

		static void recurse( View parentView, Html.HtmlNode parentNode, List<View> allViews, Sys.Action<DiagnosticMessage> diagnosticMessageConsumer, string diagnosticTemplatePathName )
		{
			foreach( Html.HtmlNode childNode in parentNode.ChildNodes )
			{
				View? childView = createChildView( parentView, childNode, diagnosticMessageConsumer, diagnosticTemplatePathName );
				if( childView != null )
					allViews.Add( childView );
				recurse( childView ?? parentView, childNode, allViews, diagnosticMessageConsumer, diagnosticTemplatePathName );
			}

			static View? createChildView( View parentView, Html.HtmlNode htmlNode, Sys.Action<DiagnosticMessage> diagnosticMessageConsumer, string diagnosticTemplatePathName )
			{
				if( htmlNode.Name == contentViewTagName )
				{
					Name name = getName( htmlNode );
					RegEx.Regex appliesTo = getAppliesTo( htmlNode );
					return new ContentView( parentView, htmlNode, name, appliesTo );
				}
				if( htmlNode.Name == collectionViewTagName )
				{
					Name name = getName( htmlNode );
					RegEx.Regex appliesTo = getAppliesTo( htmlNode );
					Name elementViewName = getElementViewName( htmlNode, diagnosticMessageConsumer, diagnosticTemplatePathName );
					return new CollectionView( parentView, htmlNode, name, appliesTo, elementViewName );
				}
				return null;

				static Name getName( Html.HtmlNode htmlNode )
				{
					Html.HtmlAttribute? nameAttribute = htmlNode.Attributes.AttributesWithName( nameAttributeName ).SingleOrDefault();
					return Name.Of( nameAttribute?.Value ?? htmlNode.XPath );
				}

				static RegEx.Regex getAppliesTo( Html.HtmlNode htmlNode )
				{
					Html.HtmlAttribute? appliesToAttribute = htmlNode.Attributes.AttributesWithName( appliesToAttributeName ).SingleOrDefault();
					return new RegEx.Regex( appliesToAttribute?.Value ?? ".*" );
				}

				static Name getElementViewName( Html.HtmlNode htmlNode, Sys.Action<DiagnosticMessage> diagnosticMessageConsumer, string diagnosticTemplatePathName )
				{
					Html.HtmlAttribute? elementViewAttribute = htmlNode.Attributes.AttributesWithName( elementViewAttributeName ).SingleOrDefault();
					if( elementViewAttribute == null )
					{
						diagnosticMessageConsumer.Invoke( new DiagnosticMessage( diagnosticTemplatePathName, htmlNode.Line, htmlNode.LinePosition, "", $"Collection view is missing a '{elementViewAttributeName}' attribute" ) );
						return Name.Of( "" );
					}
					return Name.Of( elementViewAttribute.Value );
				}
			}
		}
	}

	//static ImmutableArray<View> getViews( IFileSystem templateFileSystem, Sys.Action<DiagnosticMessage> diagnosticMessageConsumer )
	//{
	//	IFileSystem.Path templatePath = IFileSystem.Path.Of( "template.html" );
	//	string template = templateFileSystem.ReadAllText( templatePath );
	//	string diagnosticTemplatePathName = templateFileSystem.GetDiagnosticFullPath( templatePath );
	//	Html.HtmlDocument templateDocument = new Html.HtmlDocument();
	//	templateDocument.LoadHtml( template );
	//	foreach( Html.HtmlParseError parseError in templateDocument.ParseErrors )
	//		diagnosticMessageConsumer.Invoke( new DiagnosticMessage( diagnosticTemplatePathName, parseError.Line, parseError.LinePosition, parseError.SourceText, parseError.Reason ) );
	//	Html.HtmlNode rootNode = templateDocument.DocumentNode;
	//	List<View> allViews = new List<View>();
	//	View rootView = new ContentView( null, rootNode, Name.Of( "root" ), new RegEx.Regex( ".*" ) );
	//	allViews.Add( rootView );
	//	recurse( rootView, rootNode, allViews, diagnosticMessageConsumer, diagnosticTemplatePathName );
	//	foreach( View view in allViews )
	//		view.HtmlNode.Remove();
	//	return allViews.ToImmutableArray();

	//	static void recurse( View parentView, Html.HtmlNode parentNode, List<View> allViews, Sys.Action<DiagnosticMessage> diagnosticMessageConsumer, string diagnosticTemplatePathName )
	//	{
	//		foreach( Html.HtmlNode childNode in parentNode.ChildNodes )
	//		{
	//			View? childView = createChildView( parentView, childNode, diagnosticMessageConsumer, diagnosticTemplatePathName );
	//			if( childView != null )
	//				allViews.Add( childView );
	//			recurse( childView ?? parentView, childNode, allViews, diagnosticMessageConsumer, diagnosticTemplatePathName );
	//		}

	//		static View? createChildView( View parentView, Html.HtmlNode htmlNode, Sys.Action<DiagnosticMessage> diagnosticMessageConsumer, string diagnosticTemplatePathName )
	//		{
	//			if( htmlNode.Name == contentViewTagName )
	//			{
	//				Name name = getName( htmlNode );
	//				RegEx.Regex appliesTo = getAppliesTo( htmlNode );
	//				return new ContentView( parentView, htmlNode, name, appliesTo );
	//			}
	//			if( htmlNode.Name == collectionViewTagName )
	//			{
	//				Name name = getName( htmlNode );
	//				RegEx.Regex appliesTo = getAppliesTo( htmlNode );
	//				Name elementViewName = getElementViewName( htmlNode, diagnosticMessageConsumer, diagnosticTemplatePathName );
	//				return new CollectionView( parentView, htmlNode, name, appliesTo, elementViewName );
	//			}
	//			return null;

	//			static Name getName( Html.HtmlNode htmlNode )
	//			{
	//				Html.HtmlAttribute? nameAttribute = htmlNode.Attributes.AttributesWithName( nameAttributeName ).SingleOrDefault();
	//				return Name.Of( nameAttribute?.Value ?? htmlNode.XPath );
	//			}

	//			static RegEx.Regex getAppliesTo( Html.HtmlNode htmlNode )
	//			{
	//				Html.HtmlAttribute? appliesToAttribute = htmlNode.Attributes.AttributesWithName( appliesToAttributeName ).SingleOrDefault();
	//				return new RegEx.Regex( appliesToAttribute?.Value ?? ".*" );
	//			}

	//			static Name getElementViewName( Html.HtmlNode htmlNode, Sys.Action<DiagnosticMessage> diagnosticMessageConsumer, string diagnosticTemplatePathName )
	//			{
	//				Html.HtmlAttribute? elementViewAttribute = htmlNode.Attributes.AttributesWithName( elementViewAttributeName ).SingleOrDefault();
	//				if( elementViewAttribute == null )
	//				{
	//					diagnosticMessageConsumer.Invoke( new DiagnosticMessage( diagnosticTemplatePathName, htmlNode.Line, htmlNode.LinePosition, "", $"Collection view is missing a '{elementViewAttributeName}' attribute" ) );
	//					return Name.Of( "" );
	//				}
	//				return Name.Of( elementViewAttribute.Value );
	//			}
	//		}
	//	}
	//}

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

	static readonly View anyContentView = createAnyContentView();

	static readonly View anyCollectionView = createAnyCollectionView();

	static View createAnyContentView()
	{
		Html.HtmlDocument htmlDocument = new();
		Html.HtmlNode htmlNode = new( Html.HtmlNodeType.Element, htmlDocument, 0 );
		htmlNode.Name = contentViewTagName;
		htmlNode.InnerHtml = "{{content}}";
		return new ContentView( null, htmlNode, Name.Of( "anyContent" ), new RegEx.Regex( ".*" ) );
	}

	static View createAnyCollectionView()
	{
		Html.HtmlDocument htmlDocument = new();
		Html.HtmlNode htmlNode = new( Html.HtmlNodeType.Element, htmlDocument, 0 );
		htmlNode.Name = collectionViewTagName;
		htmlNode.InnerHtml = "{{content}}";
		return new CollectionView( null, htmlNode, Name.Of( "anyCollection" ), new RegEx.Regex( ".*" ), Name.Of( "anyContent" ) );
	}

	static View findView( ViewModel viewModel, IReadOnlyList<View> views )
	{
		IEnumerable<View> visibleViews = views.Where( view => view.Parent == null ).Collect();
		IReadOnlyList<View> applicableViews = visibleViews.Where( a => a.IsApplicableTo( viewModel ) ).Collect();
		if( applicableViews.Count == 0 )
			return viewModel switch
			{
				ContentViewModel => anyContentView,
				CollectionViewModel => anyCollectionView,
				_ => throw new AssertionFailureException()
			};
		if( applicableViews.Count > 1 )
			Log.Warn( $"More than one view is applicable to {viewModel}" );
		return applicableViews[0];
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
