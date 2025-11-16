namespace Blaster;

using System.Collections.Generic;
using Framework.Codecs;
using MikeNakis.Kit;
using MikeNakis.Kit.Extensions;
using static MikeNakis.Kit.GlobalStatics;
using Sys = System;

public abstract class FileSystem
{
	public readonly struct DirectoryName : Sys.IComparable<DirectoryName>, Sys.IEquatable<DirectoryName>
	{
		public static bool operator ==( DirectoryName left, DirectoryName right ) => left.Equals( right );
		public static bool operator !=( DirectoryName left, DirectoryName right ) => !left.Equals( right );

		public static DirectoryName Of( string content )
		{
			return new DirectoryName( content );
		}

		public static readonly Codec<DirectoryName> Codec = new StringRepresentationCodec<DirectoryName>( s => new DirectoryName( s ), k => k.Content );

		public string Content { get; }

		DirectoryName( string content )
		{
			Assert( content.Length > 0 );
			Assert( content[0] == '/' ); //absolute paths only
			Assert( content[^1] == '/' ); //directories only
			Assert( !content.Contains2( '\\' ) ); //forward slashes only
			Assert( !content.Contains2( "//" ) ); //single slashes only
			Assert( !content.Contains2( "/./" ) ); //normalized paths only
			Assert( !content.Contains2( "/../" ) ); //normalized paths only
			Content = content;
		}

		public int CompareTo( DirectoryName other ) => StringCompare( Content, other.Content );
		[Sys.Obsolete] public override bool Equals( object? other ) => other is DirectoryName kin && Equals( kin );
		public override int GetHashCode() => Content.GetHashCode( Sys.StringComparison.Ordinal );
		public bool Equals( DirectoryName other ) => CompareTo( other ) == 0;
		public override string ToString() => Content;

		public DirectoryName? Parent => getParent();

		DirectoryName? getParent()
		{
			int i = Content.LastIndexOf( '/', 0, Content.Length - 1 );
			if( i == -1 )
				return null;
			return new DirectoryName( Content[..(i + 1)] );
		}
	}

	public readonly struct FileName : Sys.IComparable<FileName>, Sys.IEquatable<FileName>
	{
		public static bool operator ==( FileName left, FileName right ) => left.Equals( right );
		public static bool operator !=( FileName left, FileName right ) => !left.Equals( right );

		public static FileName Absolute( string content )
		{
			return new FileName( content );
		}

		public static FileName AbsoluteOrRelative( string content, DirectoryName directoryName )
		{
			if( content.StartsWith2( "/" ) )
				return new FileName( content );
			return new FileName( directoryName.Content + content );
		}

		public static readonly Codec<FileName> Codec = new StringRepresentationCodec<FileName>( s => new FileName( s ), k => k.Content );

		public string Content { get; }

		FileName( string content )
		{
			Assert( content.Length > 0 );
			Assert( content[0] == '/' ); //ansolute paths only
			Assert( content[^1] != '/' ); //files only
			Assert( !content.Contains2( '\\' ) ); //forward slashes only
			Assert( !content.Contains2( "//" ) ); //single slashes only
			Assert( !content.Contains2( "/./" ) ); //normalized paths only
			Assert( !content.Contains2( "/../" ) ); //normalized paths only
			Content = content;
		}

		public int CompareTo( FileName other ) => StringCompare( Content, other.Content );
		[Sys.Obsolete] public override bool Equals( object? other ) => other is FileName kin && Equals( kin );
		public override int GetHashCode() => Content.GetHashCode( Sys.StringComparison.Ordinal );
		public bool Equals( FileName other ) => CompareTo( other ) == 0;
		public override string ToString() => Content;
		public string Extension => getExtension();
		public DirectoryName DirectoryName => getDirectoryName();
		public FileName WithoutExtension => withoutExtension();

		DirectoryName getDirectoryName()
		{
			int i = Content.LastIndexOf( '/' );
			Assert( i != -1 );
			return DirectoryName.Of( Content[..(i + 1)] );
		}

		string getExtension()
		{
			int i = Content.LastIndexOf( '.' );
			if( i == -1 )
				return "";
			return Content[i..];
		}

		FileName withoutExtension()
		{
			int i = Content.LastIndexOf( '.' );
			return new FileName( i == -1 ? Content : Content[..i] );
		}

		public FileName WithExtension( string extension ) => Absolute( Content + extension );
		internal bool HasExtension( string extension ) => Content.EndsWith2( extension );
	}

	public abstract class Item
	{
		public FileSystem FileSystem { get; }
		public abstract FileName FileName { get; }

		protected Item( FileSystem fileSystem )
		{
			FileSystem = fileSystem;
		}

		public abstract byte[] ReadAllBytes();
		public abstract void WriteAllBytes( byte[] text );
		public string ReadAllText() => DotNetHelpers.BomlessUtf8.GetString( ReadAllBytes() );
		public void WriteAllText( string text ) => WriteAllBytes( DotNetHelpers.BomlessUtf8.GetBytes( text ) );
		public abstract string GetDiagnosticPathName();
		public void Delete() => FileSystem.Delete( FileName );
		public void CopyFrom( Item sourceItem ) => WriteAllBytes( sourceItem.ReadAllBytes() );
	}

	public abstract Item CreateItem( FileName fileName );
	public abstract void Delete( FileName fileName );

	public Item CopyFrom( Item sourceItem )
	{
		Item targetItem = CreateItem( sourceItem.FileName );
		targetItem.CopyFrom( sourceItem );
		return targetItem;
	}

	public abstract IEnumerable<Item> EnumerateItems();

	public IEnumerable<Item> EnumerateItems( DirectoryName directoryName )
	{
		foreach( Item item in EnumerateItems() )
		{
			if( item.FileName.DirectoryName == directoryName )
				yield return item;
		}
	}

	public abstract bool Exists( FileName fileName );

	public void Clear()
	{
		foreach( Item? item in EnumerateItems() )
			item.Delete();
	}
}
