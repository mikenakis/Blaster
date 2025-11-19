namespace Blaster;

using System.Collections.Generic;
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
	public readonly FileItem Item;
	public abstract string TypeName { get; }
	public abstract override string Title { get; }
	public abstract override string Content { get; }

	protected ViewModel( FileItem item )
	{
		Item = item;
	}

	public override string ToString() => $"{GetType().Name} \"{Item}\"";
}

sealed class ContentViewModel : ViewModel
{
	public override string TypeName => Item.FileName.Content;
	public override string Title => Properties.TryGet2( "Title" ) ?? Item.FileName.Content;
	public override string Content => HtmlText;
	public readonly IReadOnlyDictionary<string, string> Properties;
	public readonly string HtmlText;

	public ContentViewModel( FileItem item, IReadOnlyDictionary<string, string> properties, string htmlText )
		: base( item )
	{
		Properties = properties;
		HtmlText = htmlText;
	}
}

sealed class CollectionViewModel : ViewModel
{
	public override string TypeName => $"{Item.FileName.Content}[]";
	public override string Title => TypeName;
	public override string Content => Paths.Select( path => path.FileName.Content ).MakeString( ", " );
	public readonly ImmutableArray<FileItem> Paths;

	public CollectionViewModel( FileItem item, ImmutableArray<FileItem> paths )
		: base( item )
	{
		Paths = paths;
	}
}
