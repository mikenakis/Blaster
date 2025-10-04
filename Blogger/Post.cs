namespace Blogger;

using System.Collections.Immutable;
using Sys = System;

enum PostStatus
{
	Draft,
	Live,
	SoftTrashed
};

sealed record class Post( PostStatus Status, string Filename, string Title, Author Author,
	Sys.DateTime TimeCreated, Sys.DateTime TimePublished, Sys.DateTime TimeUpdated,
	string Content, ImmutableArray<string> Categories, ImmutableArray<Comment> Comments );
