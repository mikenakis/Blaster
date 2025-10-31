namespace Blaster_Test;

using Blaster;
using MikeNakis.Kit;
using MikeNakis.Kit.Extensions;
using MikeNakis.Kit.FileSystem;

sealed class FakeFileSystem : IFileSystem
{
	sealed class Item
	{
		public IFileSystem.Path Path { get; }
		readonly Sys.DateTime dateTime;
		public byte[] Content;

		public Item( IFileSystem.Path path, Sys.DateTime dateTime )
		{
			Path = path;
			this.dateTime = dateTime;
			Content = Sys.Array.Empty<byte>();
		}

		public Sys.DateTime GetTimeModified()
		{
			return dateTime;
		}
	}

	readonly Clock clock;
	readonly DirectoryPath? persistenceDirectoryPath;
	readonly Dictionary<IFileSystem.Path, Item> items = new();

	public FakeFileSystem( Clock clock, DirectoryPath? persistenceDirectoryPath = null )
	{
		this.clock = clock;
		this.persistenceDirectoryPath = persistenceDirectoryPath;
	}

	public void AddItem( IFileSystem.Path path, Sys.DateTime dateTime, string content )
	{
		byte[] bytes = DotNetHelpers.BomlessUtf8.GetBytes( content );
		var item = new Item( path, dateTime );
		items.Add( path, item );
		item.Content = bytes;
		possiblyPersist( path, bytes );
	}

	IEnumerable<IFileSystem.Path> IFileSystem.EnumerateItems() => items.Keys;

	IEnumerable<IFileSystem.Path> IFileSystem.EnumerateItems( IFileSystem.Path path )
	{
		return items.Keys.Where( p => p.Content.StartsWith2( path.Content ) );
	}

	byte[] IFileSystem.ReadAllBytes( IFileSystem.Path path )
	{
		return items[path].Content;
	}

	void IFileSystem.WriteAllBytes( IFileSystem.Path path, byte[] bytes )
	{
		if( !items.TryGetValue( path, out Item? item ) )
		{
			item = new Item( path, clock.GetUniversalTime() );
			items.Add( path, item );
		}
		item.Content = bytes;
		possiblyPersist( path, bytes );
	}

	void possiblyPersist( IFileSystem.Path path, byte[] bytes )
	{
		if( persistenceDirectoryPath != null )
			persistenceDirectoryPath.RelativeFile( path.Content ).WriteAllBytes( bytes );
	}

	public string GetDiagnosticFullPath( IFileSystem.Path path )
	{
		if( persistenceDirectoryPath != null )
			return persistenceDirectoryPath.RelativeFile( path.Content ).Path;
		return path.Content;
	}
}
