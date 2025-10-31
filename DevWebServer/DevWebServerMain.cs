namespace DevWebServer;

using MikeNakis.Clio.Extensions;
using MikeNakis.Console;
using MikeNakis.Kit;
using MikeNakis.Kit.FileSystem;
using static MikeNakis.Kit.GlobalStatics;
using Clio = MikeNakis.Clio;

public sealed class DevWebServerMain
{
	static void Main( string[] arguments )
	{
		StartupProjectDirectory.Initialize();
		ConsoleHelpers.Run( false, () => run( arguments ) );
	}

	static int run( string[] arguments )
	{
		Clio.ArgumentParser argumentParser = new();
		Clio.IPositionalArgument<string> prefixArgument = argumentParser.AddStringPositionalWithDefault( "prefix", "http://localhost:8000/", "The host name and port to serve" );
		Clio.IPositionalArgument<string> webRootArgument = argumentParser.AddStringPositionalWithDefault( "web-root", ".", "The directory containing the files to serve" );
		if( !argumentParser.TryParse( arguments ) )
			return -1;
		DirectoryPath webRoot = DirectoryPath.FromAbsoluteOrRelativePath( webRootArgument.Value, DotNetHelpers.GetWorkingDirectoryPath() );
		Sys.Console.WriteLine( $"Serving '{webRoot}'" );
		Sys.Console.WriteLine( $"On '{prefixArgument.Value}'" );
		//Sys.Console.Write( "Press [Enter] to terminate: " );
		HttpServer.Run( prefixArgument.Value, webRoot );
		return 0;
	}
}
