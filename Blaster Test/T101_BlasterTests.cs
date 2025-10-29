namespace Blaster_Test;

using Blaster;
using MikeNakis.Kit;
using MikeNakis.Kit.FileSystem;
using Testing;
using VSTesting = Microsoft.VisualStudio.TestTools.UnitTesting;

[VSTesting.TestClass]
public class T101_BlasterTests : TestClass
{
	[VSTesting.TestMethod]
	public void T101_Blaster_Works()
	{
		Clock fakeClock = new FakeClock();
		DirectoryPath testFilesDirectoryPath = getTestDirectory().Directory( "test-files" );
		IFileSystem sourceFileSystem = new FakeFileSystem( fakeClock, testFilesDirectoryPath.Directory( "content" ) );
		sourceFileSystem.WriteAllText( IFileSystem.Path.Of( "index.md" ), "[Posts](post/index.md)" );
		sourceFileSystem.WriteAllText( IFileSystem.Path.Of( "post/index.md" ), "" );
		sourceFileSystem.WriteAllText( IFileSystem.Path.Of( "post/post1.md" ), "This is post1" );
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
		BlasterEngine.Run( sourceFileSystem, templateFileSystem, targetFileSystem );
	}

	static DirectoryPath getTestDirectory( [SysCompiler.CallerFilePath] string callerFilePath = "" )
	{
		return FilePath.FromAbsolutePath( callerFilePath ).Directory;
	}
}
