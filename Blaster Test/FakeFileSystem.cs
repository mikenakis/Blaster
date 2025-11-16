namespace Blaster_Test;

using System.Collections.Generic;
using Blaster;
using MikeNakis.Kit;
using static MikeNakis.Kit.GlobalStatics;
using Sys = System;

sealed class FakeFileSystem : FileSystem
{
	sealed class FakeItem : Item
	{
		readonly FakeFileSystem fileSystem;
		readonly FileName fileName;
		readonly Sys.DateTime dateTime;
		byte[] content = Sys.Array.Empty<byte>();
		public override FileName FileName => fileName;

		public FakeItem( FakeFileSystem fileSystem, FileName fileName, Sys.DateTime dateTime )
			: base( fileSystem )
		{
			this.fileSystem = fileSystem;
			this.fileName = fileName;
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
			fileSystem.possiblyPersist( fileName, bytes );
		}

		public override string GetDiagnosticPathName()
		{
			return fileSystem.getFilePathName( FileName );
		}

		public override string ToString() => $"{GetType().Name} {fileName}";
	}

	readonly Clock clock;
	readonly MikeNakis.Kit.FileSystem.DirectoryPath? persistenceDirectoryPath;
	readonly Dictionary<FileName, FakeItem> items = new();

	public FakeFileSystem( Clock clock, MikeNakis.Kit.FileSystem.DirectoryPath? persistenceDirectoryPath = null )
	{
		this.clock = clock;
		this.persistenceDirectoryPath = persistenceDirectoryPath;
	}

	public Item AddItem( FileName fileName, Sys.DateTime dateTime, string content )
	{
		FakeItem item = new FakeItem( this, fileName, dateTime );
		items.Add( fileName, item );
		item.WriteAllBytes( DotNetHelpers.BomlessUtf8.GetBytes( content ) );
		return item;
	}

	public override IEnumerable<Item> EnumerateItems() => items.Values;

	void possiblyPersist( FileName fileName, byte[] bytes )
	{
		if( persistenceDirectoryPath != null )
		{
			string pathName = fileName.Content;
			Assert( pathName[0] == '/' );
			persistenceDirectoryPath.RelativeFile( pathName[1..] ).WriteAllBytes( bytes );
		}
	}

	string getFilePathName( FileName fileName )
	{
		if( persistenceDirectoryPath == null )
			return fileName.Content;
		return persistenceDirectoryPath.RelativeFile( fileName.Content ).Path;
	}

	public override Item CreateItem( FileName fileName )
	{
		FakeItem item = new FakeItem( this, fileName, clock.GetUniversalTime() );
		items.Add( fileName, item );
		return item;
	}

	public override void Delete( FileName fileName ) => items.Remove( fileName );
	public override bool Exists( FileName fileName ) => items.ContainsKey( fileName );
}
