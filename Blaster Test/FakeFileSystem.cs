namespace Blaster_Test;

using System.Collections.Generic;
using Blaster;
using MikeNakis.Kit;
using MikeNakis.Kit.FileSystem;
using Sys = System;

sealed class FakeFileSystem : FileSystem
{
	sealed class FakeItem : Item
	{
		readonly FakeFileSystem fileSystem;
		readonly Path path;
		readonly Sys.DateTime dateTime;
		byte[] content = Sys.Array.Empty<byte>();
		public override Path Path => path;

		public FakeItem( FakeFileSystem fileSystem, Path path, Sys.DateTime dateTime )
		{
			this.fileSystem = fileSystem;
			this.path = path;
			this.dateTime = dateTime;
		}

		public Sys.DateTime GetTimeModified()
		{
			return dateTime;
		}

		public override byte[] ReadAllBytes()
		{
			byte[] result = new byte[content.Length];
			Sys.Array.Copy( content, result, content.Length );
			return result;
		}

		public override void WriteAllBytes( byte[] bytes )
		{
			content = new byte[bytes.Length];
			Sys.Array.Copy( bytes, content, content.Length );
			fileSystem.possiblyPersist( path, bytes );
		}

		public override string GetDiagnosticPathName()
		{
			return fileSystem.getFilePathName( Path );
		}
	}

	readonly Clock clock;
	readonly DirectoryPath? persistenceDirectoryPath;
	readonly Dictionary<Path, FakeItem> items = new();

	public FakeFileSystem( Clock clock, DirectoryPath? persistenceDirectoryPath = null )
	{
		this.clock = clock;
		this.persistenceDirectoryPath = persistenceDirectoryPath;
	}

	public Item AddItem( Path path, Sys.DateTime dateTime, string content )
	{
		FakeItem item = new FakeItem( this, path, dateTime );
		items.Add( path, item );
		item.WriteAllBytes( DotNetHelpers.BomlessUtf8.GetBytes( content ) );
		return item;
	}

	public override IEnumerable<Item> EnumerateItems() => items.Values;

	void possiblyPersist( Path path, byte[] bytes )
	{
		if( persistenceDirectoryPath != null )
			persistenceDirectoryPath.RelativeFile( path.Content ).WriteAllBytes( bytes );
	}

	string getFilePathName( Path path )
	{
		if( persistenceDirectoryPath == null )
			return path.Content;
		return persistenceDirectoryPath.RelativeFile( path.Content ).Path;
	}

	public override Item CreateItem( Path path )
	{
		FakeItem item = new FakeItem( this, path, clock.GetUniversalTime() );
		items.Add( path, item );
		return item;
	}
}
