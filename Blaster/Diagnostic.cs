namespace Blaster;

using Html = HtmlAgilityPack;
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
	public abstract string Message { get; }

	protected Diagnostic( Severity severity, FileSystem.Item sourceItem, int lineNumber, int columnNumber )
	{
		Severity = severity;
		SourceItem = sourceItem;
		LineNumber = lineNumber;
		ColumnNumber = columnNumber;
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
			string fullText = SourceItem.ReadAllText();
			string lineText = Helpers.GetLine( fullText, LineNumber );
			stringBuilder.Append( lineText );
			stringBuilder.Append( "\n" );
			stringBuilder.Append( ' ', ColumnNumber );
			stringBuilder.Append( "^" );
		}
		return stringBuilder.ToString();
	}
}

public sealed class HtmlParseDiagnostic : Diagnostic
{
	public Html.HtmlParseErrorCode HtmlParseErrorCode { get; }
	public override string Message { get; }

	public HtmlParseDiagnostic( Severity severity, FileSystem.Item sourceItem, int lineNumber, int columnNumber, Html.HtmlParseErrorCode htmlParseErrorCode, string message )
		: base( severity, sourceItem, lineNumber, columnNumber )
	{
		HtmlParseErrorCode = htmlParseErrorCode;
		Message = message;
	}
}

public sealed class CustomDiagnostic : Diagnostic
{
	public override string Message { get; }

	public CustomDiagnostic( Severity severity, FileSystem.Item sourceItem, int lineNumber, int columnNumber, string message )
		: base( severity, sourceItem, lineNumber, columnNumber )
	{
		Message = message;
	}
}
