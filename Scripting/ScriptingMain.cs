namespace Scripting;

using MikeNakis.Kit;
using Sys = System;
using SysIo = System.IO;

public sealed class ScriptsMain
{
	public static void Main( string[] arguments )
	{
		Sys.Console.WriteLine( $"INFO: Current directory: '{DotNetHelpers.GetWorkingDirectoryPath()}'" );
		Sys.Console.WriteLine( $"INFO: Arguments: [{string.Join( ", ", arguments )}]" );

		//when a script is launched from within obsidian, the current directory is the directory of the script, so
		//switch to it.
		string scriptsDirectory = @"C:\Users\MBV\Personal\Documents\digital-garden\obsidian\Scripts";
#pragma warning disable RS0030 // Do not use banned APIs
		SysIo.Directory.SetCurrentDirectory( scriptsDirectory );
#pragma warning restore RS0030 // Do not use banned APIs

		Helpers.LaunchHugoServer();
	}
}
