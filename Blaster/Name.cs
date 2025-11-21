namespace Blaster;

using static MikeNakis.Kit.GlobalStatics;
using Sys = System;

readonly struct Name : Sys.IComparable<Name>, Sys.IEquatable<Name>
{
	public static bool operator ==( Name left, Name right ) => left.Equals( right );
	public static bool operator !=( Name left, Name right ) => !left.Equals( right );

	public static Name Of( string content )
	{
		return new Name( content );
	}

	public string Content { get; init; }

	Name( string content )
	{
		Content = content;
	}

	public int CompareTo( Name other ) => StringCompare( Content, other.Content );
	[Sys.Obsolete] public override bool Equals( object? other ) => other is Name kin && Equals( kin );
	public override int GetHashCode() => Content.GetHashCode( Sys.StringComparison.Ordinal );
	public bool Equals( Name other ) => CompareTo( other ) == 0;
	public override string ToString() => Content;
}
