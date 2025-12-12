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
	abstract class MyFileItem : FileItem
	{
		public readonly new HybridFileSystem FileSystem;
		readonly FileName fileName;
		public override FileName FileName => fileName;

		protected MyFileItem( HybridFileSystem fileSystem, FileName fileName )
			: base( fileSystem )
		{
			FileSystem = fileSystem;
			this.fileName = fileName;
		}
	}

	sealed class MyActualFileItem : MyFileItem
	{
		readonly Sys.DateTime dateTime;
		readonly byte[] content;
		FilePath filePath => FileSystem.getFilePath( FileName );

		public MyActualFileItem( HybridFileSystem fileSystem, FileName fileName, Sys.DateTime dateTime, byte[] content )
			: base( fileSystem, fileName )
		{
			this.dateTime = dateTime;
			this.content = content;
		}

		public Sys.DateTime GetTimeModified() => dateTime;
		public override byte[] ReadAllBytes() => filePath.ReadAllBytes();
		public override void WriteAllBytes( byte[] bytes ) => filePath.WriteAllBytes( bytes );
		public override string GetDiagnosticPathName() => filePath.Path;
		public override long FileLength => content.Length;
	}

	sealed class MyFakeFileItem : MyFileItem
	{
		readonly Sys.DateTime dateTime;
		byte[] content;

		public MyFakeFileItem( HybridFileSystem fileSystem, FileName fileName, Sys.DateTime dateTime, byte[] content )
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
		public override long FileLength => content.Length;
	}

	public DirectoryPath Root { get; }
	readonly Dictionary<FileName, MyFileItem> items = new();
	readonly Clock clock;

	public HybridFileSystem( DirectoryPath root, Clock clock )
	{
		Root = root;
		this.clock = clock;
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
			MyActualFileItem item = new MyActualFileItem( this, fileName, filePath.CreationTimeUtc, filePath.ReadAllBytes() );
			items.Add( fileName, item );
		}
	}

	public FileItem AddFakeItem( FileName fileName, string content )
	{
		MyFakeFileItem item = new MyFakeFileItem( this, fileName, clock.GetUniversalTime(), DotNetHelpers.BomlessUtf8.GetBytes( content ) );
		items.Add( fileName, item );
		return item;
	}

	public override IEnumerable<FileItem> EnumerateItems() => items.Values;

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

	public override FileItem CreateItem( FileName fileName )
	{
		FilePath filePath = getFilePath( fileName );
		using( filePath.CreateBinary() )
		{
			MyActualFileItem item = new MyActualFileItem( this, fileName, filePath.CreationTimeUtc, Sys.Array.Empty<byte>() );
			items.Add( fileName, item );
			return item;
		}
	}

	public override void Delete( FileName fileName )
	{
		FileItem item = items[fileName];
		items.Remove( fileName );
		if( item is MyActualFileItem )
		{
			FilePath filePath = getFilePath( fileName );
			filePath.Delete();
		}
	}

	public override bool Exists( FileName fileName ) => items.ContainsKey( fileName );
}
