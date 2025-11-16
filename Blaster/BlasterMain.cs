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
			Clio.IOptionArgument<string?> contentArgument = argumentParser.AddStringOption( "content", 'c', "The content directory", "directory" );
			Clio.IOptionArgument<string> templateArgument = argumentParser.AddRequiredStringOption( "template", 't', "The template directory", "directory" );
			Clio.IOptionArgument<string> outputArgument = argumentParser.AddRequiredStringOption( "output", 'o', "The output directory", "directory" );
			Clio.ISwitchArgument pauseArgument = argumentParser.AddSwitch( "pause", description: "When done, wait for [Enter] to be pressed before terminating." );
			argumentParser.Parse( arguments );
			pause = pauseArgument.Value;
			DirectoryPath workingDirectory = DotNetHelpers.GetWorkingDirectoryPath();
			DirectoryPath contentDirectoryPath = contentArgument.Value == null ? workingDirectory : DirectoryPath.FromAbsoluteOrRelativePath( contentArgument.Value, workingDirectory );
			DirectoryPath templateDirectoryPath = DirectoryPath.FromAbsoluteOrRelativePath( templateArgument.Value, workingDirectory );
			DirectoryPath outputDirectoryPath = DirectoryPath.FromAbsoluteOrRelativePath( outputArgument.Value, workingDirectory );
			Sys.Console.WriteLine( $"INFO: Content: {contentDirectoryPath}" );
			Sys.Console.WriteLine( $"INFO: Template: {templateDirectoryPath}" );
			Sys.Console.WriteLine( $"INFO: Output: {outputDirectoryPath}" );

			HybridFileSystem contentFileSystem = new HybridFileSystem( contentDirectoryPath );
			contentFileSystem.AddFakeItem( FileSystem.FileName.Absolute( "index.md" ), DotNetClock.Instance.GetUniversalTime(), "[Pages](page/)\r\n\r\n[Posts](post/)" );
			contentFileSystem.AddFakeItem( FileSystem.FileName.Absolute( "post/index.md" ), DotNetClock.Instance.GetUniversalTime(), "" );
			contentFileSystem.AddFakeItem( FileSystem.FileName.Absolute( "page/index.md" ), DotNetClock.Instance.GetUniversalTime(), "" );
			FileSystem templateFileSystem = new HybridFileSystem( templateDirectoryPath );
			FileSystem outputFileSystem = new HybridFileSystem( outputDirectoryPath );

			BlasterEngine.Run( contentFileSystem, templateFileSystem, outputFileSystem, BlasterEngine.DefaultDiagnosticConsumer );

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
