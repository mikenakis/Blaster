namespace Blaster;

using SysText = System.Text;

public enum Severity
{
	Info,
	Warn,
	Error
}

public abstract class Diagnostic
{
	public Severity Severity { get; }
	public FileSystem.Item SourceItem { get; }
	public int LineNumber { get; }
	public int ColumnNumber { get; }
	public int Length { get; }
	public abstract string Message { get; }

	protected Diagnostic( Severity severity, FileSystem.Item sourceItem, int lineNumber, int columnNumber, int length )
	{
		Severity = severity;
		SourceItem = sourceItem;
		LineNumber = lineNumber;
		ColumnNumber = columnNumber;
		Length = length;
	}

	public override string ToString()
	{
		SysText.StringBuilder stringBuilder = new();
		string sourceFilePathName = SourceItem.GetDiagnosticPathName();
		stringBuilder.Append( sourceFilePathName ).Append( "(" ).Append( LineNumber );
		if( ColumnNumber > 0 )
			stringBuilder.Append( "," ).Append( ColumnNumber );
		stringBuilder.Append( "): " ).Append( Message );
		if( LineNumber > 0 )
		{
			stringBuilder.Append( "\n" );
			string lineText = Helpers.GetLine( SourceItem, LineNumber );
			stringBuilder.Append( lineText );
			stringBuilder.Append( "\n" );
			stringBuilder.Append( ' ', ColumnNumber );
			stringBuilder.Append( '^', Length );
		}
		return stringBuilder.ToString();
	}
}

public sealed class HtmlParseDiagnostic : Diagnostic
{
	readonly string message;
	public override string Message => message;

	public HtmlParseDiagnostic( FileSystem.Item sourceItem, int lineNumber, int columnNumber, int length, string message )
		: base( Severity.Error, sourceItem, lineNumber, columnNumber, length )
	{
		this.message = message;
	}
}

public sealed class BrokenLinkDiagnostic : Diagnostic
{
	public FileSystem.FileName FileName { get; }
	public override string Message => $"Broken link: {FileName}";

	public BrokenLinkDiagnostic( FileSystem.Item sourceItem, int lineNumber, int columnNumber, int length, FileSystem.FileName fileName )
		: base( Severity.Error, sourceItem, lineNumber, columnNumber, length )
	{
		FileName = fileName;
	}
}

public sealed class CustomDiagnostic : Diagnostic
{
	public override string Message { get; }

	public CustomDiagnostic( Severity severity, FileSystem.Item sourceItem, int lineNumber, int columnNumber, int length, string message )
		: base( severity, sourceItem, lineNumber, length, columnNumber )
	{
		Message = message;
	}
}
