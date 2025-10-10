namespace Blogger;

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Common;
using static Common.Statics;
using Sys = System;
using SysGlob = System.Globalization;
using SysIo = System.IO;
using SysText = System.Text;
using SysXmlLinq = System.Xml.Linq;

class MarkdownBlogWriter
{
	public static void WriteBlog( Blog blog, string blogDirectory )
	{
		if( SysIo.Directory.Exists( blogDirectory ) )
			SysIo.Directory.Delete( blogDirectory, true );
		SysIo.Directory.CreateDirectory( blogDirectory );

		var template = Template.Create( SysIo.File.ReadAllText( "PostTemplate.md" ) );
		var utf8Bomless = new SysText.UTF8Encoding( false );
		var markdownFromHtmlConverter = new MarkdownFromHtmlConverter();
		foreach( Post post in blog.Posts.Sort( ( a, b ) => a.Filename.CompareTo( b.Filename ) ) )
		{
			Sys.Console.WriteLine( post.Title );
			string postDirectoryName = getPostDirectoryName( blogDirectory, post );
			SysIo.Directory.CreateDirectory( postDirectoryName );
			template["title"] = escapeForYaml( fixForHugo( post.Title ) );
			template["description"] = ""; //post.Description;
			template["time-created"] = post.TimeCreated.ToString( @"yyyy-MM-ddTHH:mm:ss.fffZ", SysGlob.CultureInfo.InvariantCulture );
			template["time-updated"] = post.TimeUpdated.ToString( @"yyyy-MM-ddTHH:mm:ss.fffZ", SysGlob.CultureInfo.InvariantCulture );
			template["draft"] = (post.Status == PostStatus.Draft || post.Status == PostStatus.SoftTrashed).ToString();
			Template tagsTemplate = template.GetTemplate( "tags" );
			Template tagTemplate = tagsTemplate.GetTemplate( "tag" );
			template["tags"] = "";
			foreach( string category in post.Categories )
				template["tags"] += tagTemplate.GenerateText( ("name", fixTag( category )) );
			template["image"] = ""; //TODO
			string content = fixPostContent( post.Content );
			content = copyImages( content, postDirectoryName );
			content = markdownFromHtmlConverter.Convert( content );
			template["content"] = content;
			template["comments"] = stringFromComments( template, post.Comments );
			string text = template.GenerateText();
			string postPathName = SysIo.Path.GetFullPath( SysIo.Path.Combine( postDirectoryName, "index.md" ) );
			SysIo.File.WriteAllText( postPathName, text.Replace( "\r\n", "\n" ).Replace( "\n", "\r\n" ), utf8Bomless );
		}
		return;

		static string getPostDirectoryName( string blogDirectory, Post post )
		{
			Assert( post.Filename[0] == '/' );
			string bloggerPostFileName = post.Filename[1..];
			string postYearAndMonth = SysIo.Path.GetDirectoryName( bloggerPostFileName ) ?? throw new Sys.Exception();
			string postSlug = filenameFrom( SysIo.Path.GetFileName( bloggerPostFileName ) );
			string postRelativeDirectoryName = SysIo.Path.Combine( postYearAndMonth, postSlug );
			string postDirectoryName = SysIo.Path.GetFullPath( SysIo.Path.Combine( blogDirectory, postRelativeDirectoryName ) );
			Assert( postDirectoryName.StartsWith( blogDirectory ) );
			return postDirectoryName;
		}

		static string fixPostContent( string content )
		{
			//PEARL: get rid of square brackets inside link text to work around what appears to be a ridiculous bug of Hugo
			content = content.Replace( "Is my mentor's concern for code quality excessive? [closed]", "Is my mentor's concern for code quality excessive? (closed)" );
			return content;
		}

		static string copyImages( string content, string postDirectoryName )
		{
			SysXmlLinq.XDocument document = SysXmlLinq.XDocument.Parse( content );
			List<SysXmlLinq.XElement> elementsToRemove = new();
			foreach( SysXmlLinq.XElement imageElement in document.Descendants( "img" ) )
			{
				Assert( !imageElement.HasElements );
				Assert( imageElement.FirstNode == null );
				Assert( imageElement.LastNode == null );
				Assert( imageElement.Value == "" );
				SysXmlLinq.XAttribute? sourceAttribute = imageElement.Attribute( "src" );
				Assert( sourceAttribute != null );
				if( !SysIo.File.Exists( sourceAttribute.Value ) )
				{
					elementsToRemove.Add( imageElement );
					continue;
				}
				string fileName = copyImage( sourceAttribute.Value, postDirectoryName );
				imageElement.SetAttributeValue( "src", fileName );
				//SysXmlLinq.XAttribute? widthAttribute = imageElement.Attribute( "width" );
				//SysXmlLinq.XAttribute? heightAttribute = imageElement.Attribute( "height" );
				//SysXmlLinq.XAttribute? altAttribute = imageElement.Attribute( "alt" );
			}
			foreach( SysXmlLinq.XElement elementToRemove in elementsToRemove )
				elementToRemove.Remove();
			return document.ToString();

			static string copyImage( string sourceFilePathName, string postDirectoryName )
			{
				string fileName = fixFileName( SysIo.Path.GetFileName( sourceFilePathName ) );
				string targetFilePathName = SysIo.Path.Combine( postDirectoryName, fileName );
				SysIo.File.Copy( sourceFilePathName, targetFilePathName, true );
				return fileName;

				static string fixFileName( string fileName )
				{
					return fileName.Replace( ' ', '-' );
				}
			}
		}

		static string stringFromComments( Template template, ImmutableArray<Comment> comments )
		{
			if( comments.Length == 0 )
				return "";
			Template commentsTemplate = template.GetTemplate( "comments" );
			Template commentTemplate = commentsTemplate.GetTemplate( "comment" );
			SysText.StringBuilder stringBuilder = new();
			collectComments( 0, commentsTemplate, commentTemplate, comments, stringBuilder );
			commentsTemplate["comment"] = stringBuilder.ToString();
			return commentsTemplate.GenerateText();

			static void collectComments( int depth, Template commentsTemplate, Template commentTemplate, ImmutableArray<Comment> comments, SysText.StringBuilder stringBuilder )
			{
				foreach( Comment comment in comments )
				{
					commentTemplate["indentation"] = Helpers.Indentation( depth );
					commentTemplate["status"] = commentTemplate.GetTemplate( "status" ).GenerateText( ("value", comment.Status.ToString()) );
					commentTemplate["author-name"] = commentTemplate.GetTemplate( "author-name" ).GenerateText( ("value", comment.Author.Name) );
					commentTemplate["author-uri"] = comment.Author.Uri == "" ? "" : commentTemplate.GetTemplate( "author-uri" ).GenerateText( ("value", comment.Author.Uri) );
					commentTemplate["time-created"] = commentTemplate.GetTemplate( "time-created" ).GenerateText( ("value", comment.TimeCreated.ToString()) );
					commentTemplate["content"] = string.Join( '\n', comment.Content.Split( "\n" ).Select( line => Helpers.Indentation( depth + 1 ) + line ) );
					string commentText = commentTemplate.GenerateText();
					stringBuilder.Append( commentText );
					collectComments( depth + 1, commentsTemplate, commentTemplate, comment.Replies, stringBuilder );
				}
			}
		}

		static string filenameFrom( string text )
		{
			string invalidCharacters = "<>:\"/\\|?*";
			foreach( char c in invalidCharacters )
				text = text.Replace( c, '-' );
			text = text.Replace( "--", "-" );
			if( text.StartsWith( '-' ) )
				text = text[1..];
			text = SysIo.Path.GetFileNameWithoutExtension( text );
			return text;
		}

		//PEARL: trim dots to work around what appears to be a ridiculous bug of Hugo
		static string fixForHugo( string text )
		{
			text = text.Replace( '.', '-' );
			text = text.Replace( "--", "-" );
			if( text[0] == '-' )
				text = text[1..];
			if( text[^1] == '-' )
				text = text[..^1];
			return text;
		}
	}

	private static string fixTag( string tag )
	{
		tag = tag.Replace( ' ', '-' );
		return tag;
	}

	delegate void TextConsumer( Sys.ReadOnlySpan<char> text );

	static string escapeForYaml( Sys.ReadOnlySpan<char> instance )
	{
		SysText.StringBuilder stringBuilder = new();
		escapeForYaml( '"', instance, text => stringBuilder.Append( text ) );
		return stringBuilder.ToString();
	}

	static void escapeForYaml( char quoteCharacter, Sys.ReadOnlySpan<char> instance, TextConsumer textConsumer )
	{
		//TODO: cover all escapes, see https://stackoverflow.com/a/323664/773113
		textConsumer.Invoke( new Sys.ReadOnlySpan<char>( in quoteCharacter ) );
		foreach( char c in instance )
			if( c == quoteCharacter )
				emitEscapedCharacter( textConsumer, c );
			else
				switch( c )
				{
					case '\t':
						emitEscapedCharacter( textConsumer, 't' );
						break;
					case '\r':
						emitEscapedCharacter( textConsumer, 'r' );
						break;
					case '\n':
						emitEscapedCharacter( textConsumer, 'n' );
						break;
					case '\\':
						emitEscapedCharacter( textConsumer, '\\' );
						break;
					default:
						emitOtherCharacter( textConsumer, c );
						break;
				}
		textConsumer.Invoke( new Sys.ReadOnlySpan<char>( in quoteCharacter ) );
		return;

		static void emitEscapedCharacter( TextConsumer textConsumer, char c )
		{
			Sys.Span<char> buffer = stackalloc char[2];
			buffer[0] = '\\';
			buffer[1] = c;
			textConsumer.Invoke( buffer );
		}

		static void emitOtherCharacter( TextConsumer textConsumer, char c )
		{
			if( isPrintable( c ) )
				textConsumer.Invoke( new Sys.ReadOnlySpan<char>( in c ) );
			else
			{
				Sys.Span<char> buffer = stackalloc char[6];
				buffer[0] = '\\';
				buffer[1] = 'u';
				buffer[2] = digitFromNibble( c >> 12 & 0x0f );
				buffer[3] = digitFromNibble( c >> 8 & 0x0f );
				buffer[4] = digitFromNibble( c >> 4 & 0x0f );
				buffer[5] = digitFromNibble( c & 0x0f );
				textConsumer.Invoke( buffer );
			}
			return;

			static char digitFromNibble( int nibble )
			{
				Assert( nibble is >= 0 and < 16 );
				return (char)((nibble >= 10 ? 'a' - 10 : '0') + nibble);
			}
		}
	}

	static bool isPrintable( char c )
	{
		// see https://www.johndcook.com/blog/2013/04/11/which-unicode-characters-can-you-depend-on/
		// see https://en.wikipedia.org/wiki/Windows-1252
		return (int)c switch
		{
			< 32 => false,
			< 127 => true,
			< 160 => false,
			173 => false,
			< 256 => true,
			0x0152 => true, // Œ
			0x0153 => true, // œ
			0x0160 => true, // Š
			0x0161 => true, // š
			0x0178 => true, // Ÿ
			0x017D => true, // Ž
			0x017E => true, // ž
			0x0192 => true, // ƒ
			0x02C6 => true, // ˆ
			0x02DC => true, // ˜
			0x2013 => true, // –
			0x2014 => true, // —
			0x2018 => true, // ‘
			0x2019 => true, // ’
			0x201A => true, // ‚
			0x201C => true, // “
			0x201D => true, // ”
			0x201E => true, // „
			0x2020 => true, // †
			0x2021 => true, // ‡
			0x2022 => true, // •
			0x2026 => true, // …
			0x2030 => true, // ‰
			0x2039 => true, // ‹
			0x203A => true, // ›
			0x20AC => true, // €
			0x2122 => true, // ™
			_ => false
		};
	}
}
