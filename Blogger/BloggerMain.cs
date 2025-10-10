namespace Blogger;

using static Common.Statics;
using Sys = System;
using SysIo = System.IO;

public class BloggerMain
{
	public static void Main( string[] arguments )
	{
		string bloggerExportDirectory = SysIo.Path.GetFullPath( @"C:\Users\MBV\Personal\Downloads\takeout-20251001T185456Z-1-001\Takeout\Blogger" );
		Blog blog = BloggerExportReader.ReadBloggerExport( bloggerExportDirectory, "michael.gr" );
		if( False )
			blog.Dump();
		string blogDirectory = SysIo.Path.GetFullPath( @"C:\Users\MBV\Personal\Documents\digital-garden\obsidian\blog.michael.gr\content\post\generated" );
		MarkdownBlogWriter.WriteBlog( blog, blogDirectory );
		Sys.Console.Write( "Press [Enter] to terminate: " );
		Sys.Console.ReadLine();
	}
}
