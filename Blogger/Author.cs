namespace Blogger;

enum AuthorType
{
	Blogger,
	Anonymous
}

sealed record class Author( string Name, string Uri, AuthorType Type );
