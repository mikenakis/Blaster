namespace Scripting;

using Sys = System;

public class ScriptsMain
{
	public static void Main( string[] arguments )
	{
		Sys.Console.WriteLine( $"INFO: Current directory: '{Sys.IO.Directory.GetCurrentDirectory()}'" );
		Sys.Console.WriteLine( $"INFO: Arguments: [{string.Join( ", ", arguments )}]" );

		//when a script is launched from within obsidian, the current directory is the directory of the script, so
		//switch to it.
		System.IO.Directory.SetCurrentDirectory( "../obsidian/Scripts" );
		Helpers.LaunchHugoServer();
	}
}
