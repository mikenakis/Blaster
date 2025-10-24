using MikeNakis.Kit;
using MikeNakis.Kit.Extensions;
using static MikeNakis.Kit.GlobalStatics;

static class Helpers
{
	static string getParentDirectory( string directory, string suffix )
	{
		directory = normalizePath( directory );
		if( !directory.EndsWith2( suffix ) )
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
		return SysIo.Path.GetFullPath( path ).Replace2( "\\", "/" );
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
			Sys.Console.WriteLine( $"ERROR: {exception.GetType()}: {exception.ToString()}" );
			Pause();
			Sys.Environment.Exit( -1 );
			return default;
		}
	}

	public static void RunSafe( Sys.Action action )
	{
		RunSafe( () =>
		{
			action.Invoke();
			return 0;
		} );
	}

	public static void LaunchHugoServer()
	{
		//NOTE: when a script is launched from within obsidian, the current directory is the directory of the script.
		Sys.Console.WriteLine( $"INFO: Current directory: '{normalizePath( SysIo.Directory.GetCurrentDirectory() )}'" );
		SysIo.Directory.SetCurrentDirectory( ".." ); //switch up to the obsidian directory.
		Assert( SysIo.Path.GetFileName( SysIo.Directory.GetCurrentDirectory() ) == "obsidian" );
		SysIo.Directory.SetCurrentDirectory( ".." ); //switch up to the digital garden directory.
		Assert( SysIo.Path.GetFileName( SysIo.Directory.GetCurrentDirectory() ) == "digital-garden" );
		Sys.Console.WriteLine( $"INFO: Launching hugo..." );

		//exec( "hugo", @"server --buildDrafts --cleanDestinationDir --buildFuture --navigateToChanged --panicOnWarning --disableFastRender" );
		exec( "hugo", "server --buildDrafts --gc --buildFuture --navigateToChanged --disableFastRender" +
			" --cleanDestinationDir" + //NOTE: this does not delete everything, but it does delete .gitignore 
									   // " --panicOnWarning" + hugo does not offer the ability to print messages, so we have to print warnings instead, so we cannot panic on them.
			$@" --themesDir ../hugo-themes --source michael.gr-hugo-files" +
			// PEARL: these directories are relative to the configuration file, even when specified from the
			// command-line and the current directory is elsewhere.
			@" --destination ../michael.gr-hugo-publish" +
			@" --contentDir ../obsidian/blog.michael.gr/content" );

		// --minify TODO
		// --printPathWarnings    cannot use because these warnings keep randomly popping up.
		// --printUnusedTemplates cannot use because the stack theme issues such a warning.
	}

	//	string destination = "../michael.gr-hugo-publish";
	//	empty( destination );
	//static void empty( string directory )
	//{
	//	string path = SysIo.Path.GetFullPath( directory );
	//	Sys.Console.WriteLine( $"Emptying {path}" );
	//}

	static SysDiag.Process execAsync( string command, string arguments )
	{
		return SysDiag.Process.Start( command, arguments );
	}

	static int tryExec( string command, string arguments )
	{
		using( SysDiag.Process process = execAsync( command, arguments ) )
		{
			if( process == null )
				return 0;
			process.WaitForExit();
			return process.ExitCode;
		}
	}

	static void exec( string command, string arguments )
	{
		int exitCode = tryExec( command, arguments );
		if( exitCode != 0 )
			throw new ApplicationException( $"The command '{command}' returned {exitCode} (current directory: {SysIo.Directory.GetCurrentDirectory()})" );
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
