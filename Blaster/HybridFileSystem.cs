namespace Blaster;

using System.Collections.Generic;
using MikeNakis.Kit;
using MikeNakis.Kit.Extensions;
using MikeNakis.Kit.FileSystem;
using static System.MemoryExtensions;
using static MikeNakis.Kit.GlobalStatics;
using Sys = System;

public sealed class HybridFileSystem : FileSystem
{
	abstract class MyItem : Item
	{
		public readonly new HybridFileSystem FileSystem;
		readonly FileName path;
		public override FileName FileName => path;

		protected MyItem( HybridFileSystem fileSystem, FileName fileName )
			: base( fileSystem )
		{
			FileSystem = fileSystem;
			this.path = fileName;
		}

		public override string ToString() => $"{GetType().Name} \"{FileName}\"";
	}

	sealed class FileItem : MyItem
	{
		readonly Sys.DateTime dateTime;
		public readonly byte[] Content;
		FilePath filePath => FileSystem.getFilePath( FileName );

		public FileItem( HybridFileSystem fileSystem, FileName fileName, Sys.DateTime dateTime, byte[] content )
			: base( fileSystem, fileName )
		{
			this.dateTime = dateTime;
			Content = content;
		}

		public Sys.DateTime GetTimeModified() => dateTime;
		public override byte[] ReadAllBytes() => filePath.ReadAllBytes();
		public override void WriteAllBytes( byte[] bytes ) => filePath.WriteAllBytes( bytes );
		public override string GetDiagnosticPathName() => filePath.Path;
	}

	sealed class FakeItem : MyItem
	{
		readonly Sys.DateTime dateTime;
		byte[] content;

		public FakeItem( HybridFileSystem fileSystem, FileName fileName, Sys.DateTime dateTime, byte[] content )
			: base( fileSystem, fileName )
		{
			this.dateTime = dateTime;
			this.content = content;
		}

		public Sys.DateTime GetTimeModified()
		{
			return dateTime;
		}

		public override byte[] ReadAllBytes()
		{
			byte[] result = content.AsSpan().ToArray();
			Assert( !result.ReferenceEquals( content ) );
			return result;
		}

		public override void WriteAllBytes( byte[] bytes )
		{
			content = bytes.AsSpan().ToArray();
			Assert( !content.ReferenceEquals( bytes ) );
		}

		public override string GetDiagnosticPathName() => FileName.Content;
	}

	public DirectoryPath Root { get; }
	readonly Dictionary<FileName, MyItem> items = new();

	public HybridFileSystem( DirectoryPath root )
	{
		Root = root;
		foreach( FilePath filePath in Root.EnumerateFiles( "*", true ) )
		{
			if( filePath.GetFileNameAndExtension().StartsWith2( "." ) )
				continue;
			if( filePath.Directory.GetDirectoryName().StartsWith2( "." ) )
				continue;
			FileName fileName = FileName.Absolute( normalize( '/' + Root.GetRelativePath( filePath ) ) );
			createItem( fileName );
		}

		void createItem( FileName fileName )
		{
			FilePath filePath = getFilePath( fileName );
			FileItem item = new FileItem( this, fileName, filePath.CreationTimeUtc, filePath.ReadAllBytes() );
			items.Add( fileName, item );
		}
	}

	public Item AddFakeItem( FileName fileName, Sys.DateTime dateTime, string content )
	{
		FakeItem item = new FakeItem( this, fileName, dateTime, DotNetHelpers.BomlessUtf8.GetBytes( content ) );
		items.Add( fileName, item );
		return item;
	}

	public override IEnumerable<Item> EnumerateItems() => items.Values;

	static string normalize( string s )
	{
		return s.Replace( '\\', '/' );
	}

	FilePath getFilePath( FileName fileName )
	{
		string pathName = fileName.Content;
		Assert( pathName.StartsWith2( "/" ) );
		return Root.RelativeFile( pathName[1..] );
	}

	public override Item CreateItem( FileName fileName )
	{
		FilePath filePath = getFilePath( fileName );
		using( filePath.CreateBinary() )
		{
			FileItem item = new FileItem( this, fileName, filePath.CreationTimeUtc, Sys.Array.Empty<byte>() );
			items.Add( fileName, item );
			return item;
		}
	}

	public override void Delete( FileName fileName )
	{
		Item item = items[fileName];
		items.Remove( fileName );
		if( item is FileItem )
		{
			FilePath filePath = getFilePath( fileName );
			filePath.Delete();
		}
	}

	public override bool Exists( FileName fileName ) => items.ContainsKey( fileName );
}
