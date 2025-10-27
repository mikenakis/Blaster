namespace Blaster.MarkdigExtensions;

using Markdig = Markdig;
using MarkdigImageAsFigure = global::MarkdigExtensions.ImageAsFigure;
using MarkdigInlines = Markdig.Syntax.Inlines;
using MarkdigUrlRewriter = global::MarkdigExtensions.UrlRewriter;

public static class Extensions
{
	//
	// Summary:
	//     Render any image inside a figure tag with a figcaption tag containing the title
	//     of the image, if set
	//
	// Parameters:
	//   pipeline:
	//     The Markdig MarkdownPipelineBuilder to add the extension to
	//
	//   onlyWithTitle:
	//     Only wrap images with a title in a figure tag, default is false
	public static Markdig.MarkdownPipelineBuilder UseImageAsFigure( this Markdig.MarkdownPipelineBuilder pipeline, bool onlyWithTitle = false )
	{
		pipeline.Extensions.Add( new MarkdigImageAsFigure.ImageAsFigureExtension( onlyWithTitle ) );
		return pipeline;
	}

	//
	// Summary:
	//     Use the urlRewriter function to rewrite URLs in any link in your Markdown
	//
	// Parameters:
	//   pipeline:
	//     The Markdig MarkdownPipelineBuilder to add the extension to
	//
	//   urlRewriter:
	//     A function that gets a Markdig.Syntax.Inlines.LinkInline object and returns the
	//     new URL for that link
	public static Markdig.MarkdownPipelineBuilder UseUrlRewriter( this Markdig.MarkdownPipelineBuilder pipeline, Sys.Func<MarkdigInlines.LinkInline, string> urlRewriter )
	{
		pipeline.Extensions.Add( new MarkdigUrlRewriter.UrlRewriterExtension( urlRewriter ) );
		return pipeline;
	}
}
