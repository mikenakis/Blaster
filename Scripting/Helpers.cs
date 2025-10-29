using MikeNakis.Kit;
using MikeNakis.Kit.FileSystem;
using static MikeNakis.Kit.GlobalStatics;

static class Helpers
{
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
		Sys.Console.WriteLine( $"INFO: Current directory: '{DotNetHelpers.GetWorkingDirectoryPath()}'" );
		DirectoryPath digitalGardenDirectoryPath = DotNetHelpers.GetWorkingDirectoryPath()?.GetParent()?.GetParent() ?? throw new AssertionFailureException();
		Assert( digitalGardenDirectoryPath.GetDirectoryName() == "digital-garden" );

		DirectoryPath destinationDirectoryPath = digitalGardenDirectoryPath.Directory( "michael.gr-hugo-publish" );

		Sys.Console.WriteLine( $"INFO: Launching hugo..." );
		exec( digitalGardenDirectoryPath, "hugo", "server --buildDrafts --gc --buildFuture --navigateToChanged --disableFastRender --cleanDestinationDir"
			// " --panicOnWarning" + hugo does not offer the ability to print messages, so we have to print warnings instead, so we cannot panic on them.
			+ $@" --themesDir C:\Users\MBV\Personal\Documents\digital-garden\hugo-themes"
			+ $@" --source C:\Users\MBV\Personal\Documents\digital-garden\obsidian\blog.michael.gr\hugo-files"
			// PEARL: these directories are relative to the configuration file, even when specified from the
			// command-line and the current directory is elsewhere.
			+ $" --destination \"{destinationDirectoryPath}\""
			+ @" --contentDir C:\Users\MBV\Personal\Documents\digital-garden\obsidian\blog.michael.gr\content" );
		// --minify TODO
		// --printPathWarnings    cannot use because these warnings keep randomly popping up.
		// --printUnusedTemplates cannot use because the stack theme issues such a warning.
	}

	static int tryExec( DirectoryPath workingDirectoryPath, string command, string arguments )
	{
		SysDiag.ProcessStartInfo startInfo = new SysDiag.ProcessStartInfo( command, arguments );
		startInfo.WorkingDirectory = workingDirectoryPath.Path;
		using( SysDiag.Process process = new SysDiag.Process() )
		{
			process.StartInfo = startInfo;
			if( !process.Start() )
				throw new SysIo.IOException();
			process.WaitForExit();
			return process.ExitCode;
		}
	}

	static void exec( DirectoryPath workingDirectoryPath, string command, string arguments )
	{
		int exitCode = tryExec( workingDirectoryPath, command, arguments );
		if( exitCode != 0 )
			throw new ApplicationException( $"The command '{command}' returned {exitCode} in directory '{workingDirectoryPath}'" );
	}

	public static void Pause()
	{
		Sys.Console.Write( "Press [Enter] to terminate: " );
		Sys.Console.ReadLine();
	}
}
