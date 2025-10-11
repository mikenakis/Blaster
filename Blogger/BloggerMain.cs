namespace Blogger;

using static Common.Statics;
using Sys = System;
using SysIo = System.IO;

public class BloggerMain
{
	public static void Main( string[] arguments )
	{
		Assert( arguments.Length == 0 );
		string bloggerExportDirectory = SysIo.Path.GetFullPath( @"C:\Users\MBV\Personal\Downloads\takeout-20251001T185456Z-1-001\Takeout\Blogger" );
		Blog blog = BloggerExportReader.ReadBloggerExport( bloggerExportDirectory, "michael.gr" );
		if( False )
			blog.Dump();
		string blogDirectory = SysIo.Path.GetFullPath( @"C:\Users\MBV\Personal\Documents\digital-garden\obsidian\blog.michael.gr\content\post\generated" );
		Sys.Uri sourceBaseUri = new( "https://blog.michael.gr/" );
		MarkdownBlogWriter.WriteBlog( blog, blogDirectory, sourceBaseUri );
		Sys.Console.Write( "Press [Enter] to terminate: " );
		Sys.Console.ReadLine();
	}
}
