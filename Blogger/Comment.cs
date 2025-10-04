namespace Blogger;

using System.Collections.Immutable;
using Sys = System;

enum CommentStatus
{
	Live,
	Spam,
	Ghosted
};

sealed record class Comment( CommentStatus Status, Author Author,
	Sys.DateTime TimeCreated, Sys.DateTime TimePublished, Sys.DateTime TimeUpdated,
	string Content, ImmutableArray<Comment> Replies )
{
	public void Dump( int depth )
	{
		Sys.Console.WriteLine( $"{Helpers.Indentation( depth )}         COMMENT Status={Status} Author.Name={Author.Name} Author.Uri={Author.Uri}, Author.Type={Author.Type}, Created={TimeCreated} Published={TimePublished} Updated={TimeUpdated} " );
		Sys.Console.WriteLine( $"{Helpers.Indentation( depth )}             Content={Helpers.Summarize( Content )}" );
		foreach( Comment replyComment in Replies )
			replyComment.Dump( depth + 1 );
	}
}
