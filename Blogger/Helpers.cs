namespace Blogger;

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
}
