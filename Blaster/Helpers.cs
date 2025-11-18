namespace Blaster;

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using MikeNakis.Kit.Collections;
using static MikeNakis.Kit.GlobalStatics;
using Sys = System;
using SysText = System.Text;

static class Helpers
{
	public static string GetLine( FileItem item, int lineNumber )
	{
		if( lineNumber == 0 )
			return "";
		string text = item.ReadAllText();
		return text.Split( '\n' ).Skip( lineNumber - 1 ).First();
	}

	internal static (int lineNumber, int columnNumber, int length) GetSpanInformation( FileItem item, int start, int end )
	{
		string text = item.ReadAllText();
		ImmutableArray<string> lines = text.Split( '\n' ).ToImmutableArray();
		int offset = 0;
		for( int i = 0; i < lines.Length; i++ )
		{
			int columnNumber = start - offset;
			if( columnNumber < lines[i].Length )
			{
				int length = end - start + 1;
				if( columnNumber + length > lines[i].Length )
					length = lines[i].Length - columnNumber;
				return (i + 1, columnNumber, length);
			}
		}
		return (0, 0, 0);
	}

	public static void PrintTree<T>( T rootNode, Sys.Func<T, IEnumerable<T>> breeder, Sys.Func<T, string> stringizer, Sys.Action<string> emitter, int indentation = 1 )
	{
		Assert( indentation >= 1 );

		const char verticalAndRight = '\u251c'; // Unicode U+251C "Box Drawings Light Vertical and Right"
		const char horizontal = '\u2500'; // Unicode U+2500 "Box Drawings Light Horizontal"
		const char upAndRight = '\u2514'; // Unicode U+2514 "Box Drawings Light Up and Right"
		const char vertical = '\u2502'; // Unicode U+2502 "Box Drawings Light Vertical"
		const char blackSquare = '\u25a0'; // Unicode U+25A0 "Black Square"

		string parentIndentation = new string( horizontal, indentation );
		string childIndentation = new string( ' ', indentation );
		string nonLastParentPrefix = $"{verticalAndRight}{parentIndentation}";
		string lastParentPrefix = $"{upAndRight}{parentIndentation}";
		string nonLastChildPrefix = $"{vertical}{childIndentation}";
		string lastChildPrefix = $" {childIndentation}";
		string terminal = $"{blackSquare} ";

		SysText.StringBuilder stringBuilder = new();
		recurse( "", rootNode, "" );
		return;

		void recurse( string parentPrefix, T node, string childPrefix )
		{
			int position = stringBuilder.Length;
			stringBuilder.Append( parentPrefix ).Append( terminal );
			stringBuilder.Append( stringizer.Invoke( node ) );
			emitter.Invoke( stringBuilder.ToString() );
			stringBuilder.Length = position;
			stringBuilder.Append( childPrefix );
			IReadOnlyList<T> children = breeder.Invoke( node ).Collect();
			foreach( T childNode in children.Take( children.Count - 1 ) )
				recurse( nonLastParentPrefix, childNode, nonLastChildPrefix );
			if( children.Count > 0 )
				recurse( lastParentPrefix, children[^1], lastChildPrefix );
			stringBuilder.Length = position;
		}
	}
}
