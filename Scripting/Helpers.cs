using Sys = System;
using SysIo = System.IO;

static class Helpers
{
	static string getParentDirectory( string directory, string suffix )
	{
		directory = normalizePath( directory );
		if( !directory.EndsWith( suffix ) )
			throw new Sys.Exception( $"Directory '{directory}' does not end with '{suffix}'" );
		return combine( directory, ".." );
	}

	static string getObsidianDirectory()
	{
		return getParentDirectory( normalizePath( SysIo.Directory.GetCurrentDirectory() ), "digital-garden/obsidian/Scripts" );
	}

	static string getDigitalGardenDirectory()
	{
		return getParentDirectory( getObsidianDirectory(), "digital-garden/obsidian" );
	}

	static string getFullPathFromDigitalGardenRelativePath( string relativePath )
	{
		return combine( getDigitalGardenDirectory(), relativePath );
	}

	static string normalizePath( string path )
	{
		return SysIo.Path.GetFullPath( path ).Replace( "\\", "/" );
	}

	static string combine( string rootedPath, string relativePath )
	{
		if( !SysIo.Path.IsPathRooted( rootedPath ) )
			throw new Sys.Exception( $"Path '{rootedPath}' is relative" );
		if( SysIo.Path.IsPathRooted( relativePath ) )
			throw new Sys.Exception( $"Path '{relativePath}' is not relative" );
		return normalizePath( SysIo.Path.Combine( rootedPath, relativePath ) );
	}

	public static T RunSafe<T>( Sys.Func<T> func )
	{
		try
		{
			return func.Invoke();
		}
		catch( Sys.Exception exception )
		{
			Sys.Console.WriteLine( exception.ToString() );
			Pause();
			Sys.Environment.Exit( -1 );
			return default;
		}
	}

	public static void RunSafe( Sys.Action action )
	{
		RunSafe<int>( () =>
		{
			action.Invoke();
			return 0;
		} );
	}

	public static void LaunchHugoServer( string theme )
	{
		//NOTE: when a script is launched from within obsidian, the current directory is the directory of the script.
		Sys.Console.WriteLine( $"Current directory: '{normalizePath( SysIo.Directory.GetCurrentDirectory() )}'" );
		SysIo.Directory.SetCurrentDirectory( "../blog.michael.gr" ); //switch up to the obsidian directory, then down into the blog directory.
		Sys.Console.WriteLine( $"Current directory: '{normalizePath( SysIo.Directory.GetCurrentDirectory() )}'" );
		Sys.Console.WriteLine( $"Launching hugo..." );
		Sys.Console.WriteLine();

		//exec( "hugo", @"server --buildDrafts --cleanDestinationDir --buildFuture --navigateToChanged --panicOnWarning --disableFastRender" );
		exec( "hugo", "server --buildDrafts --cleanDestinationDir --buildFuture --navigateToChanged --panicOnWarning --disableFastRender" +
			$@" --themesDir ..\..\..\hugo-themes --destination ..\..\..\michael.gr-hugo-publish --theme {theme} --source using-{theme}" +
			// PEARL: the content directory is relative to the configuration file, even if the content directory is specified from the command-line.
			@" --contentDir ..\content" );
		// --minify TODO
		// --printPathWarnings    cannot use because these warnings keep randomly popping up.
		// --printUnusedTemplates cannot use because the stack theme issues such a warning.
	}

	static int tryExec( string command, string arguments )
	{
		Sys.Diagnostics.Process process = Sys.Diagnostics.Process.Start( command, arguments );
		if( process == null )
			return 0;
		process.WaitForExit();
		return process.ExitCode;
	}

	static void exec( string command, string arguments )
	{
		int exitCode = tryExec( command, arguments );
		if( exitCode != 0 )
			throw new Sys.Exception( $"The command '{command}' returned {exitCode} (current directory: {SysIo.Directory.GetCurrentDirectory()})" );
	}

	public static void Pause()
	{
		Sys.Console.Write( "Press [Enter] to terminate: " );
		Sys.Console.ReadLine();
	}

	public static void GitCommitAndPush( string digitalGardenRelativeRepositoryDirectory )
	{
		string repositoryDirectory = getFullPathFromDigitalGardenRelativePath( digitalGardenRelativeRepositoryDirectory );
		SysIo.Directory.SetCurrentDirectory( repositoryDirectory );
		exec( "git", "add ." );
		tryExec( "git", "commit -q -m \"single-click-commit\"" );
		tryExec( "git", "push -q" );
	}
}