namespace Common;

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using static System.MemoryExtensions;
using static Statics;
using Sys = System;
using SysText = System.Text;

public sealed class Template
{
	public static Template Create( string text, string openingMarker = "{{", string closingMarker = "}}", int startIndex = 0 )
	{
		int index = startIndex;
		List<int> lengths = new();
		List<Variable> templates = new();
		while( true )
		{
			int nextSpanLength = getNextSpanLength( text, openingMarker, closingMarker, index );
			if( nextSpanLength < 0 )
			{
				lengths.Add( ~nextSpanLength );
				break;
			}
			lengths.Add( nextSpanLength );
			index += nextSpanLength;
			int start = index;
			index += openingMarker.Length;
			string name = parseIdentifier( text, index );
			Assert( name.Length > 0 );
			index += name.Length;
			if( match( text, index, closingMarker ) )
			{
				index += closingMarker.Length;
				templates.Add( new SimpleVariable( name, index - start ) );
			}
			else
			{
				Assert( isWhitespace( text[index] ) );
				index++;
				Template templateEngine = Create( text, openingMarker, closingMarker, index );
				index += templateEngine.Length;
				templates.Add( new TemplateVariable( name, (index - start) + closingMarker.Length, templateEngine ) );
				Assert( match( text, index, closingMarker ) );
				index += closingMarker.Length;
			}
		}
		return new Template( text, startIndex, lengths.ToImmutableArray(), templates.ToImmutableArray() );

		static int getNextSpanLength( string text, string openingMarker, string closingMarker, int index )
		{
			int nextOpeningMarkerIndex = text.IndexOf( openingMarker, index, Sys.StringComparison.Ordinal );
			int nextClosingMarkerIndex = text.IndexOf( closingMarker, index, Sys.StringComparison.Ordinal );
			if( nextClosingMarkerIndex != -1 && (nextOpeningMarkerIndex == -1 || nextClosingMarkerIndex < nextOpeningMarkerIndex) )
				return ~(nextClosingMarkerIndex - index);
			if( nextOpeningMarkerIndex == -1 )
				return ~(text.Length - index);
			return nextOpeningMarkerIndex - index;
		}

		static string parseIdentifier( string text, int startIndex )
		{
			if( !isAlphabetic( text[startIndex] ) )
				return "";
			int index = startIndex + 1;
			while( isAlphanumeric( text[index] ) )
				index++;
			return text[startIndex..index];
		}

		static bool isAlphabetic( char c ) => c is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z');

		static bool isAlphanumeric( char c ) => isAlphabetic( c ) || c is (>= '0' and <= '9') or '-' or '_';

		static bool isWhitespace( char c ) => c is ' ' or '\r' or '\n' or '\t';

		static bool match( string text, int start, string token )
		{
			int end = start + token.Length;
			if( end > text.Length )
				return false;
			return text.AsSpan()[start..end].SequenceEqual( token );
		}
	}

	abstract class Variable
	{
		public readonly string Name;
		public readonly int Length;
		public string Value { get; set; } = "?";

		protected Variable( string name, int length )
		{
			Name = name;
			Length = length;
		}
	}

	sealed class SimpleVariable : Variable
	{
		public SimpleVariable( string name, int length )
			 : base( name, length )
		{
		}
	}

	sealed class TemplateVariable : Variable
	{
		public readonly Template TemplateEngine;

		public TemplateVariable( string name, int length, Template templateEngine )
			 : base( name, length )
		{
			TemplateEngine = templateEngine;
			Value = templateEngine.GenerateText();
		}
	}

	readonly string text;
	readonly int startIndex;
	readonly ImmutableArray<int> lengths;
	readonly ImmutableArray<Variable> templates;
	readonly Dictionary<string, Variable> templatesByName;
	public int Length;

	Template( string text, int startIndex, ImmutableArray<int> lengths, ImmutableArray<Variable> templates )
	{
		Assert( lengths.Length == templates.Length + 1 );
		this.text = text;
		this.startIndex = startIndex;
		this.lengths = lengths;
		this.templates = templates;
		Length = lengths.Sum() + templates.Select( template => template.Length ).Sum();
		Assert( startIndex > 0 || Length == text.Length );
		templatesByName = templates.ToDictionary( template => template.Name, Identity );
	}

	public string this[string name] { get => GetValue( name ); set => SetValue( name, value ); }

	public string GetValue( string name ) => templatesByName[name].Value;

	public void SetValue( string name, string value ) => templatesByName[name].Value = value;

	public Template GetTemplate( string name )
	{
		Variable template = templatesByName[name];
		return ((TemplateVariable)template).TemplateEngine;
	}

	public string GenerateText( params (string name, string value)[] pairs )
	{
		foreach( (string name, string value) in pairs )
			SetValue( name, value );
		return GenerateText();
	}

	public string GenerateText()
	{
		SysText.StringBuilder stringBuilder = new();
		GenerateText( stringBuilder );
		return stringBuilder.ToString();
	}

	public void GenerateText( SysText.StringBuilder stringBuilder )
	{
		int index = startIndex;
		int i;
		for( i = 0; i < lengths.Length; i++ )
		{
			int length = lengths[i];
			stringBuilder.Append( text.AsSpan( index, length ) );
			index += length;
			if( i + 1 < lengths.Length )
			{
				Variable template = templates[i];
				stringBuilder.Append( template.Value );
				index += template.Length;
			}
		}
	}

	// TODO: move to a separate test project
	public static void Test()
	{
		Template template = Template.Create( "0 {{a}} 1" );
		string text = template.GenerateText();
		Assert( text == "0 ? 1" );
		template["a"] = "X";
		text = template.GenerateText();
		Assert( text == "0 X 1" );

		template = Template.Create( "{{a}}" );
		text = template.GenerateText();
		Assert( text == "?" );
		template["a"] = "X";
		text = template.GenerateText();
		Assert( text == "X" );

		template = Template.Create( "0 {{a}}{{b}} 1" );
		text = template.GenerateText();
		Assert( text == "0 ?? 1" );
		template["a"] = "X";
		template["b"] = "Y";
		text = template.GenerateText();
		Assert( text == "0 XY 1" );

		template = Template.Create( "0 {{a 1}} 2" );
		text = template.GenerateText();
		Assert( text == "0 1 2" );
		Template aTemplate = template.GetTemplate( "a" );
		text = aTemplate.GenerateText();
		Assert( text == "1" );
		template["a"] = "X";
		text = template.GenerateText();
		Assert( text == "0 X 2" );

		template = Template.Create( "0 {{a 1 {{b}} 2}} 3" );
		text = template.GenerateText();
		Assert( text == "0 1 ? 2 3" );
		aTemplate = template.GetTemplate( "a" );
		aTemplate["b"] = "X";
		template["a"] = aTemplate.GenerateText();
		text = template.GenerateText();
		Assert( text == "0 1 X 2 3" );

		template = Template.Create( "0 {{a 1 {{b 2}} 3}} 4" );
		text = template.GenerateText();
		Assert( text == "0 1 2 3 4" );
		aTemplate = template.GetTemplate( "a" );
		aTemplate["b"] = "X";
		template["a"] = aTemplate.GenerateText();
		text = template.GenerateText();
		Assert( text == "0 1 X 3 4" );

		Identity( template );
	}
}
