namespace Blaster;

using System.Collections.Generic;
using MikeNakis.Kit;
using MikeNakis.Kit.Extensions;
using MikeNakis.Kit.FileSystem;
using static MikeNakis.Kit.GlobalStatics;
using Sys = System;

public sealed class HybridFileSystem : IFileSystem
{
	sealed class Item
	{
		public IFileSystem.Path Path { get; }
		readonly Sys.DateTime dateTime;
		public readonly byte[] Content;

		public Item( IFileSystem.Path path, Sys.DateTime dateTime, byte[] content )
		{
			Path = path;
			this.dateTime = dateTime;
			Content = content;
		}

		public Sys.DateTime GetTimeModified()
		{
			return dateTime;
		}
	}

	public DirectoryPath Root { get; }
	readonly Dictionary<IFileSystem.Path, Item> items = new();

	public HybridFileSystem( DirectoryPath root )
	{
		Root = root;
	}

	public void AddFakeItem( IFileSystem.Path path, Sys.DateTime dateTime, string content )
	{
		Item item = new Item( path, dateTime, DotNetHelpers.BomlessUtf8.GetBytes( content ) );
		items.Add( path, item );
	}

	IEnumerable<IFileSystem.Path> IFileSystem.EnumerateItems()
	{
		foreach( FilePath filePath in Root.EnumerateFiles( "*", true ) )
		{
			if( filePath.GetFileNameAndExtension().StartsWith2( "." ) )
				continue;
			if( filePath.GetDirectoryPatrh().GetDirectoryName().StartsWith2( "." ) )
				continue;
			yield return IFileSystem.Path.Of( normalize( Root.GetRelativePath( filePath ) ) );
		}
		foreach( IFileSystem.Path path in items.Keys )
			yield return path;
	}

	static string normalize( string s )
	{
		return s.Replace( '\\', '/' );
	}

	IEnumerable<IFileSystem.Path> IFileSystem.EnumerateItems( IFileSystem.Path path )
	{
		DirectoryPath localRoot = Root.RelativeFile( path.Content ).Directory;
		foreach( FilePath filePath in localRoot.EnumerateFiles( "*", true ) )
		{
			if( filePath.GetFileNameAndExtension().StartsWith2( "." ) )
				continue;
			if( filePath.GetDirectoryPatrh().GetDirectoryName().StartsWith2( "." ) )
				continue;
			yield return IFileSystem.Path.Of( normalize( localRoot.GetRelativePath( filePath ) ) );
		}
	}

	byte[] IFileSystem.ReadAllBytes( IFileSystem.Path path )
	{
		if( items.TryGetValue( path, out Item? value ) )
			return value.Content;
		FilePath filePath = filePathFromPathName( path );
		return filePath.ReadAllBytes();
	}

	void IFileSystem.WriteAllBytes( IFileSystem.Path path, byte[] bytes )
	{
		Assert( !items.ContainsKey( path ) );
		FilePath filePath = filePathFromPathName( path );
		filePath.WriteAllBytes( bytes );
	}

	FilePath filePathFromPathName( IFileSystem.Path path )
	{
		return Root.RelativeFile( path.Content );
	}

	public string GetDiagnosticFullPath( IFileSystem.Path path )
	{
		return filePathFromPathName( path ).Path;
	}
}
