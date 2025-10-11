namespace Blogger;

using System.Collections.Immutable;
using Sys = System;

sealed record class Blog( string Title, ImmutableArray<Post> Posts )
{
	public void Dump()
	{
		Sys.Console.WriteLine( $"INFO: Blog Title=\"{Title}\"" );
		foreach( Post post in Posts )
		{
			Sys.Console.WriteLine( $"INFO:    POST Status={post.Status} Author.Name={post.Author.Name} Author.Uri={post.Author.Uri}, Author.Type={post.Author.Type}, Created={post.TimeCreated} Published={post.TimePublished} Updated={post.TimeUpdated} Filename={post.Filename}, Categories={string.Join( ", ", post.Categories )}, Title={post.Title}" );
			Sys.Console.WriteLine( $"INFO:          Content={Helpers.Summarize( post.Content )}" );
			foreach( Comment comment in post.Comments )
				comment.Dump( 0 );
		}
	}
}
