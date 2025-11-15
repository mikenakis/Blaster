namespace Blaster;

using static MikeNakis.Kit.GlobalStatics;
using Html = HtmlAgilityPack;
using RegEx = System.Text.RegularExpressions;

abstract class View
{
	public readonly View? Parent;
	public readonly Html.HtmlNode HtmlNode;
	public readonly Name Name;
	readonly RegEx.Regex filter;

	protected View( View? parent, Html.HtmlNode htmlNode, Name name, RegEx.Regex filter )
	{
		Parent = parent;
		HtmlNode = htmlNode;
		Name = name;
		this.filter = filter;
	}

	public bool IsApplicableTo( ViewModel viewModel )
	{
		return filter.IsMatch( viewModel.TypeName );
	}

	public abstract string Apply( ViewModel viewModel );

	public override string ToString() => $"{GetType().Name} \"{Name}\" filter=\"{filter}\"";
}

sealed class ContentView : View
{
	readonly TemplateEngine templateEngine;

	public ContentView( View? parent, Html.HtmlNode htmlNode, Name name, RegEx.Regex filter )
		: base( parent, htmlNode, name, filter )
	{
		Assert( htmlNode.Name is Constants.ContentViewTagName or "#document" );
		templateEngine = TemplateEngine.Create( htmlNode.InnerHtml ); //NOTE: this is wrong. At this point, the inner html contains child views. We must wait until they have been extracted before building the template.
	}

	public override string Apply( ViewModel viewModel )
	{
		return templateEngine.GenerateText( name => name switch
			{
				"title" => viewModel.Title,
				"content" => viewModel.Content,
				_ => "?"
			} );
	}
}

sealed class CollectionView : View
{
	public readonly Name ElementViewName;

	public CollectionView( View? parent, Html.HtmlNode htmlNode, Name name, RegEx.Regex filter, Name elementViewName )
		: base( parent, htmlNode, name, filter )
	{
		Assert( htmlNode.Name == Constants.CollectionViewTagName );
		ElementViewName = elementViewName;
	}

	public override string Apply( ViewModel viewModel )
	{
		CollectionViewModel collectionViewModel = (CollectionViewModel)viewModel;
		return collectionViewModel.Path.Content; //XXX
	}
}
