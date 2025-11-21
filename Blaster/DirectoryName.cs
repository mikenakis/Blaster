namespace Blaster;

using MikeNakis.Kit.Extensions;
using static MikeNakis.Kit.GlobalStatics;
using Sys = System;

public readonly struct DirectoryName : Sys.IComparable<DirectoryName>, Sys.IEquatable<DirectoryName>
{
	public static bool operator ==( DirectoryName left, DirectoryName right ) => left.Equals( right );
	public static bool operator !=( DirectoryName left, DirectoryName right ) => !left.Equals( right );

	public static DirectoryName Of( string content )
	{
		return new DirectoryName( content );
	}

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
