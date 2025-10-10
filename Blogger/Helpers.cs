namespace Blogger;

using static Common.Statics;
using Math = System.Math;
using Sys = System;

static class Helpers
{
	public static string Indentation( int depth ) => new( ' ', depth * 4 );

	public static string Summarize( string text )
	{
		const int maxLength = 100;
		//text = text.Substring( 0, Math.Min( 100, text.Length ) );
		text = text.Replace( "\r\n", "\\r\\n" );
		text = text.Replace( "\r", "\\r" );
		text = text.Replace( "\n", "\\n" );
		if( text.Length < maxLength )
			return $"\"{text}\"";
		return $"\"{text.Substring( 0, maxLength - 10 )} ... ({text.Length} characters)";
	}

	public static bool AssertEquals( string a, string b )
	{
		int index = indexOfDifference( a, b );
		if( index != -1 )
		{
			Sys.Diagnostics.Debug.WriteLine( $"a: {a}" );
			Sys.Diagnostics.Debug.WriteLine( $"b: {b}" );
			Sys.Diagnostics.Debug.WriteLine( $"   {new string( ' ', index )}^" );
			Assert( false );
		}
		return true;

		static int indexOfDifference( string a, string b )
		{
			for( int i = 0; i < Math.Min( a.Length, b.Length ); i++ )
				if( a[i] != b[i] )
					return i;
			if( a.Length < b.Length )
				return a.Length;
			else if( b.Length < a.Length )
				return b.Length;
			return -1;
		}
	}
}
