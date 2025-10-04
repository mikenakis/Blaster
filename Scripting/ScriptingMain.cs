namespace Scripting;

using Sys = System;

public class ScriptsMain
{
	public static void Main( string[] arguments )
	{
		Sys.Console.WriteLine( $"Hello, World! (arguments = [{string.Join( ", ", arguments )}])" );
		string currentDirectory = System.IO.Directory.GetCurrentDirectory();
		System.Console.WriteLine( $"Current directory: '{currentDirectory}'" );
		Helpers.LaunchHugoServer( "michael.gr-hugo-files" );
	}
}
