namespace Blaster_Test;

using System.Linq;
using Blaster;
using MikeNakis.Kit;
using MikeNakis.Kit.Extensions;
using MikeNakis.Kit.FileSystem;
using Testing;
using static MikeNakis.Kit.GlobalStatics;
using SysCompiler = System.Runtime.CompilerServices;
using VSTesting = Microsoft.VisualStudio.TestTools.UnitTesting;

[VSTesting.TestClass]
public class T101_BlasterTests : TestClass
{
	static DirectoryPath getTestDirectory( [SysCompiler.CallerFilePath] string callerFilePath = "" )
	{
		return FilePath.FromAbsolutePath( callerFilePath ).Directory;
	}

	[VSTesting.TestMethod]
	public void T101_Blaster_Works()
	{
		Clock fakeClock = new FakeClock();
		//DirectoryPath testFilesDirectoryPath = getTestDirectory().Directory( "test-files" );
		IFileSystem sourceFileSystem = new FakeFileSystem( fakeClock /*, testFilesDirectoryPath.Directory( "content" )*/ );
		sourceFileSystem.WriteAllText( IFileSystem.Path.Of( "index.md" ), "This is index.md" );
		IFileSystem templateFileSystem = new FakeFileSystem( fakeClock /*, testFilesDirectoryPath.Directory( "template" )*/ );
		templateFileSystem.WriteAllText( IFileSystem.Path.Of( "template.html" ), """
<!DOCTYPE html>
<html>
	<head>
		<title>{{title}}</title>
    </head>
    <body>
		{{content}}
	</body>
</html>
""" );
		IFileSystem targetFileSystem = new FakeFileSystem( fakeClock /*, testFilesDirectoryPath.Directory( "target" )*/ );
		BlasterEngine.Run( sourceFileSystem, templateFileSystem, targetFileSystem, diagnosticMessageConsumer );
		Assert( sourceFileSystem.ReadAllText( sourceFileSystem.EnumerateItems().Single() ) == "This is index.md" );
		Assert( equals( targetFileSystem.ReadAllText( targetFileSystem.EnumerateItems().Single() ), """
<!DOCTYPE html>
<html>
	<head>
		<title>index.md</title>
    </head>
    <body>
		<p>This is index.md</p>

	</body>
</html>
""" ) );

		static void diagnosticMessageConsumer( BlasterEngine.DiagnosticMessage diagnosticMessage )
		{
			Assert( false );
		}

		static bool equals( string s1, string s2 )
		{
			return fix( s1 ) == fix( s2 );

			static string fix( string s )
			{
				s = s.Replace2( "\r\n", " " );
				s = s.Replace2( "\n", " " );
				s = s.Replace2( "\t", " " );
				s = s.Replace2( "  ", " " );
				return s;
			}
		}
	}

	[VSTesting.TestMethod]
	public void T102_Blaster_Works()
	{
		Clock fakeClock = new FakeClock();
		DirectoryPath testFilesDirectoryPath = getTestDirectory().Directory( "test-files" );
		IFileSystem sourceFileSystem = new HybridFileSystem( /*fakeClock,*/ testFilesDirectoryPath.Directory( "content" ) );
		//sourceFileSystem.WriteAllText( IFileSystem.Path.Of( "index.md" ), "[Posts](post/index.md)" );
		//sourceFileSystem.WriteAllText( IFileSystem.Path.Of( "post/index.md" ), "" );
		//sourceFileSystem.WriteAllText( IFileSystem.Path.Of( "post/post1.md" ), "This is post1" );
		IFileSystem templateFileSystem = new FakeFileSystem( fakeClock, testFilesDirectoryPath.Directory( "template" ) );
		templateFileSystem.WriteAllText( IFileSystem.Path.Of( "template.html" ), """
<!DOCTYPE html>
<html>
	<head>
		<title>{{title}}</title>
		<meta name="viewport" content="width=device-width, height=device-height, initial-scale=1.0, minimum-scale=1.0">
		<style>body{ margin:1em; color: #F0E0e0; background-color: #1e1010 }</style>
    </head>
    <body>
		{{content}}
	</body>
</html>
""" );
		IFileSystem targetFileSystem = new FakeFileSystem( fakeClock, testFilesDirectoryPath.Directory( "target" ) );

		BlasterEngine.Run( sourceFileSystem, templateFileSystem, targetFileSystem, BlasterEngine.DefaultDiagnosticMessageConsumer );
	}
}
