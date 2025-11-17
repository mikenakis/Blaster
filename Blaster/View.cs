namespace Blaster;

using System.Collections.Immutable;
using RegEx = System.Text.RegularExpressions;

abstract class View
{
	public readonly Name Name;
	public readonly ImmutableArray<View> Children;
	readonly RegEx.Regex appliesTo;

	protected View( Name name, RegEx.Regex appliesTo, ImmutableArray<View> children )
	{
		Name = name;
		Children = children;
		this.appliesTo = appliesTo;
	}

	public bool IsApplicableTo( ViewModel viewModel )
	{
		return appliesTo.IsMatch( viewModel.TypeName );
	}

	public abstract string Apply( ContentBase viewModel );

	public override string ToString() => $"{GetType().Name} \"{Name}\"";
}

sealed class ContentView : View
{
	readonly TemplateEngine templateEngine;

	public ContentView( Name name, RegEx.Regex appliesTo, ImmutableArray<View> children, string html )
		: base( name, appliesTo, children )
	{
		templateEngine = TemplateEngine.Create( html );
	}

	public override string Apply( ContentBase viewModel )
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
	readonly TemplateEngine templateEngine;
	public readonly Name ElementViewName;

	public CollectionView( Name name, RegEx.Regex appliesTo, ImmutableArray<View> children, string html, Name elementViewName )
		: base( name, appliesTo, children )
	{
		ElementViewName = elementViewName;
		templateEngine = TemplateEngine.Create( html );
	}

	public override string Apply( ContentBase viewModel )
	{
		CollectionViewModel collectionViewModel = (CollectionViewModel)viewModel;
		return collectionViewModel.Item.FileName.Content; //XXX
	}
}
