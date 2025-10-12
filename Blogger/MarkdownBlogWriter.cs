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
	public static void WriteBlog( Blog blog, string blogDirectory, Sys.Uri sourceBaseUri )
	{
		if( SysIo.Directory.Exists( blogDirectory ) )
			SysIo.Directory.Delete( blogDirectory, true );
		SysIo.Directory.CreateDirectory( blogDirectory );

		var template = Template.Create( SysIo.File.ReadAllText( "PostTemplate.md" ) );
		var utf8Bomless = new SysText.UTF8Encoding( false );
		var markdownFromHtmlConverter = new MarkdownFromHtmlConverter();
		foreach( Post post in blog.Posts )
		{
			//Sys.Console.WriteLine( post.Title );
			string postLocalPathName = post.Filename;
			string postRelativeDirectoryName = getPostRelativeDirectoryPathName( postLocalPathName );
			string postDirectoryPathName = getPostDirectoryPathName( blogDirectory, postRelativeDirectoryName );
			SysIo.Directory.CreateDirectory( postDirectoryPathName );
			template["title"] = escapeForYaml( fixForHugo( post.Title ) );
			template["time-created"] = post.TimeCreated.ToString( @"yyyy-MM-ddTHH:mm:ss.fffZ", SysGlob.CultureInfo.InvariantCulture );
			template["time-updated"] = post.TimeUpdated.ToString( @"yyyy-MM-ddTHH:mm:ss.fffZ", SysGlob.CultureInfo.InvariantCulture );
			template["draft"] = (post.Status == PostStatus.Draft || post.Status == PostStatus.SoftTrashed).ToString();
			Template tagsTemplate = template.GetTemplate( "tags" );
			template["tags"] = getTags( post, tagsTemplate );
			template["image"] = ""; //TODO
			string content = post.Content;
			content = copyImages( content, postDirectoryPathName, "images" );
			content = fixInternalLinks( content, sourceBaseUri, postLocalPathName );
			content = markdownFromHtmlConverter.Convert( content );
			template["content"] = content;
			template["comments"] = getComments( template.GetTemplate( "comments" ), post.Comments, markdownFromHtmlConverter );
			string text = template.GenerateText();
			string postPathName = SysIo.Path.GetFullPath( SysIo.Path.Combine( postDirectoryPathName, "index.md" ) );
			SysIo.File.WriteAllText( postPathName, text.Replace( "\r\n", "\n" ).Replace( "\n", "\r\n" ), utf8Bomless );
		}
		return;

		static string getTags( Post post, Template tagsTemplate )
		{
			if( post.Categories.Length == 0 )
				return "";
			Template tagTemplate = tagsTemplate.GetTemplate( "tag" );
			SysText.StringBuilder tagsStringBuilder = new();
			foreach( string category in post.Categories )
				tagsStringBuilder.Append( tagTemplate.GenerateText( ("name", fixTag( category )) ) );
			string content = tagsStringBuilder.ToString();
			tagsTemplate["tag"] = content;
			return tagsTemplate.GenerateText();
		}

		static string fixInternalLinks( string content, Sys.Uri sourceBaseUri, string filename )
		{
			SysXmlLinq.XDocument document = SysXmlLinq.XDocument.Parse( content );
			foreach( SysXmlLinq.XElement linkElement in document.Descendants( "a" ).ToImmutableArray() )
			{
				SysXmlLinq.XAttribute? hrefAttribute = linkElement.Attribute( "href" );

				//links without an `href` attribute are presumably anchors.
				if( hrefAttribute == null )
					continue;

				string link = hrefAttribute.Value.Trim();

				//If this is a link to an anchor, leave it as-is.
				if( link.StartsWith( '#' ) )
					continue;

				Sys.Uri uri = new( link );
				Assert( uri.IsAbsoluteUri );
				Assert( uri.Scheme == "https" );

				if( sourceBaseUri.IsBaseOf( uri ) )
				{
					Assert( SysIo.Path.GetExtension( uri.LocalPath ) == ".html" );
					Assert( uri.Query == "" );
					string postLocalPathName = uri.LocalPath;
					string postRelativeDirectoryName = "/" + getPostRelativeDirectoryPathName( postLocalPathName );
					string postDirectoryPathName = getPathNameWithoutExtension( postRelativeDirectoryName );
					string postFilePathName = SysIo.Path.Combine( postDirectoryPathName, "index.md" );
					string relativePath = SysIo.Path.GetRelativePath( filename, postFilePathName );
					Sys.Console.WriteLine( $"INFO: converting internal link: '{uri}' -> '{relativePath}'" );
					hrefAttribute.Value = relativePath.Replace( '\\', '/' );
				}
			}
			return document.ToString();
		}

		static string getPathNameWithoutExtension( string pathName )
		{
			string directoryName = SysIo.Path.GetDirectoryName( pathName ) ?? "/";
			string fileName = SysIo.Path.GetFileNameWithoutExtension( pathName );
			return SysIo.Path.Combine( directoryName, fileName );
		}

		static string getPostRelativeDirectoryPathName( string bloggerPostFileName )
		{
			Assert( bloggerPostFileName[0] == '/' );
			bloggerPostFileName = bloggerPostFileName[1..];
			string postYearAndMonth = SysIo.Path.GetDirectoryName( bloggerPostFileName ) ?? throw new Sys.Exception();
			string postSlug = filenameFrom( SysIo.Path.GetFileName( bloggerPostFileName ) );
			return SysIo.Path.Combine( postYearAndMonth, postSlug ).Replace( '\\', '/' );

			static string filenameFrom( string text )
			{
				string invalidCharacters = "<>:\"/\\|?* ";
				foreach( char c in invalidCharacters )
					text = text.Replace( c, '-' );
				text = text.Replace( "--", "-" );
				if( text.StartsWith( '-' ) )
					text = text[1..];
				text = SysIo.Path.GetFileNameWithoutExtension( text );
				return text;
			}
		}

		static string getPostDirectoryPathName( string blogDirectory, string postRelativeDirectoryName )
		{
			string postDirectoryName = SysIo.Path.GetFullPath( SysIo.Path.Combine( blogDirectory, postRelativeDirectoryName ) );
			Assert( postDirectoryName.StartsWith( blogDirectory ) );
			return postDirectoryName;
		}

		static string copyImages( string content, string postDirectoryName, string imagesDirectoryName )
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
				string fileName = copyImage( sourceAttribute.Value, postDirectoryName, imagesDirectoryName );
				imageElement.SetAttributeValue( "src", fileName );
			}
			foreach( SysXmlLinq.XElement elementToRemove in elementsToRemove )
				elementToRemove.Remove();
			return document.ToString();

			static string copyImage( string sourceFilePathName, string postDirectoryPathName, string imagesDirectoryName )
			{
				string fileName = fixFileName( SysIo.Path.GetFileName( sourceFilePathName ) );
				string targetDirectoryPathName = SysIo.Path.Combine( postDirectoryPathName, imagesDirectoryName );
				SysIo.Directory.CreateDirectory( targetDirectoryPathName );
				string targetFilePathName = SysIo.Path.Combine( targetDirectoryPathName, fileName );
				SysIo.File.Copy( sourceFilePathName, targetFilePathName, true );
				string result = SysIo.Path.GetRelativePath( postDirectoryPathName, targetFilePathName );
				Assert( result == SysIo.Path.Combine( imagesDirectoryName, fileName ) );
				return result.Replace( '\\', '/' );

				static string fixFileName( string fileName )
				{
					return fileName.Replace( ' ', '-' );
				}
			}
		}

		static string getComments( Template commentsTemplate, ImmutableArray<Comment> comments, MarkdownFromHtmlConverter markdownFromHtmlConverter )
		{
			Template commentTemplate = commentsTemplate.GetTemplate( "comment" );
			string content = collectComments( 0, commentTemplate, comments, markdownFromHtmlConverter );
			if( content == "" )
				return content;
			commentsTemplate["comment"] = content;
			return commentsTemplate.GenerateText();

			static string collectComments( int depth, Template commentTemplate, ImmutableArray<Comment> comments, MarkdownFromHtmlConverter markdownFromHtmlConverter )
			{
				SysText.StringBuilder stringBuilder = new();
				recurse( depth, commentTemplate, comments, stringBuilder, markdownFromHtmlConverter );
				return stringBuilder.ToString();

				static void recurse( int depth, Template commentTemplate, ImmutableArray<Comment> comments, SysText.StringBuilder stringBuilder, MarkdownFromHtmlConverter markdownFromHtmlConverter )
				{
					foreach( Comment comment in comments )
					{
						if( comment.Status == CommentStatus.Spam || comment.Status == CommentStatus.Ghosted )
							continue;
						commentTemplate["indentation"] = Helpers.Indentation( depth );
						string author = comment.Author.Name;
						if( comment.Author.Uri != "" )
							author = $"[{author}]({comment.Author.Uri})";
						commentTemplate["author"] = commentTemplate.GetTemplate( "author" ).GenerateText( ("value", author) );
						commentTemplate["time-created"] = commentTemplate.GetTemplate( "time-created" ).GenerateText( ("value", comment.TimeCreated.ToString( "yyyy-MM-dd HH:mm:ss \"UTC\"", SysGlob.CultureInfo.InvariantCulture )) );
						string content = comment.Content;
						content = replaceAll( content, "$$", "$ $" ); //PEARL: disallow two dollar signs in a row to work around some weirdness of obsidian.
						content = markdownFromHtmlConverter.Convert( content );
						commentTemplate["content"] = string.Join( '\n', content.Split( "\n" ).Select( line => Helpers.Indentation( depth + 1 ) + line ) );
						string commentText = commentTemplate.GenerateText();
						stringBuilder.Append( commentText );
						recurse( depth + 1, commentTemplate, comment.Replies, stringBuilder, markdownFromHtmlConverter );
					}
				}
			}

			static string replaceAll( string s, string a, string b )
			{
				while( true )
				{
					string t = s.Replace( a, b );
					if( t == s )
						return s;
					s = t;
				}
			}
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
		return tag.Replace( ' ', '-' );
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
						textConsumer.Invoke( isPrintable( c ) ? [c] : ['\\', 'u', d( c >> 12 & 0x0f ), d( c >> 8 & 0x0f ), d( c >> 4 & 0x0f ), d( c & 0x0f )] );
						break;
				}
		textConsumer.Invoke( new Sys.ReadOnlySpan<char>( in quoteCharacter ) );
		return;

		static void emitEscapedCharacter( TextConsumer textConsumer, char c )
		{
			Sys.Span<char> buffer = ['\\', c];
			textConsumer.Invoke( buffer );
		}

		static char d( int nibble )
		{
			Assert( nibble is >= 0 and < 16 );
			return (char)((nibble >= 10 ? 'a' - 10 : '0') + nibble);
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
