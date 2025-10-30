namespace Blaster;

using MikeNakis.Kit;
using MikeNakis.Kit.FileSystem;
using static MikeNakis.Kit.GlobalStatics;

public sealed class BlasterMain
{
	public static void Main( string[] arguments )
	{
		Assert( arguments.Length == 0 );
		Sys.Console.WriteLine( $"INFO: Current directory: '{DotNetHelpers.GetWorkingDirectoryPath()}'" );
		Sys.Console.WriteLine( $"INFO: Arguments: [{string.Join( ", ", arguments )}]" );
		DirectoryPath contentDirectoryPath = DirectoryPath.FromAbsolutePath( @"C:\Users\MBV\Personal\Documents\digital-garden\obsidian\blog.michael.gr\content" );
		Sys.Console.WriteLine( $"INFO: Content: {contentDirectoryPath}" );
		DirectoryPath templateDirectoryPath = DirectoryPath.FromAbsolutePath( @"C:\Users\MBV\Personal\Documents\digital-garden\michael.gr-blaster-files" );
		Sys.Console.WriteLine( $"INFO: Template: {templateDirectoryPath}" );
		DirectoryPath targetDirectoryPath = DotNetHelpers.GetTempDirectoryPath().Directory( "blaster-publish" );
		Sys.Console.WriteLine( $"INFO: Target: {targetDirectoryPath}" );

		HybridFileSystem contentFileSystem = new HybridFileSystem( contentDirectoryPath );
		contentFileSystem.AddFakeItem( IFileSystem.Path.Of( "index.md" ), DotNetClock.Instance.GetUniversalTime(), "[Pages](page/)\r\n\r\n[Posts](post/)" );
		contentFileSystem.AddFakeItem( IFileSystem.Path.Of( "post/index.md" ), DotNetClock.Instance.GetUniversalTime(), "" );
		contentFileSystem.AddFakeItem( IFileSystem.Path.Of( "page/index.md" ), DotNetClock.Instance.GetUniversalTime(), "" );
		IFileSystem templateFileSystem = new HybridFileSystem( templateDirectoryPath );
		IFileSystem targetFileSystem = new HybridFileSystem( targetDirectoryPath );

		BlasterEngine.Run( contentFileSystem, templateFileSystem, targetFileSystem, BlasterEngine.DefaultDiagnosticMessageConsumer );
		//Sys.Console.Write( "Press [Enter] to terminate: " );
		HttpServer.Run( targetDirectoryPath );
	}
}
