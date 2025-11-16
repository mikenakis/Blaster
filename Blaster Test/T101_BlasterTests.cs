namespace Blaster_Test;

using System.Collections.Generic;
using System.Linq;
using Blaster;
using MikeNakis.Kit;
using MikeNakis.Kit.Extensions;
using MikeNakis.Kit.FileSystem;
using Testing;
using static MikeNakis.Kit.GlobalStatics;
using Html = HtmlAgilityPack;
using SysCompiler = System.Runtime.CompilerServices;
using VSTesting = Microsoft.VisualStudio.TestTools.UnitTesting;

[VSTesting.TestClass]
public class T101_BlasterTests : TestClass
{
	readonly Clock fakeClock = new FakeClock();

	[VSTesting.TestMethod]
	public void T101_Malformed_Html_Is_Caught()
	{
		FileSystem sourceFileSystem = new FakeFileSystem( fakeClock );
		FileSystem templateFileSystem = new FakeFileSystem( fakeClock );
		templateFileSystem.CreateItem( FileSystem.FileName.Absolute( "/template.html" ) ).WriteAllText( """
<!DOCTYPE html>
<html>
""" );
		FileSystem targetFileSystem = new FakeFileSystem( fakeClock );
		List<Diagnostic> diagnostics = new();
		BlasterEngine.Run( sourceFileSystem, templateFileSystem, targetFileSystem, diagnosticConsumer );
		Assert( diagnostics.Count == 1 );
		Assert( diagnostics[0] is HtmlParseDiagnostic );
		Assert( ((HtmlParseDiagnostic)diagnostics[0]).HtmlParseError.Code == Html.HtmlParseErrorCode.TagNotClosed );
		return;

		void diagnosticConsumer( Diagnostic diagnostic )
		{
			foreach( string line in diagnostic.ToString().Split( "\n" ).Select( s => s.TrimEnd() ) )
				Log.Debug( line );
			diagnostics.Add( diagnostic );
		}
	}

	[VSTesting.TestMethod]
	public void T102_Broken_Link_Is_Caught()
	{
		FileSystem sourceFileSystem = new FakeFileSystem( fakeClock );
		sourceFileSystem.CreateItem( FileSystem.FileName.Absolute( "/index.md" ) ).WriteAllText( "This is /index.md and this is a broken link [](nonexistent.md)" );
		FileSystem templateFileSystem = new FakeFileSystem( fakeClock );
		FileSystem targetFileSystem = new FakeFileSystem( fakeClock );
		List<Diagnostic> diagnostics = new();
		BlasterEngine.Run( sourceFileSystem, templateFileSystem, targetFileSystem, diagnosticConsumer );
		Assert( diagnostics.Count == 1 );
		Assert( diagnostics[0] is BrokenLinkDiagnostic );
		Assert( ((BrokenLinkDiagnostic)diagnostics[0]).FileName.Content == "/nonexistent.md" );
		return;

		void diagnosticConsumer( Diagnostic diagnostic )
		{
			foreach( string line in diagnostic.ToString().Split( "\n" ).Select( s => s.TrimEnd() ) )
				Log.Debug( line );
			diagnostics.Add( diagnostic );
		}
	}

	[VSTesting.TestMethod]
	public void T102_Root_Template_Works()
	{
		FileSystem sourceFileSystem = new FakeFileSystem( fakeClock );
		sourceFileSystem.CreateItem( FileSystem.FileName.Absolute( "/index.md" ) ).WriteAllText( "This is /index.md" );
		FileSystem templateFileSystem = new FakeFileSystem( fakeClock );
		templateFileSystem.CreateItem( FileSystem.FileName.Absolute( "/template.html" ) ).WriteAllText( """
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
		FileSystem targetFileSystem = new FakeFileSystem( fakeClock );
		BlasterEngine.Run( sourceFileSystem, templateFileSystem, targetFileSystem, diagnosticConsumer );
		string expectedText = """
<!DOCTYPE html>
<html>
	<head>
		<title>/index.md</title>
    </head>
    <body>
		<p>This is /index.md</p>
	</body>
</html>
""";
		string actualText = targetFileSystem.EnumerateItems().Single().ReadAllText();
		Assert( equals( actualText, expectedText ) );
		return;

		static void diagnosticConsumer( Diagnostic diagnostic )
		{
			Assert( false );
		}
	}

	static DirectoryPath getTestDirectory( [SysCompiler.CallerFilePath] string callerFilePath = "" )
	{
		return FilePath.FromAbsolutePath( callerFilePath ).Directory;
	}

	[VSTesting.TestMethod]
	public void T103_Complex_Template_Works()
	{
		DirectoryPath testFilesDirectoryPath = getTestDirectory().Directory( "test-files" );
		FileSystem sourceFileSystem = new HybridFileSystem( /*fakeClock,*/ testFilesDirectoryPath.Directory( "content" ) );
		FileSystem templateFileSystem = new HybridFileSystem( /*fakeClock,*/ testFilesDirectoryPath.Directory( "template" ) );
		FileSystem targetFileSystem = new HybridFileSystem( /*fakeClock,*/ testFilesDirectoryPath.Directory( "output" ) );
		targetFileSystem.Clear();

		BlasterEngine.Run( sourceFileSystem, templateFileSystem, targetFileSystem, diagnosticConsumer );

		static void diagnosticConsumer( Diagnostic diagnostic )
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
