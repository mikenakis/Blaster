namespace Blaster;

using System.Collections.Generic;
using Framework.Codecs;
using MikeNakis.Kit;
using MikeNakis.Kit.Extensions;
using static MikeNakis.Kit.GlobalStatics;
using Sys = System;
using SysIo = System.IO;

public abstract class FileSystem
{
	public readonly struct Path : Sys.IComparable<Path>, Sys.IEquatable<Path>
	{
		public static bool operator ==( Path left, Path right ) => left.Equals( right );
		public static bool operator !=( Path left, Path right ) => !left.Equals( right );

		public static Path Of( string content )
		{
			return new Path( content );
		}

		public static readonly Codec<Path> Codec = new StringRepresentationCodec<Path>( s => new Path( s ), k => k.Content );

		public string Content { get; init; }

		Path( string content )
		{
			Assert( !content.Contains2( '\\' ) ); //forward slashes only
			Content = content;
		}

		public int CompareTo( Path other ) => StringCompare( Content, other.Content );
		[Sys.Obsolete] public override bool Equals( object? other ) => other is Path kin && Equals( kin );
		public override int GetHashCode() => Content.GetHashCode( Sys.StringComparison.Ordinal );
		public bool Equals( Path other ) => CompareTo( other ) == 0;
		public override string ToString() => Content;

		public string Extension => getExtension();

		public readonly string GetDirectory()
		{
#pragma warning disable RS0030 // Do not use banned APIs
			return SysIo.Path.GetDirectoryName( Content ).OrThrow();
#pragma warning restore RS0030 // Do not use banned APIs
		}

		string getExtension()
		{
#pragma warning disable RS0030 // Do not use banned APIs
			return SysIo.Path.GetExtension( Content );
#pragma warning restore RS0030 // Do not use banned APIs
		}

		internal Path WithExtension( string extension )
		{
#pragma warning disable RS0030 // Do not use banned APIs
			string newContent = SysIo.Path.ChangeExtension( Content, extension );
#pragma warning restore RS0030 // Do not use banned APIs
			return new Path( newContent );
		}
	}

	public abstract class Item
	{
		public abstract Path Path { get; }

		public abstract byte[] ReadAllBytes();
		public abstract void WriteAllBytes( byte[] text );
		public string ReadAllText() => DotNetHelpers.BomlessUtf8.GetString( ReadAllBytes() );
		public void WriteAllText( string text ) => WriteAllBytes( DotNetHelpers.BomlessUtf8.GetBytes( text ) );
		public abstract string GetDiagnosticPathName();

		public void CopyFrom( Item sourceItem )
		{
			WriteAllBytes( sourceItem.ReadAllBytes() );
		}
	}

	public abstract Item CreateItem( Path path );

	public Item CopyFrom( Item sourceItem )
	{
		Item targetItem = CreateItem( sourceItem.Path );
		targetItem.CopyFrom( sourceItem );
		return targetItem;
	}

	public abstract IEnumerable<Item> EnumerateItems();
	//public abstract IEnumerable<Item> EnumerateSiblingItems( Item item );

	public IEnumerable<Item> EnumerateSiblingItems( Item rootItem )
	{
		string rootPath = rootItem.Path.GetDirectory();
		foreach( Item item in EnumerateItems() )
		{
			string itemPath = item.Path.GetDirectory();
			if( itemPath == rootPath )
				yield return item;
		}
	}
}
