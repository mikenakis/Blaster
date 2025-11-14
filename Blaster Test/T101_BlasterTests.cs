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
		IFileSystem sourceFileSystem = new FakeFileSystem( fakeClock );
		sourceFileSystem.WriteAllText( IFileSystem.Path.Of( "index.md" ), "This is index.md" );
		Assert( sourceFileSystem.ReadAllText( sourceFileSystem.EnumerateItems().Single() ) == "This is index.md" );
		IFileSystem templateFileSystem = new FakeFileSystem( fakeClock );
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
		IFileSystem targetFileSystem = new FakeFileSystem( fakeClock );
		BlasterEngine.Run( sourceFileSystem, templateFileSystem, targetFileSystem, diagnosticMessageConsumer );
		string expectedText = """
<!DOCTYPE html>
<html>
	<head>
		<title>index.md</title>
    </head>
    <body>
		<p>This is index.md</p>
	</body>
</html>
""";
		string actualText = targetFileSystem.ReadAllText( targetFileSystem.EnumerateItems().Single() );
		Assert( equals( actualText, expectedText ) );
		return;

		static void diagnosticMessageConsumer( BlasterEngine.DiagnosticMessage diagnosticMessage )
		{
			Assert( false );
		}
	}

	[VSTesting.TestMethod]
	public void T102_Blaster_Works()
	{
		Clock fakeClock = new FakeClock();
		DirectoryPath testFilesDirectoryPath = getTestDirectory().Directory( "test-files" );
		IFileSystem sourceFileSystem = new HybridFileSystem( /*fakeClock,*/ testFilesDirectoryPath.Directory( "content" ) );
		IFileSystem templateFileSystem = new FakeFileSystem( fakeClock, testFilesDirectoryPath.Directory( "template" ) );
		IFileSystem targetFileSystem = new FakeFileSystem( fakeClock, testFilesDirectoryPath.Directory( "target" ) );

		BlasterEngine.Run( sourceFileSystem, templateFileSystem, targetFileSystem, diagnosticMessageConsumer );

		static void diagnosticMessageConsumer( BlasterEngine.DiagnosticMessage diagnosticMessage )
		{
			Assert( false );
		}
	}

	static bool equals( string s1, string s2 )
	{
		string f1 = fix( s1 );
		string f2 = fix( s2 );
		Assert( f1 == f2 );
		return true;

		static string fix( string s )
		{
			s = s.Replace2( "\r\n", " " );
			s = s.Replace2( "\n", " " );
			s = s.Replace2( "\t", " " );
			while( true )
			{
				string s2 = s.Replace2( "  ", " " );
				if( s2 == s )
					break;
				s = s2;
			}
			return s;
		}
	}
}
