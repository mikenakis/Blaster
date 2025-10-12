namespace Blogger;

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using static System.Threading.Tasks.TaskExtensions;
using static Common.Statics;
using NewtonsoftJsonLinq = Newtonsoft.Json.Linq;
using Regex = System.Text.RegularExpressions;
using Sys = System;
using SysIo = System.IO;
using SysNet = System.Net;
using SysNetHttp = System.Net.Http;
using SysTasks = System.Threading.Tasks;
using SysWeb = System.Web;
using SysXmlLinq = System.Xml.Linq;

class BloggerExportReader
{
	public static Blog ReadBloggerExport( string bloggerExportDirectory, string bloggerBlogName )
	{
		string blogDirectory = SysIo.Path.GetFullPath( SysIo.Path.Combine( bloggerExportDirectory, "Blogs", bloggerBlogName ) );
		string postsAtomFile = SysIo.Path.GetFullPath( SysIo.Path.Combine( blogDirectory, "feed.atom" ) );
		string xmlText = SysIo.File.ReadAllText( postsAtomFile );
		SysXmlLinq.XDocument document = SysXmlLinq.XDocument.Parse( xmlText );
		if( False )
			dumpXElement( 0, document.Root! );
		(Entity blogEntity, List<Entity> blogEntryEntities) = blogEntityFromXDocument( document );
		if( False )
			dumpBlogEntity( blogEntity, blogEntryEntities );
		string albumDirectory = SysIo.Path.GetFullPath( SysIo.Path.Combine( bloggerExportDirectory, "Albums", bloggerBlogName ) );
		IReadOnlyDictionary<string, Entity> imageEntities = getImageEntities( albumDirectory );
		Blog blog = blogFromBlogEntity( blogEntity, blogEntryEntities, albumDirectory, imageEntities );
		if( False )
			blog.Dump();
		return blog;
	}

	static IReadOnlyDictionary<string, Entity> getImageEntities( string albumDirectory )
	{
		Dictionary<string, Entity> entities = new();
		foreach( string jsonFilePathName in SysIo.Directory.EnumerateFiles( albumDirectory, "*.json" ) )
		{
			if( jsonFilePathName.EndsWith( "metadata.json" ) )
				continue;
			NewtonsoftJsonLinq.JObject data = NewtonsoftJsonLinq.JObject.Parse( SysIo.File.ReadAllText( jsonFilePathName ) );
			string imageFileName = SysIo.Path.GetFileNameWithoutExtension( jsonFilePathName );
			Regex.Match match = Regex.Regex.Match( imageFileName, "\\(\\d+\\)$" );
			if( match.Success )
			{
				Assert( match.Captures.Count == 1 && match.Groups.Count == 1 );
				imageFileName = imageFileName[..match.Index];
			}
			if( !SysIo.File.Exists( SysIo.Path.Combine( albumDirectory, imageFileName ) ) )
			{
				Sys.Console.WriteLine( $"WARNING: image does not exist: {imageFileName}" );
				continue;
			}
			string key = data["filename"]!.ToString();
			if( entities.ContainsKey( key ) )
			{
				//Sys.Console.WriteLine( $"INFO: duplicate image entity: {key}" );
				continue;
			}
			Entity entity = new Entity();
			entity.Add( "key", key );
			entity.Add( "imageFileName", imageFileName );
			foreach( string jsonPropertyName in new string[] { "uploadStatus", "sizeBytes", "creationTimestampMs", "hasOriginalBytes", "contentVersion", "mimeType" } )
				entity.Add( jsonPropertyName, data[jsonPropertyName]!.ToString() );
			entities.Add( key, entity );
		}
		return entities;
	}

	class Entity
	{
		readonly Dictionary<string, string> members = new();

		public string this[string key]
		{
			get => members[key];
			set
			{
				Assert( members.ContainsKey( key ) );
				members[key] = value;
			}
		}

		public void Add( string name, string value )
		{
			members.Add( name, value );
		}

		public IEnumerable<string> MemberNames()
		{
			return members.Keys;
		}
	}

	static Blog blogFromBlogEntity( Entity blogEntity, List<Entity> blogEntryEntities, string albumDirectory, //
		IReadOnlyDictionary<string, Entity> imageEntities )
	{
		List<SysTasks.Task> tasks = new();
		Dictionary<Entity, Post> entityToPostMap = new();
		Dictionary<Entity, Comment> entityToCommentMap = new();
		ImmutableArray<Entity> postEntities = blogEntryEntities.Where( entry => entry["type"] == "POST" ).ToImmutableArray();
		foreach( Entity postEntity in postEntities )
			blogEntryEntities.Remove( postEntity );
		List<Post> posts = new();
		foreach( Entity postEntity in postEntities )
		{
			string status = postEntity["status"];
			//Assert( postEntity["trashed"] == "" ); //"trashed" is the time of trashing; we do not care because we have status.
			//Assert( postEntity["location"] == "" ); //there is one blog post with location information, and it is in Palo Alto, CA, so it is meaningless and can safely be ignored.
			Assert( postEntity["link"] == "" );
			Assert( postEntity["metaDescription"] == "" );
			Assert( postEntity["enclosure"] == "" );
			ImmutableArray<string> categories = extractCategories( postEntity );
			ImmutableArray<Comment> comments = extractComments( blogEntryEntities, postEntity["id"], "" );
			fixImages( postEntity, albumDirectory, imageEntities, tasks );
			fixLinks( postEntity );
			string postTitle = postEntity["title"];
			var postAuthor = new Author( postEntity["author-name"], postEntity["author-uri"], authorTypeFromString( postEntity["author-type"] ) );
			Sys.DateTime postTimeCreated = Sys.DateTime.Parse( postEntity["created"] ).ToUniversalTime();
			Sys.DateTime postTimePublished = Sys.DateTime.Parse( postEntity["published"] ).ToUniversalTime();
			Sys.DateTime postTimeUpdated = Sys.DateTime.Parse( postEntity["updated"] ).ToUniversalTime();
			string postContent = postEntity["content"];
			string postFileName = fixPostFileName( postEntity["filename"], postTimeCreated, postTitle );
			Assert( categories.All( category => category.Length > 0 ) );
			Post post = new Post( postStatusFromString( status ), postFileName, postTitle, postAuthor, //
				postTimeCreated, postTimePublished, postTimeUpdated, postContent, categories, comments );
			posts.Add( post );
		}
		SysTasks.Task.WhenAll( tasks );
		Assert( blogEntryEntities.Count == 0 );
		return new Blog( blogEntity["title"], posts.ToImmutableArray() );

		static ImmutableArray<string> extractCategories( Entity postEntity )
		{
			return postEntity["categories"].Split( ',', Sys.StringSplitOptions.TrimEntries | Sys.StringSplitOptions.RemoveEmptyEntries ).ToImmutableArray();
		}

		static ImmutableArray<Comment> extractComments( List<Entity> blogEntryEntities, string postId, string inReplyTo )
		{
			ImmutableArray<Entity> commentEntities = blogEntryEntities.Where( entity => entity["type"] == "COMMENT" && entity["parent"] == postId && entity["inReplyTo"] == inReplyTo ).ToImmutableArray();
			foreach( Entity commentEntity in commentEntities )
				blogEntryEntities.Remove( commentEntity );
			return commentEntities.Select( commentEntity => commentFromCommentEntity( blogEntryEntities, commentEntity, postId, inReplyTo ) ).ToImmutableArray();
		}

		static string fixPostFileName( string fileName, Sys.DateTime timeCreated, string title )
		{
			if( fileName == "" )
				fileName = $"/{timeCreated.Year:D2}/{timeCreated.Month:D2}/{title}.html";
			return fileName;
		}

		static Comment commentFromCommentEntity( List<Entity> blogEntryEntities, Entity commentEntity, string postId, string inReplyTo )
		{
			string status = commentEntity["status"];
			AuthorType authorType = authorTypeFromString( commentEntity["author-type"] );
			Author author = new Author( fixAuthorName( commentEntity["author-name"], authorType ), commentEntity["author-uri"], authorType );
			Sys.DateTime timeCreated = Sys.DateTime.Parse( commentEntity["created"] ).ToUniversalTime();
			Sys.DateTime timePublished = Sys.DateTime.Parse( commentEntity["published"] ).ToUniversalTime();
			Sys.DateTime timeUpdated = Sys.DateTime.Parse( commentEntity["updated"] ).ToUniversalTime();
			Assert( commentEntity["trashed"] == "" );
			string content = commentEntity["content"];
			Assert( commentEntity["parent"] == postId );
			Assert( commentEntity["inReplyTo"] == inReplyTo );
			ImmutableArray<Comment> replies = extractComments( blogEntryEntities, postId, commentEntity["id"] );
			return new Comment( commentStatusFromString( status ), author, timeCreated, timePublished, timeUpdated, content, replies );

			static string fixAuthorName( string authorName, AuthorType authorType )
			{
				if( authorName != "" )
					return authorName;
				if( authorType == AuthorType.Anonymous )
					return "Anonymous";
				if( authorType == AuthorType.Blogger )
					return "Unspecified";
				Assert( false );
				return authorName;
			}
		}

		static PostStatus postStatusFromString( string status )
		{
			return status switch
			{
				"DRAFT" => PostStatus.Draft,
				"LIVE" => PostStatus.Live,
				"SOFT_TRASHED" => PostStatus.SoftTrashed,
				_ => throw new Sys.Exception()
			};
		}

		static CommentStatus commentStatusFromString( string status )
		{
			return status switch
			{
				"LIVE" => CommentStatus.Live,
				"SPAM_COMMENT" => CommentStatus.Spam,
				"GHOSTED_COMMENT" => CommentStatus.Ghosted,
				_ => throw new Sys.Exception()
			};
		}

		static AuthorType authorTypeFromString( string authorType )
		{
			return authorType switch
			{
				"BLOGGER" => AuthorType.Blogger,
				"ANONYMOUS" => AuthorType.Anonymous,
				_ => throw new Sys.Exception()
			};
		}
	}

	static string fixHtmlContent( string content )
	{
		if( content.StartsWith( "<p>So, for some time now, whenever" ) || content.StartsWith( "<p>Solon's original phrase was" ) )
			content += "</p>";
		else if( content.Contains( "preload=\"none\"\n    controls\n" ) )
			content = content.Replace( "preload=\"none\"\n    controls\n", "preload=\"none\"\n" );
		content = content.Replace( "<o:p>", "<p>" );
		content = content.Replace( "</o:p>", "</p>" );

		content = "<!DOCTYPE html PUBLIC " +
			"\"-//W3C//DTD XHTML 1.0 Transitional//EN\" \"http://www.w3.org/TR/xhtml1/DTD/xhtml1-transitional.dtd\"" +
			"[" +
			"<!ENTITY lt \"&#60;\">" +
			"<!ENTITY gt \"&#62;\">" +
			"<!ENTITY amp \"&#38;\">" +
			"<!ENTITY nbsp \"&#160;\">" +
			// &quot; 	&#34; 	
			// &apos; 	&#39; 	
			// &cent; 	&#162; 	
			// &pound; 	&#163; 	
			// &yen; 	&#165; 	
			// &euro; 	&#8364; 	
			// &copy; 	&#169; 	
			// &reg; 	&#174; 	
			// &trade; 	&#8482;
			"]>" +
			$"<html><head></head><body>" + content + "</body></html>";

		return content;
	}

	static void fixImages( Entity postEntity, string albumDirectory, IReadOnlyDictionary<string, Entity> imageEntities, List<SysTasks.Task> tasks )
	{
		string content = postEntity["content"];
		SysXmlLinq.XDocument document = SysXmlLinq.XDocument.Parse( content );
		int imageNumber = 1;
		Dictionary<SysXmlLinq.XElement, SysXmlLinq.XElement> elementsToReplace = new();
		foreach( SysXmlLinq.XElement imageElement in document.Descendants( "img" ) )
		{
			Assert( !imageElement.HasElements );
			Assert( imageElement.FirstNode == null );
			Assert( imageElement.LastNode == null );
			Assert( imageElement.Value == "" );
			Sys.Uri srcUri = new( imageElement.Attribute( "src" )!.Value );
			Assert( srcUri.Scheme == "http" || srcUri.Scheme == "https" );

			if( imageElement.Parent!.Name == "a" )
			{
				SysXmlLinq.XElement linkElement = imageElement.Parent!;
				Assert( linkElement.FirstNode == imageElement );
				Assert( linkElement.LastNode == imageElement );
				Sys.Uri hrefUri = new( linkElement.Attribute( "href" )!.Value );
				if( srcUri.Segments[^1].StartsWith( hrefUri.Segments[^1] ) )
					srcUri = hrefUri;
				if( srcUri.Segments[^1] == hrefUri.Segments[^1] )
					elementsToReplace.Add( linkElement, imageElement );
			}

			string imagePathName = getImage( srcUri, albumDirectory, postEntity["id"], imageNumber++, imageEntities, tasks );
			imageElement.SetAttributeValue( "src", imagePathName );
		}
		foreach( (SysXmlLinq.XElement linkElement, SysXmlLinq.XElement imageElement) in elementsToReplace )
			linkElement.ReplaceWith( imageElement );
		postEntity["content"] = document.ToString();
		return;

		static string getImage( Sys.Uri uri, string albumDirectory, string postId, int imageNumber, IReadOnlyDictionary<string, Entity> imageEntities, List<SysTasks.Task> tasks )
		{
			string key = decode( uri.Segments[^1] );
			if( imageEntities.TryGetValue( key, out Entity? imageEntity ) )
				return SysIo.Path.Combine( albumDirectory, imageEntity["imageFileName"] );

			string extension = SysIo.Path.GetExtension( key );
			if( extension == "" )
				extension = ".jpg";
			string filePathName = SysIo.Path.Combine( albumDirectory, "downloaded", fixFileName( $"{postId}{imageNumber}{extension}" ) );
			if( SysIo.File.Exists( filePathName ) )
				return filePathName;

			Sys.Console.WriteLine( $"WARNING: image not found locally: '{uri}'" );
			SysTasks.Task task = SysTasks.Task.Run( () => download( uri, filePathName ) );
			tasks.Add( task );
			return filePathName;

			static string decode( string s )
			{
				return SysWeb.HttpUtility.UrlDecode( s );
			}

			static string fixFileName( string fileName )
			{
				string invalidCharacters = "<>:\"/\\|?*";
				foreach( char c in invalidCharacters )
					fileName = fileName.Replace( c, '-' );
				return fileName;
			}

			static async SysTasks.Task download( Sys.Uri uri, string filePathName )
			{
				try
				{
					Sys.Console.WriteLine( $"INFO: begin download '{uri}' as '{filePathName}'" );
					byte[]? imageBytes = await download( uri );
					if( imageBytes != null )
					{
						string directory = SysIo.Path.GetDirectoryName( filePathName )!;
						SysIo.Directory.CreateDirectory( directory );
						await SysIo.File.WriteAllBytesAsync( filePathName, imageBytes );
					}
					Sys.Console.WriteLine( $"INFO: end download '{uri}' as '{filePathName}'" );
				}
				catch( Sys.Exception exception )
				{
					Sys.Console.WriteLine( $"ERROR: {exception.GetType()}: \"{exception.Message}\"" );
				}
				return;

				static async SysTasks.Task<byte[]?> download( Sys.Uri uri )
				{
					var handler = new SysNetHttp.HttpClientHandler();
					handler.AllowAutoRedirect = false;
					using( SysNetHttp.HttpClient client = new SysNetHttp.HttpClient( handler ) )
					{
						SysNetHttp.HttpResponseMessage response;
						for(; ; )
						{
							response = await client.GetAsync( uri ).ConfigureAwait( false );
							if( response.StatusCode == SysNet.HttpStatusCode.OK )
								return await response.Content.ReadAsByteArrayAsync();
							if( response.StatusCode == SysNet.HttpStatusCode.MovedPermanently ||
								response.StatusCode == SysNet.HttpStatusCode.Found )
							{
								uri = new( response.Headers.GetValues( "Location" ).First() );
								continue;
							}
							Sys.Console.WriteLine( $"WARNING: Not found: '{uri}'" );
							return null; //return Sys.Text.Encoding.UTF8.GetBytes( "<svg></svg>" );
						}
					}
				}
			}
		}
	}

	static void fixLinks( Entity postEntity )
	{
		string content = postEntity["content"];
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

			//PEARL: blogger has inserted these href attributes to anchor-only links in published posts and draft posts
			if( link == "https://www.blogger.com/null" || link == "https://draft.blogger.com/#" )
			{
				Sys.Console.WriteLine( $"WARNING: fixing '{link}' in \"{postEntity["title"]}\"" );
				hrefAttribute.Remove();
				continue;
			}

			//Some links are just invalid, containing text insteadf of an address; replace them with text.
			if( !Sys.Uri.IsWellFormedUriString( link, Sys.UriKind.RelativeOrAbsolute ) )
			{
				Sys.Console.WriteLine( $"WARNING: invalid link: '{link}' in \"{postEntity["title"]}\"" );
				linkElement.ReplaceWith( linkElement.Nodes() );
				continue;
			}

			Sys.Uri uri = new( link );
			Assert( uri.IsAbsoluteUri );
			Assert( uri.Scheme == "http" || uri.Scheme == "https" );

			//If this is a link to blogger.googleusercontent.com report it, ignore it.
			if( uri.Authority == "blogger.googleusercontent.com" )
			{
				Sys.Console.WriteLine( $"WARNING: blogger link: '{link}' in \"{postEntity["title"]}\"" );
				continue;
			}

			//Replace http: with https:
			if( uri.Scheme == "http" && uri.IsDefaultPort )
			{
				Sys.UriBuilder uriBuilder = new( uri );
				uriBuilder.Scheme = "https";
				uriBuilder.Port = 443;
				uri = uriBuilder.Uri;
				Assert( uri.IsDefaultPort );
				//Sys.Console.WriteLine( $"INFO: fixing '{hrefAttribute.Value}' -> '{uri}'" );
				hrefAttribute.Value = uri.ToString();
			}
		}
		postEntity["content"] = document.ToString();
	}

	static void dumpBlogEntity( Entity entity, List<Entity> blogEntryEntities )
	{
		Sys.Console.WriteLine( $"INFO: blog: Id={entity["id"]} Title={entity["title"]}" );
		Sys.Console.WriteLine( $"INFO: posts:" );
		foreach( Entity postEntity in blogEntryEntities.Where( entry => entry["type"] == "POST" ) )
		{
			dumpPostEntity( 1, postEntity );
			foreach( Entity commentEntity in blogEntryEntities.Where( entry => entry["type"] == "COMMENT" ) )
				if( commentEntity["parent"] == postEntity["id"] )
					dumpCommentEntity( 2, commentEntity );
		}

		static void dumpPostEntity( int depth, Entity entity )
		{
			Sys.Console.WriteLine( $"INFO: {Helpers.Indentation( depth )}POST Author-Name={entity["author-name"]} Author-Uri={entity["author-uri"]} author-type={entity["author-type"]} Status={entity["status"]} Created={entity["created"]} Published={entity["published"]} Updated={entity["updated"]} Trashed={entity["trashed"]} Title={entity["title"]} MetaDescription={entity["metaDescription"]} Location={entity["location"]} Filename={entity["filename"]}, Link={entity["link"]}, Enclosure={entity["enclosure"]}, Categories={entity["categories"]}" );
			Sys.Console.WriteLine( $"INFO: {Helpers.Indentation( depth )}    Content={Helpers.Summarize( entity["content"] )}" );
		}

		static void dumpCommentEntity( int depth, Entity entity )
		{
			Sys.Console.WriteLine( $"INFO: {Helpers.Indentation( depth )}COMMENT Author-Name={entity["author-name"]} Author-Uri={entity["author-uri"]} author-type={entity["author-type"]} Status={entity["status"]} Created={entity["created"]} Published={entity["published"]} Updated={entity["updated"]} Trashed={entity["trashed"]}  InReplyTo={entity["inReplyTo"]}" );
			Sys.Console.WriteLine( $"INFO: {Helpers.Indentation( depth )}    Content={Helpers.Summarize( entity["content"] )}" );
		}
	}

	static (Entity blogEntity, List<Entity> blogEntryEntities) blogEntityFromXDocument( SysXmlLinq.XDocument document )
	{
		SysXmlLinq.XElement feedElement = document.Elements().Single();
		Assert( feedElement.Name.LocalName == "feed" );
		Entity blogEntity = new();
		blogEntity.Add( "id", extractElement( feedElement, "id" ).Value );
		blogEntity.Add( "title", extractElement( feedElement, "title" ).Value );
		List<Entity> blogEntryEntities = new();
		while( true )
		{
			SysXmlLinq.XElement? entryElement = tryExtractElement( feedElement, "entry" );
			if( entryElement == null )
				break;
			Entity? entry = entryEntityFromXElement( entryElement );
			if( entry == null )
				continue;
			blogEntryEntities.Add( entry );
		}
		Assert( feedElement.IsEmpty );
		return (blogEntity, blogEntryEntities);
	}

	static Entity? entryEntityFromXElement( SysXmlLinq.XElement element )
	{
		Assert( element.Name.LocalName == "entry" );
		string type = extractElement( element, "type" ).Value;
		return type switch
		{
			"COMMENT" => commentEntityFromXElement( element ),
			"POST" => postEntityFromXElement( element ),
			"PAGE" => null,
			_ => throw new Sys.Exception(),
		};

		static Entity commentEntityFromXElement( SysXmlLinq.XElement element )
		{
			Entity entity = new();
			entity.Add( "type", "COMMENT" );
			entity.Add( "id", extractElement( element, "id" ).Value );
			entity.Add( "status", extractElement( element, "status" ).Value );
			SysXmlLinq.XElement authorElement = extractElement( element, "author" );
			entity.Add( "author-name", extractElement( authorElement, "name" ).Value );
			entity.Add( "author-uri", tryExtractElement( authorElement, "uri" )?.Value ?? "" );
			entity.Add( "author-type", extractElement( authorElement, "type" ).Value );
			entity.Add( "created", extractElement( element, "created" ).Value );
			entity.Add( "published", extractElement( element, "published" ).Value );
			entity.Add( "updated", extractElement( element, "updated" ).Value );
			entity.Add( "trashed", extractElement( element, "trashed" ).Value );
			entity.Add( "content", extractElement( element, "content" ).Value );
			entity.Add( "parent", extractElement( element, "parent" ).Value );
			entity.Add( "inReplyTo", extractElement( element, "inReplyTo" ).Value );
			Assert( element.IsEmpty );
			return entity;
		}

		static Entity postEntityFromXElement( SysXmlLinq.XElement element )
		{
			Entity entity = new();
			entity.Add( "type", "POST" );
			entity.Add( "id", extractElement( element, "id" ).Value );
			entity.Add( "status", extractElement( element, "status" ).Value );
			SysXmlLinq.XElement authorElement = extractElement( element, "author" );
			entity.Add( "author-name", extractElement( authorElement, "name" ).Value );
			entity.Add( "author-uri", tryExtractElement( authorElement, "uri" )?.Value ?? "" );
			entity.Add( "author-type", extractElement( authorElement, "type" ).Value );
			entity.Add( "created", extractElement( element, "created" ).Value );
			entity.Add( "published", extractElement( element, "published" ).Value );
			entity.Add( "updated", extractElement( element, "updated" ).Value );
			entity.Add( "trashed", extractElement( element, "trashed" ).Value );
			entity.Add( "content", fixHtmlContent( extractElement( element, "content" ).Value ) );
			entity.Add( "title", extractElement( element, "title" ).Value );
			entity.Add( "metaDescription", extractElement( element, "metaDescription" ).Value );
			entity.Add( "location", extractElement( element, "location" ).Value );
			entity.Add( "filename", extractElement( element, "filename" ).Value );
			entity.Add( "link", extractElement( element, "link" ).Value );
			entity.Add( "enclosure", extractElement( element, "enclosure" ).Value );
			List<string> categoryNames = new();
			while( true )
			{
				SysXmlLinq.XElement? categoryElement = tryExtractElement( element, "category" );
				if( categoryElement == null )
					break;
				string? categoryName = findAttribute( categoryElement, "term" );
				if( categoryName == null )
					continue;
				Assert( !categoryName.Contains( ',' ) );
				categoryNames.Add( categoryName );
			}
			entity.Add( "categories", string.Join( ", ", categoryNames ) );
			Assert( element.IsEmpty );
			return entity;
		}
	}

	static SysXmlLinq.XElement extractElement( SysXmlLinq.XElement element, string name )
	{
		SysXmlLinq.XElement result = findElement( element, name ) ?? throw new Sys.Exception();
		result.Remove();
		return result;
	}

	static SysXmlLinq.XElement? tryExtractElement( SysXmlLinq.XElement element, string name )
	{
		SysXmlLinq.XElement? result = findElement( element, name );
		if( result == null )
			return null;
		result.Remove();
		return result;
	}

	static SysXmlLinq.XElement? findElement( SysXmlLinq.XElement element, string name )
	{
		foreach( SysXmlLinq.XElement child in element.Elements() )
		{
			if( child.Name.LocalName == name )
				return child;
		}
		return null;
	}

	static string? findAttribute( SysXmlLinq.XElement element, string name )
	{
		foreach( SysXmlLinq.XAttribute child in element.Attributes() )
		{
			if( child.Name.LocalName == name )
				return child.Value;
		}
		return null;
	}

	static void dumpXElement( int depth, SysXmlLinq.XElement element )
	{
		output( depth, $"{element.Name.LocalName} {string.Join( ", ", element.Attributes().Select( attribute => $"{attribute.Name.LocalName} = \"{attribute.Value}\"" ) )} {elementValue( element )}" );
		foreach( SysXmlLinq.XElement childElement in element.Elements() )
			dumpXElement( depth + 1, childElement );
	}

	static void output( int depth, string content )
	{
		Sys.Console.WriteLine( $"INFO: {Helpers.Indentation( depth )}{content}" );
	}

	static string? getText( SysXmlLinq.XElement element )
	{
		return element.Nodes().OfType<SysXmlLinq.XText>().FirstOrDefault()?.Value;
	}

	static string elementValue( SysXmlLinq.XElement element )
	{
		string? text = getText( element );
		if( string.IsNullOrEmpty( text ) )
			return "";
		return $"text = {Helpers.Summarize( text )}";
	}
}
