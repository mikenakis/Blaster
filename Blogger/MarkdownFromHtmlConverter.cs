namespace Blogger;

class MarkdownFromHtmlConverter
{
	readonly ReverseMarkdown.Converter converter;

	public MarkdownFromHtmlConverter()
	{
		var reverseMarkdownConfig = new ReverseMarkdown.Config();
		reverseMarkdownConfig.CleanupUnnecessarySpaces = true; //default = true
		reverseMarkdownConfig.DefaultCodeBlockLanguage = "CSharp"; //default = null
		reverseMarkdownConfig.GithubFlavored = false; //default = false
		reverseMarkdownConfig.PassThroughTags = ["Style"]; //default = {}
		reverseMarkdownConfig.RemoveComments = false; //default = false
		reverseMarkdownConfig.SlackFlavored = false; //default = false
		reverseMarkdownConfig.SmartHrefHandling = false; //default = false
		reverseMarkdownConfig.SuppressDivNewlines = false; //default = false
		reverseMarkdownConfig.TableHeaderColumnSpanHandling = true; //default = true
		reverseMarkdownConfig.TableWithoutHeaderRowHandling = ReverseMarkdown.Config.TableWithoutHeaderRowHandlingOption.Default; // default = Default
		reverseMarkdownConfig.UnknownTags = ReverseMarkdown.Config.UnknownTagsOption.PassThrough;
		reverseMarkdownConfig.WhitelistUriSchemes = [];  //default = {}
		converter = new ReverseMarkdown.Converter( reverseMarkdownConfig );
	}

	public string Convert( string html )
	{
		string markdown = converter.Convert( html );
		//PEARL: overcome ReverseMarkdown bug, see https://github.com/mysticmind/reversemarkdown-net/discussions/406
		markdown = markdown.Replace( "\\[\\]", "[]" );
		return markdown;
	}
}
