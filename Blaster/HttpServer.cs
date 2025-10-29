namespace Blaster;

using MikeNakis.Kit;
using MikeNakis.Kit.Extensions;
using MikeNakis.Kit.FileSystem;
using static MikeNakis.Kit.GlobalStatics;

public class HttpServer : Sys.IDisposable
{
	const string url = "http://localhost:8000/";

	public static void Run( DirectoryPath webRoot )
	{
		using( HttpServer httpServer = new HttpServer( webRoot ) )
		{
			httpServer.Run();
		}
	}

	readonly LifeGuard lifeGuard = LifeGuard.Create();
	readonly MimeMapper mimeMapper = new();
	readonly DirectoryPath webRoot;

	public HttpServer( DirectoryPath webRoot )
	{
		this.webRoot = webRoot;
	}

	public void Dispose()
	{
		Assert( lifeGuard.IsAliveAssertion() );
		lifeGuard.Dispose();
	}

	public void Run()
	{
		using( SysNet.HttpListener listener = new SysNet.HttpListener() )
		{
			listener.Prefixes.Add( url );
			listener.Start();
			Sys.Console.WriteLine( "Listening on {0}", url );

			while( true )
			{
				SysNet.HttpListenerContext context = listener.GetContext();
				try
				{
					processTransaction( context );
				}
				catch( Sys.Exception exception )
				{
					Log.Error( "Http transaction failed: ", exception );
				}
			}
		}

		void processTransaction( SysNet.HttpListenerContext context )
		{
			int statusCode;
			string statusDescription;
			try
			{
				(statusCode, statusDescription) = process( context.Request, context.Response );
			}
			catch( Sys.Exception exception )
			{
				Log.Error( "Http transaction processing failed: ", exception );
				statusCode = 500;
				statusDescription = "Internal Server Error";
			}
			context.Response.StatusCode = statusCode;
			context.Response.StatusDescription = statusDescription;
			context.Response.Close();
		}
	}

	(int statusCode, string statusDescription) process( SysNet.HttpListenerRequest request, SysNet.HttpListenerResponse response )
	{
		Sys.Console.WriteLine( $"{request.HttpMethod} {request.Url}" );
		Assert( request.IsLocal );
		if( request.HttpMethod != "GET" )
		{
			Sys.Console.WriteLine( $"Unknown HTTP method '{request.HttpMethod}'." );
			return (405, "Method not allowed");
		}

		string localPath = getLocalPath( request.RawUrl );
		FilePath filePath = webRoot.RelativeFile( localPath );
		if( !filePath.Exists() )
		{
			Sys.Console.WriteLine( $"File not found: '{localPath}'." );
			return (404, "Not Found");
		}

		byte[] data = filePath.ReadAllBytes();
		response.ContentType = mimeMapper.GetMimeType( filePath );
		response.ContentEncoding = DotNetHelpers.BomlessUtf8;
		response.ContentLength64 = data.LongLength;
		response.OutputStream.Write( data );
		return (200, "OK");

		static string getLocalPath( string? rawUrl )
		{
			string url = rawUrl ?? "/";
			string localPath = url.EndsWith( '/' ) ? url + "index.html" : url;
			Assert( localPath.StartsWith2( "/" ) );
			return localPath[1..];
		}
	}
}
