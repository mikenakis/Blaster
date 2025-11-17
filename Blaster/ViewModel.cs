namespace Blaster;

using System.Collections.Immutable;
using System.Linq;
using MikeNakis.Kit.Extensions;

abstract class ContentBase
{
	public abstract string Title { get; }
	public abstract string Content { get; }
}

sealed class HtmlContent : ContentBase
{
	readonly ContentBase viewModel;
	readonly string htmlText;
	public override string Title => viewModel.Title;
	public override string Content => htmlText;

	public HtmlContent( ContentBase viewModel, string htmlText )
	{
		this.viewModel = viewModel;
		this.htmlText = htmlText;
	}
}

abstract class ViewModel : ContentBase
{
	public readonly FileSystem.Item Item;
	public abstract string TypeName { get; }
	public abstract override string Title { get; }
	public abstract override string Content { get; }

	protected ViewModel( FileSystem.Item item )
	{
		Item = item;
	}

	public override string ToString() => $"{GetType().Name} \"{Item}\"";
}

sealed class ContentViewModel : ViewModel
{
	public override string TypeName => Item.FileName.Content;
	public override string Title => TypeName; //TODO: get from frontmatter
	public override string Content => HtmlText;
	public readonly string HtmlText;

	public ContentViewModel( FileSystem.Item item, string htmlText )
		: base( item )
	{
		HtmlText = htmlText;
	}
}

sealed class CollectionViewModel : ViewModel
{
	public override string TypeName => $"{Item.FileName.Content}[]";
	public override string Title => TypeName;
	public override string Content => Paths.Select( path => path.FileName.Content ).MakeString( ", " );
	public readonly ImmutableArray<FileSystem.Item> Paths;

	public CollectionViewModel( FileSystem.Item item, ImmutableArray<FileSystem.Item> paths )
		: base( item )
	{
		Paths = paths;
	}
}
