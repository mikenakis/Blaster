namespace Blaster;

using MikeNakis.Kit.FileSystem;

sealed class MimeMapper
{
	readonly Dictionary<string, string> mappings = new();

	public MimeMapper()
	{
		// from https://github.com/Microsoft/referencesource/blob/main/System.Web/MimeMapping.cs
		addMapping( ".avi", "video/x-msvideo" );
		addMapping( ".bmp", "image/bmp" );
		addMapping( ".css", "text/css" );
		addMapping( ".eml", "message/rfc822" );
		addMapping( ".gif", "image/gif" );
		addMapping( ".gz", "application/x-gzip" );
		addMapping( ".htm", "text/html" );
		addMapping( ".html", "text/html" );
		addMapping( ".ico", "image/x-icon" );
		addMapping( ".jfif", "image/pjpeg" );
		addMapping( ".jpe", "image/jpeg" );
		addMapping( ".jpeg", "image/jpeg" );
		addMapping( ".jpg", "image/jpeg" );
		addMapping( ".js", "application/x-javascript" );
		addMapping( ".mid", "audio/mid" );
		addMapping( ".midi", "audio/mid" );
		addMapping( ".mov", "video/quicktime" );
		addMapping( ".movie", "video/x-sgi-movie" );
		addMapping( ".mp2", "video/mpeg" );
		addMapping( ".mp3", "audio/mpeg" );
		addMapping( ".mpa", "video/mpeg" );
		addMapping( ".mpe", "video/mpeg" );
		addMapping( ".mpeg", "video/mpeg" );
		addMapping( ".mpg", "video/mpeg" );
		addMapping( ".msi", "application/octet-stream" );
		addMapping( ".pdf", "application/pdf" );
		addMapping( ".png", "image/png" );
		addMapping( ".qt", "video/quicktime" );
		addMapping( ".rar", "application/octet-stream" );
		addMapping( ".rtf", "application/rtf" );
		addMapping( ".svg", "image/svg+xml" );
		addMapping( ".tar", "application/x-tar" );
		addMapping( ".tgz", "application/x-compressed" );
		addMapping( ".tif", "image/tiff" );
		addMapping( ".tiff", "image/tiff" );
		addMapping( ".txt", "text/plain" );
		addMapping( ".wav", "audio/wav" );
		addMapping( ".xml", "text/xml" );
		addMapping( ".zip", "application/x-zip-compressed" );
	}

	void addMapping( string extension, string mimeType )
	{
		mappings.Add( extension, mimeType );
	}

	public string GetMimeType( FilePath filePath )
	{
		if( mappings.TryGetValue( filePath.Extension, out string? mimeType ) )
			return mimeType;
		return "application/octet-stream";
	}
}
