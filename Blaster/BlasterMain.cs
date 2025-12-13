namespace Blaster;

using MikeNakis.Clio.Extensions;
using MikeNakis.Kit;
using MikeNakis.Kit.FileSystem;
using Clio = MikeNakis.Clio;
using Sys = System;

public sealed class BlasterMain
{
	public static void Main( string[] arguments )
	{
		bool pause = true;
		try
		{
			Clio.ArgumentParser argumentParser = new();
			Clio.IOptionArgument<string?> contentArgument = argumentParser.AddStringOption( "content", 'c', "The content directory (contains markdown and media files)", "directory" );
			Clio.IOptionArgument<string> templateArgument = argumentParser.AddRequiredStringOption( "template", 't', "The template directory (contains html template, css files, additional media files)", "directory" );
			Clio.IOptionArgument<string> websiteArgument = argumentParser.AddRequiredStringOption( "website", 'o', "The website directory (all output will be placed here)", "directory" );
			Clio.ISwitchArgument pauseArgument = argumentParser.AddSwitch( "pause", description: "When done, wait for [Enter] to be pressed before terminating." );
			argumentParser.Parse( arguments );
			pause = pauseArgument.Value;
			DirectoryPath workingDirectory = DotNetHelpers.GetWorkingDirectoryPath();
			DirectoryPath contentDirectoryPath = contentArgument.Value == null ? workingDirectory : DirectoryPath.FromAbsoluteOrRelativePath( contentArgument.Value, workingDirectory );
			DirectoryPath templateDirectoryPath = DirectoryPath.FromAbsoluteOrRelativePath( templateArgument.Value, workingDirectory );
			DirectoryPath websiteDirectoryPath = DirectoryPath.FromAbsoluteOrRelativePath( websiteArgument.Value, workingDirectory );
			Sys.Console.WriteLine( $"INFO: Content: {contentDirectoryPath}" );
			Sys.Console.WriteLine( $"INFO: Template: {templateDirectoryPath}" );
			Sys.Console.WriteLine( $"INFO: Website: {websiteDirectoryPath}" );

			FakeClock fakeClock = new();
			HybridFileSystem contentFileSystem = new HybridFileSystem( contentDirectoryPath, fakeClock );
			contentFileSystem.AddFakeItem( FileName.Absolute( "/index.md" ), "[Pages](page/)\r\n\r\n[Posts](post/)" );
			contentFileSystem.AddFakeItem( FileName.Absolute( "/post/index.md" ), "" );
			contentFileSystem.AddFakeItem( FileName.Absolute( "/page/index.md" ), "" );
			FileSystem templateFileSystem = new HybridFileSystem( templateDirectoryPath, fakeClock );
			FileSystem websiteFileSystem = new HybridFileSystem( websiteDirectoryPath, fakeClock );

			BlasterEngine.Run( contentFileSystem, templateFileSystem, websiteFileSystem, BlasterEngine.DefaultDiagnosticConsumer );

			Sys.Console.WriteLine( "Done." );
		}
		finally
		{
			if( pause )
			{
				Sys.Console.Write( "Press [Enter] to terminate: " );
				Sys.Console.ReadLine();
			}
		}
	}
}
