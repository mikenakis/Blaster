namespace Blaster;

using MarkdigExtensions;
using MikeNakis.Kit;
using MikeNakis.Kit.Extensions;
using MikeNakis.Kit.FileSystem;
using Structured.Json;
using static Markdig.MarkdownExtensions;
using static MikeNakis.Kit.GlobalStatics;
using Markdig = Markdig;

public sealed class BlasterMain
{
	public static void Main( string[] arguments )
	{
		Assert( arguments.Length == 0 );
		Sys.Console.WriteLine( $"INFO: Current directory: '{DotNetHelpers.GetWorkingDirectoryPath()}'" );
		Sys.Console.WriteLine( $"INFO: Arguments: [{string.Join( ", ", arguments )}]" );
		Stock sourceStock = new Stock( DirectoryPath.FromAbsolutePath( @"C:\Users\MBV\Personal\Documents\digital-garden\obsidian\blog.michael.gr\content" ) );
		sourceStock.AddFakeItem( Stock.Id.FromString( "index.md" ), DotNetClock.Instance.GetUniversalTime(), "[](page)[](post)" );
		Sys.Console.WriteLine( $"INFO: Source: {sourceStock.Root}" );
		Stock targetStock = new Stock( DotNetHelpers.GetTempDirectoryPath().Directory( "blaster-publish" ) );
		Sys.Console.WriteLine( $"INFO: Target: {targetStock.Root}" );

		Diary diary = new();
		deserializeDiary( sourceStock, diary );
		foreach( Stock.Id sourceStockId in sourceStock.EnumerateItems() )
		{
			if( sourceStockId.Extension != ".md" )
			{
				Stock.Id targetStockId = sourceStockId;
				sourceStock.Copy( sourceStockId, targetStock, targetStockId );
			}
			else
			{
				Stock.Id targetStockId = sourceStockId.WithExtension( ".html" );
				string markdownText = sourceStock.ReadAllText( sourceStockId );
				markdownText = markdownText.Replace( "<br>", "<br />", Sys.StringComparison.OrdinalIgnoreCase );
				string htmlText = convert( markdownText );
				if( htmlText != "" )
				{
					SysXmlLinq.XElement xelement;
					try
					{
						xelement = SysXmlLinq.XElement.Parse( "<section>" + htmlText + "</section>" );
					}
					catch( Sys.Exception exception )
					{
						Log.Error( $"{sourceStockId}: {exception.Message}" );
						continue;
					}
					foreach( SysXmlLinq.XElement linkElement in xelement.Descendants( "a" ) )
					{
						SysXmlLinq.XAttribute? hrefAttribute = linkElement.Attribute( "href" );
						if( hrefAttribute == null )
							continue;
						string href = hrefAttribute.Value;
						if( href == "" || href.StartsWith2( "#" ) )
							continue;
						if( href.StartsWith2( "http://" ) || href.StartsWith2( "https://" ) )
							continue;
						if( href.EndsWith2( ".md" ) )
						{
							href = Stock.Id.FromString( hrefAttribute.Value ).WithExtension( ".html" ).Content;
							hrefAttribute.Value = href;
						}
					}
					htmlText = xelement.ToString();
				}
				targetStock.WriteAllText( targetStockId, htmlText );
			}
		}
		serializeDiary( sourceStock, diary );

		Sys.Console.Write( "Press [Enter] to terminate: " );
		Sys.Console.ReadLine();
	}

	static string convert( string markdownText )
	{
		if( False )
			return Markdig.Markdown.ToHtml( markdownText );

		Markdig.MarkdownPipeline pipeline = new Markdig.MarkdownPipelineBuilder()
			//.UseAdvancedExtensions()
			.UseYamlFrontMatter()
			.UsePipeTables()
			.UseFootnotes()
			.UseEmphasisExtras()
			.UseListExtras()
			.UseImageAsFigure()
			.UseUrlRewriter( link => link.Url.OrThrow() )
			.Build();
		Markdig.Syntax.MarkdownDocument document = Markdig.Parsers.MarkdownParser.Parse( markdownText, pipeline );
		return Markdig.Markdown.ToHtml( document, pipeline );
	}

	const string diaryFilename = ".blaster.diary.json";

	static void serializeDiary( Stock markdownRepository, Diary diary )
	{
		string content = StructuredJsonHelper.Serialize( formatted: true, useUnquotedIdentifiers: true, diary.Serialize );
		markdownRepository.WriteSideFile( diaryFilename, content );
	}

	static void deserializeDiary( Stock markdownRepository, Diary diary )
	{
		string? content = markdownRepository.TryReadSideFile( diaryFilename );
		if( content == null )
			return;
		StructuredJsonHelper.Deserialize( content, diary.Deserialize );
	}
}
