namespace Blaster;

using MikeNakis.Kit;

public abstract class FileItem
{
	public FileSystem FileSystem { get; }
	public abstract FileName FileName { get; }

	protected FileItem( FileSystem fileSystem )
	{
		FileSystem = fileSystem;
	}

	public abstract byte[] ReadAllBytes();
	public abstract void WriteAllBytes( byte[] text );
	public string ReadAllText() => DotNetHelpers.BomlessUtf8.GetString( ReadAllBytes() );
	public void WriteAllText( string text ) => WriteAllBytes( DotNetHelpers.BomlessUtf8.GetBytes( text ) );
	public abstract string GetDiagnosticPathName();
	public void Delete() => FileSystem.Delete( FileName );
	public void CopyFrom( FileItem sourceItem ) => WriteAllBytes( sourceItem.ReadAllBytes() );
	public abstract long FileLength { get; }
	public sealed override string ToString() => $"\"{FileName}\" {FileLength} bytes";
}
