namespace Blaster;

using System.Collections.Generic;
using Framework.Codecs;
using MikeNakis.Kit;
using MikeNakis.Kit.Extensions;
using static MikeNakis.Kit.GlobalStatics;
using Sys = System;
using SysIo = System.IO;

public interface IFileSystem
{
	readonly struct Path : Sys.IComparable<Path>, Sys.IEquatable<Path>
	{
		public static bool operator ==( Path left, Path right ) => left.Equals( right );
		public static bool operator !=( Path left, Path right ) => !left.Equals( right );

		public static Path Of( string content )
		{
			return new Path( content );
		}

		public static readonly Codec<Path> Codec = new StringRepresentationCodec<Path>( s => new Path( s ), k => k.Content );

		public string Content { get; init; }

		Path( string content )
		{
			Assert( !content.Contains2( '\\' ) );
			Content = content;
		}

		public int CompareTo( Path other ) => StringCompare( Content, other.Content );
		[Sys.Obsolete] public override bool Equals( object? other ) => other is Path kin && Equals( kin );
		public override int GetHashCode() => Content.GetHashCode( Sys.StringComparison.Ordinal );
		public bool Equals( Path other ) => CompareTo( other ) == 0;
		public override string ToString() => Content;

		public string Extension => getExtension();

		string getExtension()
		{
#pragma warning disable RS0030 // Do not use banned APIs
			return SysIo.Path.GetExtension( Content );
#pragma warning restore RS0030 // Do not use banned APIs
		}

		internal Path WithExtension( string extension )
		{
#pragma warning disable RS0030 // Do not use banned APIs
			string newContent = SysIo.Path.ChangeExtension( Content, extension );
#pragma warning restore RS0030 // Do not use banned APIs
			return new Path( newContent );
		}
	}

	IEnumerable<Path> EnumerateItems();
	IEnumerable<Path> EnumerateItems( Path path );
	byte[] ReadAllBytes( Path path );
	void WriteAllBytes( Path path, byte[] text );
	string ReadAllText( Path path ) => DotNetHelpers.BomlessUtf8.GetString( ReadAllBytes( path ) );
	void WriteAllText( Path path, string text ) => WriteAllBytes( path, DotNetHelpers.BomlessUtf8.GetBytes( text ) );
	void Copy( Path path, IFileSystem targetFileSystem, Path targetPath ) => targetFileSystem.WriteAllBytes( targetPath, ReadAllBytes( path ) );
	string GetDiagnosticFullPath( Path path );
}
