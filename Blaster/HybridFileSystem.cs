namespace Blaster;

using System.Collections.Generic;
using MikeNakis.Kit;
using MikeNakis.Kit.Extensions;
using MikeNakis.Kit.FileSystem;
using static MikeNakis.Kit.GlobalStatics;
using Sys = System;

public sealed class HybridFileSystem : FileSystem
{
	abstract class MyItem : Item
	{
		public readonly new HybridFileSystem FileSystem;
		readonly FileName path;
		public override FileName FileName => path;

		protected MyItem( HybridFileSystem fileSystem, FileName path )
			: base( fileSystem )
		{
			FileSystem = fileSystem;
			this.path = path;
		}
	}

	sealed class FileItem : MyItem
	{
		readonly Sys.DateTime dateTime;
		public readonly byte[] Content;
		FilePath filePath => FileSystem.getFilePath( FileName );

		public FileItem( HybridFileSystem fileSystem, FileName path, Sys.DateTime dateTime, byte[] content )
			: base( fileSystem, path )
		{
			this.dateTime = dateTime;
			Content = content;
		}

		public Sys.DateTime GetTimeModified()
		{
			return dateTime;
		}

		public override byte[] ReadAllBytes()
		{
			return filePath.ReadAllBytes();
		}

		public override void WriteAllBytes( byte[] bytes )
		{
			filePath.WriteAllBytes( bytes );
		}

		public override string GetDiagnosticPathName()
		{
			return filePath.Path;
		}
	}

	sealed class FakeItem : MyItem
	{
		readonly Sys.DateTime dateTime;
		byte[] content;

		public FakeItem( HybridFileSystem fileSystem, FileName path, Sys.DateTime dateTime, byte[] content )
			: base( fileSystem, path )
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
			byte[] result = new byte[content.Length];
			Sys.Array.Copy( content, result, content.Length );
			return result;
		}

		public override void WriteAllBytes( byte[] bytes )
		{
			content = new byte[bytes.Length];
			Sys.Array.Copy( bytes, content, content.Length );
		}

		public override string GetDiagnosticPathName()
		{
			return FileName.Content;
		}
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
			var path = FileName.Absolute( normalize( '/' + Root.GetRelativePath( filePath ) ) );
			CreateItem( path );
		}
	}

	public Item AddFakeItem( FileName path, Sys.DateTime dateTime, string content )
	{
		FakeItem item = new FakeItem( this, path, dateTime, DotNetHelpers.BomlessUtf8.GetBytes( content ) );
		items.Add( path, item );
		return item;
	}

	public override IEnumerable<Item> EnumerateItems() => items.Values;

	static string normalize( string s )
	{
		return s.Replace( '\\', '/' );
	}

	FilePath getFilePath( FileName path )
	{
		string pathName = path.Content;
		Assert( pathName.StartsWith2( "/" ) );
		return Root.RelativeFile( pathName[1..] );
	}

	public override Item CreateItem( FileName path )
	{
		FilePath filePath = getFilePath( path );
		FileItem item = new FileItem( this, path, filePath.CreationTimeUtc, filePath.ReadAllBytes() );
		items.Add( path, item );
		return item;
	}

	public override bool Exists( FileName fileName )
	{
		return items.ContainsKey( fileName );
	}
}
