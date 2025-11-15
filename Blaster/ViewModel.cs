namespace Blaster;

using System.Collections.Immutable;
using System.Linq;
using MikeNakis.Kit.Extensions;

abstract class ViewModel
{
	public readonly IFileSystem.Path Path;
	public abstract string TypeName { get; }
	public abstract string Title { get; }
	public abstract string Content { get; }

	protected ViewModel( IFileSystem.Path path )
	{
		Path = path;
	}

	public override string ToString() => $"{GetType().Name} \"{Path}\"";
}

sealed class ContentViewModel : ViewModel
{
	public override string TypeName => Path.Content;
	public override string Title => TypeName; //TODO: get from frontmatter
	public override string Content => HtmlText;
	public readonly string HtmlText;

	public ContentViewModel( IFileSystem.Path path, string htmlText )
		: base( path )
	{
		HtmlText = htmlText;
	}
}

sealed class CollectionViewModel : ViewModel
{
	public override string TypeName => $"{Path.Content}[]";
	public override string Title => TypeName;
	public override string Content => Paths.Select( path => path.Content ).MakeString( ", " );
	public readonly ImmutableArray<IFileSystem.Path> Paths;

	public CollectionViewModel( IFileSystem.Path path, ImmutableArray<IFileSystem.Path> paths )
		: base( path )
	{
		Paths = paths;
	}
}
