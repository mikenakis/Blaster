namespace Blaster;

using MikeNakis.Kit.Extensions;
using static MikeNakis.Kit.GlobalStatics;
using Sys = System;
using SysIo = System.IO;

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
		return new FileName( normalize( directoryName.Content + content ) );
	}

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
	public FileName WithReplacedExtension( string extension ) => WithoutExtension.WithExtension( extension );

	static string normalize( string pathname )
	{
#pragma warning disable RS0030 // Do not use banned APIs
		string s = SysIo.Path.GetFullPath( pathname );
		Assert( SysIo.Path.IsPathRooted( s ) );
		string root = SysIo.Path.GetPathRoot( s )!;
		Assert( root.EndsWith( '\\' ) );
		s = s[(root.Length - 1)..];
#pragma warning restore RS0030 // Do not use banned APIs
		s = s.Replace( '\\', '/' );
		return s;
	}
}
