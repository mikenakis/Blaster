namespace Blaster;

public enum Severity
{
	Info,
	Warn,
	Error
}

public class Diagnostic
{
	public string SourceFilePathName { get; }
	public int LineNumber { get; }
	public int ColumnNumber { get; }
	public Severity Severity { get; }
	public string? LineText { get; }
	public string Message { get; }

	public Diagnostic( string sourceFilePathName, int lineNumber, int columnNumber, Severity severity, string? lineText, string message )
	{
		SourceFilePathName = sourceFilePathName;
		LineNumber = lineNumber;
		ColumnNumber = columnNumber;
		Severity = severity;
		LineText = lineText;
		Message = message;
	}
}
