namespace Blaster;

using Framework.Codecs;
using MikeNakis.Kit.Extensions;
using MikeNakis.Kit.FileSystem;
using static MikeNakis.Kit.GlobalStatics;

sealed class Stock
{
	public readonly struct Id : Sys.IComparable<Id>, Sys.IEquatable<Id>
	{
		public static bool operator ==( Id left, Id right ) => left.Equals( right );
		public static bool operator !=( Id left, Id right ) => !left.Equals( right );

		const string markdownExtension = ".md";

		public static Id FromMarkdownFilePath( DirectoryPath repositoryDirectoryPath, FilePath markdownFilePath )
		{
			Assert( markdownFilePath.StartsWith( repositoryDirectoryPath ) );
			string relativePath = repositoryDirectoryPath.GetRelativePath( markdownFilePath );
			Assert( relativePath.EndsWith2( markdownExtension ) );
			return FromString( relativePath[..^markdownExtension.Length] );
		}

		public static Id FromString( string content )
		{
			return new Id( content );
		}

		public static readonly Codec<Id> Codec = new StringRepresentationCodec<Id>( s => new Id( s ), k => k.Content );

		public string Content { get; init; } //This is the repository-relative file path name of a markdown file, without the .md extension.

		Id( string content )
		{
			Content = content;
		}

		public int CompareTo( Id other ) => StringCompare( Content, other.Content );
		[Sys.Obsolete] public override bool Equals( object? other ) => other is Id kin && Equals( kin );
		public override int GetHashCode() => Content.GetHashCode( Sys.StringComparison.Ordinal );
		public bool Equals( Id other ) => CompareTo( other ) == 0;
		public override string ToString() => Content;

		public string Extension => getExtension();

		string getExtension()
		{
#pragma warning disable RS0030 // Do not use banned APIs
			return SysIo.Path.GetExtension( Content );
#pragma warning restore RS0030 // Do not use banned APIs
		}

		internal Id WithExtension( string extension )
		{
#pragma warning disable RS0030 // Do not use banned APIs
			string newContent = SysIo.Path.ChangeExtension( Content, extension );
#pragma warning restore RS0030 // Do not use banned APIs
			return new Id( newContent );
		}
	}

	abstract class StockItem
	{
		public Id Id { get; }
		public abstract Sys.DateTime GetTimeModified();

		protected StockItem( Id id )
		{
			Id = id;
		}
	}

	sealed class FakeStockItem : StockItem
	{
		readonly Sys.DateTime dateTime;
		public readonly string Content;

		public FakeStockItem( Id id, Sys.DateTime dateTime, string content )
			: base( id )
		{
			this.dateTime = dateTime;
			Content = content;
		}

		public override Sys.DateTime GetTimeModified()
		{
			return dateTime;
		}
	}

	public DirectoryPath Root { get; }
	readonly Dictionary<Id, FakeStockItem> fakeItems = new();

	public Stock( DirectoryPath root )
	{
		Root = root;
	}

	public void AddFakeItem( Id id, Sys.DateTime dateTime, string content )
	{
		FakeStockItem fakeStockItem = new( id, dateTime, content );
		fakeItems.Add( id, fakeStockItem );
	}

	public IEnumerable<Id> EnumerateItems()
	{
		foreach( FilePath filePath in Root.EnumerateFiles( "*", true ) )
		{
			if( filePath.GetFileNameAndExtension().StartsWith2( "." ) )
				continue;
			if( filePath.GetDirectoryPatrh().GetDirectoryName().StartsWith2( "." ) )
				continue;
			yield return Id.FromString( Root.GetRelativePath( filePath ) );
		}
		foreach( Id id in fakeItems.Keys )
			yield return id;
	}

	public void WriteSideFile( string sideFilename, string content )
	{
		FilePath sideFilePath = getSideFilePath( sideFilename );
		sideFilePath.WriteAllText( content );
	}

	public string? TryReadSideFile( string sideFilename )
	{
		FilePath sideFilePath = getSideFilePath( sideFilename );
		if( !sideFilePath.Exists() )
			return null;
		return sideFilePath.ReadAllText();
	}

	FilePath getSideFilePath( string filename )
	{
		Assert( filename.StartsWith2( "." ) );
		return Root.File( filename );
	}

	public string ReadAllText( Id id )
	{
		if( fakeItems.TryGetValue( id, out FakeStockItem? value ) )
			return value.Content;
		FilePath filePath = Root.RelativeFile( id.Content );
		return filePath.ReadAllText();
	}

	public void WriteAllText( Id id, string text )
	{
		Assert( !fakeItems.ContainsKey( id ) );
		FilePath filePath = Root.RelativeFile( id.Content );
		filePath.WriteAllText( text );
	}

	public void Copy( Id id, Stock targetStock, Id targetId )
	{
		FilePath sourceFilePath = Root.RelativeFile( id.Content );
		FilePath targetFilePath = targetStock.Root.RelativeFile( targetId.Content );
		targetFilePath.Directory.CreateIfNotExist();
		sourceFilePath.CopyTo( targetFilePath, overwrite: true );
	}
}
