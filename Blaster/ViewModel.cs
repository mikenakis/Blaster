namespace Blaster;

using System.Collections.Immutable;
using System.Linq;
using MikeNakis.Kit.Extensions;

abstract class ViewModel
{
	public readonly FileSystem.Item Item;
	public abstract string TypeName { get; }
	public abstract string Title { get; }
	public abstract string Content { get; }

	protected ViewModel( FileSystem.Item item )
	{
		Item = item;
	}

	public override string ToString() => $"{GetType().Name} \"{Item}\"";
}

sealed class ContentViewModel : ViewModel
{
	public override string TypeName => Item.Path.Content;
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
	public override string TypeName => $"{Item.Path.Content}[]";
	public override string Title => TypeName;
	public override string Content => Paths.Select( path => path.Path.Content ).MakeString( ", " );
	public readonly ImmutableArray<FileSystem.Item> Paths;

	public CollectionViewModel( FileSystem.Item item, ImmutableArray<FileSystem.Item> paths )
		: base( item )
	{
		Paths = paths;
	}
}
