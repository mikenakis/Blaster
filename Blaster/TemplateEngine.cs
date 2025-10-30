namespace Blaster;

using static MikeNakis.Kit.GlobalStatics;

public sealed class TemplateEngine
{
	public static TemplateEngine Create( string text, string openingMarker = "{{", string closingMarker = "}}" )
	{
		int startIndex = 0;
		int index = 0;
		List<int> lengths = new();
		List<Field> fields = new();
		while( true )
		{
			int nextOpeningMarkerIndex = text.IndexOf( openingMarker, index, Sys.StringComparison.Ordinal );
			if( nextOpeningMarkerIndex == -1 )
			{
				lengths.Add( text.Length - startIndex );
				break;
			}
			index = nextOpeningMarkerIndex;
			index += openingMarker.Length;
			index = skipWhitespace( text, index );
			string name = parseIdentifier( text, index );
			if( name.Length > 0 )
			{
				index += name.Length;
				index = skipWhitespace( text, index );
				if( match( text, index, closingMarker ) )
				{
					index += closingMarker.Length;
					lengths.Add( nextOpeningMarkerIndex - startIndex );
					fields.Add( new Field( name, index - nextOpeningMarkerIndex ) );
					startIndex = index;
				}
			}
		}
		return new TemplateEngine( text, lengths.ToImmutableArray(), fields.ToImmutableArray() );

		static int skipWhitespace( string text, int index )
		{
			while( index < text.Length && isWhitespace( text[index] ) )
				index++;
			return index;
		}

		static string parseIdentifier( string text, int startIndex )
		{
			if( startIndex >= text.Length || !isAlphabetic( text[startIndex] ) )
				return "";
			int index = startIndex + 1;
			while( index < text.Length && isAlphanumeric( text[index] ) )
				index++;
			return text[startIndex..index];
		}

		static bool isAlphabetic( char c ) => c is >= 'a' and <= 'z' or >= 'A' and <= 'Z';

		static bool isAlphanumeric( char c ) => isAlphabetic( c ) || c is >= '0' and <= '9' or '-' or '_';

		static bool isWhitespace( char c ) => c is ' ' or '\r' or '\n' or '\t';

		static bool match( string text, int start, string token )
		{
			int end = start + token.Length;
			if( end > text.Length )
				return false;
			return text.AsSpan()[start..end].SequenceEqual( token );
		}
	}

	sealed class Field
	{
		public readonly string Name;
		public readonly int Length;

		public Field( string name, int length )
		{
			Name = name;
			Length = length;
		}
	}

	readonly string text;
	readonly ImmutableArray<int> lengths;
	readonly ImmutableArray<Field> fields;
	public int Length { get; }
	public IEnumerable<string> FieldNames => fields.Select( field => field.Name );

	TemplateEngine( string text, ImmutableArray<int> lengths, ImmutableArray<Field> fields )
	{
		Assert( lengths.Length == fields.Length + 1 );
		this.text = text;
		this.lengths = lengths;
		this.fields = fields;
		Assert( lengths.Sum() + fields.Select( field => field.Length ).Sum() == text.Length );
	}

	public string GenerateText( Sys.Func<string, string> mapper )
	{
		SysText.StringBuilder stringBuilder = new();
		GenerateText( stringBuilder, mapper );
		return stringBuilder.ToString();
	}

	public void GenerateText( SysText.StringBuilder stringBuilder, Sys.Func<string, string> mapper )
	{
		int index = 0;
		for( int i = 0; i < lengths.Length; i++ )
		{
			int length = lengths[i];
			stringBuilder.Append( text.AsSpan( index, length ) );
			index += length;
			if( i + 1 < lengths.Length )
			{
				Field field = fields[i];
				string value = mapper.Invoke( field.Name );
				stringBuilder.Append( value );
				index += field.Length;
			}
		}
	}
}
