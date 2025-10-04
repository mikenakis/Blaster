namespace Blogger;

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using static Common.Statics;
using Sys = System;
using SysIo = System.IO;
using SysXmlLinq = System.Xml.Linq;

class BloggerExportReader
{
	public static Blog ReadBloggerExport( string bloggerExportDirectory, string blogName )
	{
		string blogDirectory = SysIo.Path.GetFullPath( SysIo.Path.Combine( bloggerExportDirectory, "Blogs", blogName ) );
		string postsAtomFile = SysIo.Path.GetFullPath( SysIo.Path.Combine( blogDirectory, "feed.atom" ) );
		string xmlText = SysIo.File.ReadAllText( postsAtomFile );
		SysXmlLinq.XDocument document = SysXmlLinq.XDocument.Parse( xmlText );
		if( False )
			dumpXElement( 0, document.Root! );
		(Entity blogEntity, List<Entity> blogEntryEntities) = blogEntityFromXDocument( document );
		if( False )
			dumpBlogEntity( blogEntity, blogEntryEntities );
		Blog blog = blogFromBlogEntity( blogEntity, blogEntryEntities );
		if( False )
			blog.Dump();
		return blog;
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

	static Blog blogFromBlogEntity( Entity blogEntity, List<Entity> blogEntryEntities )
	{
		Dictionary<Entity, Post> entityToPostMap = new();
		Dictionary<Entity, Comment> entityToCommentMap = new();
		ImmutableArray<Entity> postEntities = blogEntryEntities.Where( entry => entry["type"] == "POST" ).ToImmutableArray();
		foreach( Entity postEntity in postEntities )
			blogEntryEntities.Remove( postEntity );
		List<Post> posts = new();
		foreach( Entity postEntity in postEntities )
		{
			string status = postEntity["status"];
			AuthorType authorType = authorTypeFromString( postEntity["author-type"] );
			Author author = new Author( postEntity["author-name"], postEntity["author-uri"], authorType );
			Sys.DateTime timeCreated = Sys.DateTime.Parse( postEntity["created"] ).ToUniversalTime();
			Sys.DateTime timePublished = Sys.DateTime.Parse( postEntity["published"] ).ToUniversalTime();
			Sys.DateTime timeUpdated = Sys.DateTime.Parse( postEntity["updated"] ).ToUniversalTime();
			//Assert( postEntity["trashed"] == "" ); //"trashed" is the time of trashing
			string content = postEntity["content"];
			string title = postEntity["title"];
			//Assert( postEntity["location"] == "" ); //there is one blog post with location information, and it is in Palo Alto, CA, so it is meaningless and can safely be ignored.
			ImmutableArray<string> categories = extractCategories( postEntity );
			string filename = postEntity["filename"];
			Assert( postEntity["link"] == "" );
			Assert( postEntity["metaDescription"] == "" );
			Assert( postEntity["enclosure"] == "" );
			ImmutableArray<Comment> comments = extractComments( blogEntryEntities, postEntity["id"], "" );
			Post post = new Post( postStatusFromString( status ), filename, title, author, timeCreated, timePublished, timeUpdated, content, categories, comments );
			posts.Add( post );
		}
		Assert( blogEntryEntities.Count == 0 );
		return new Blog( blogEntity["title"], posts.ToImmutableArray() );

		static ImmutableArray<string> extractCategories( Entity postEntity )
		{
			return postEntity["categories"].Split( ',', Sys.StringSplitOptions.TrimEntries ).ToImmutableArray();
		}

		static ImmutableArray<Comment> extractComments( List<Entity> blogEntryEntities, string postId, string inReplyTo )
		{
			ImmutableArray<Entity> commentEntities = blogEntryEntities.Where( entity => entity["type"] == "COMMENT" && entity["parent"] == postId && entity["inReplyTo"] == inReplyTo ).ToImmutableArray();
			foreach( Entity commentEntity in commentEntities )
				blogEntryEntities.Remove( commentEntity );
			return commentEntities.Select( commentEntity => commentFromCommentEntity( blogEntryEntities, commentEntity, postId, inReplyTo ) ).ToImmutableArray();
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
				//"DRAFT" => CommentStatus.Draft,
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

	static void dumpBlogEntity( Entity entity, List<Entity> blogEntryEntities )
	{
		Sys.Console.WriteLine( $"blog: Id={entity["id"]} Title={entity["title"]}" );
		Sys.Console.WriteLine( $"posts:" );
		foreach( Entity postEntity in blogEntryEntities.Where( entry => entry["type"] == "POST" ) )
		{
			dumpPostEntity( 1, postEntity );
			foreach( Entity commentEntity in blogEntryEntities.Where( entry => entry["type"] == "COMMENT" ) )
				if( commentEntity["parent"] == postEntity["id"] )
					dumpCommentEntity( 2, commentEntity );
		}

		static void dumpPostEntity( int depth, Entity entity )
		{
			Sys.Console.WriteLine( $"{Helpers.Indentation( depth )}POST Author-Name={entity["author-name"]} Author-Uri={entity["author-uri"]} author-type={entity["author-type"]} Status={entity["status"]} Created={entity["created"]} Published={entity["published"]} Updated={entity["updated"]} Trashed={entity["trashed"]} Title={entity["title"]} MetaDescription={entity["metaDescription"]} Location={entity["location"]} Filename={entity["filename"]}, Link={entity["link"]}, Enclosure={entity["enclosure"]}, Categories={entity["categories"]}" );
			Sys.Console.WriteLine( $"{Helpers.Indentation( depth )}    Content={Helpers.Summarize( entity["content"] )}" );
		}

		static void dumpCommentEntity( int depth, Entity entity )
		{
			Sys.Console.WriteLine( $"{Helpers.Indentation( depth )}COMMENT Author-Name={entity["author-name"]} Author-Uri={entity["author-uri"]} author-type={entity["author-type"]} Status={entity["status"]} Created={entity["created"]} Published={entity["published"]} Updated={entity["updated"]} Trashed={entity["trashed"]}  InReplyTo={entity["inReplyTo"]}" );
			Sys.Console.WriteLine( $"{Helpers.Indentation( depth )}    Content={Helpers.Summarize( entity["content"] )}" );
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
			entity.Add( "content", extractElement( element, "content" ).Value );
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
		Sys.Console.WriteLine( $"{Helpers.Indentation( depth )}{content}" );
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
