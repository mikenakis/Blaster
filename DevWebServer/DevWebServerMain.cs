namespace DevWebServer;

using System.Collections.Immutable;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using MikeNakis.Clio.Extensions;
using MikeNakis.Kit;
using MikeNakis.Kit.FileSystem;
using static MikeNakis.Kit.GlobalStatics;
using Clio = MikeNakis.Clio;

public sealed class DevWebServerMain
{
	static void Main( string[] arguments )
	{
		Clio.ArgumentParser argumentParser = new();
		Clio.IPositionalArgument<string> prefixArgument = argumentParser.AddStringPositionalWithDefault( "prefix", "http://localhost:8000/", "The host name and port to serve" );
		Clio.IPositionalArgument<string> webRootArgument = argumentParser.AddStringPositionalWithDefault( "web-root", ".", "The directory containing the files to serve" );
		if( !argumentParser.TryParse( arguments ) )
		{
			Sys.Environment.Exit( -1 );
			return;
		}
		DirectoryPath webRoot = DirectoryPath.FromAbsoluteOrRelativePath( webRootArgument.Value, DotNetHelpers.GetWorkingDirectoryPath() );
		Sys.Console.WriteLine( $"Serving '{webRoot}'" );
		Sys.Console.WriteLine( $"On '{prefixArgument.Value}'" );
		WaitableEvent waitableEvent = new();
		using Hysterizer hysterizer = new( Sys.TimeSpan.FromSeconds( 0.5 ), () =>
		{
			Sys.Console.WriteLine( "Change detected." );
			waitableEvent.Trigger();
		} );
		startFileSystemWatcher( webRoot, hysterizer.Action );
		startWebServer( webRoot, waitableEvent );
		Sys.Console.WriteLine( "Press [Enter] to terminate: " );
		Sys.Console.ReadLine();
	}

	sealed class WaitableEvent
	{
		public Sys.Action Trigger => trigger;
		TaskCompletionSource taskCompletionSource;

		public WaitableEvent()
		{
			taskCompletionSource = new();
		}

		public async Task Wait()
		{
			await taskCompletionSource.Task;
		}

		void trigger()
		{
			TaskCompletionSource temp = taskCompletionSource;
			taskCompletionSource = new();
			temp.SetResult();
		}
	}

	static SysIo.FileSystemWatcher startFileSystemWatcher( DirectoryPath directoryPath, Sys.Action waitableTaskTrigegr )
	{
		SysIo.FileSystemWatcher fileSystemWatcher = new();
		fileSystemWatcher.Path = directoryPath.Path;
		fileSystemWatcher.IncludeSubdirectories = true;
		//fileSystemWatcher.Filter                = "*";
		fileSystemWatcher.NotifyFilter = SysIo.NotifyFilters.FileName | //SysIo.NotifyFilters.Attributes | SysIo.NotifyFilters.LastAccess |
				SysIo.NotifyFilters.DirectoryName | //
				SysIo.NotifyFilters.Size |
				SysIo.NotifyFilters.LastWrite |
				SysIo.NotifyFilters.CreationTime |
				SysIo.NotifyFilters.Security;
		fileSystemWatcher.Changed += onFileSystemWatcherNormalEvent;
		fileSystemWatcher.Created += onFileSystemWatcherNormalEvent;
		fileSystemWatcher.Deleted += onFileSystemWatcherNormalEvent;
		fileSystemWatcher.Error += onFileSystemWatcherErrorEvent;
		fileSystemWatcher.Renamed += onFileSystemWatcherNormalEvent;
		fileSystemWatcher.EnableRaisingEvents = true;
		return fileSystemWatcher;

		void onFileSystemWatcherNormalEvent( object sender, SysIo.FileSystemEventArgs e )
		{
			Assert( sender == fileSystemWatcher );
			//Log.Debug( $"{e.ChangeType} {e.FullPath}" );
			waitableTaskTrigegr.Invoke();
		}

		void onFileSystemWatcherErrorEvent( object sender, SysIo.ErrorEventArgs e )
		{
			Assert( sender == fileSystemWatcher );
			Log.Warn( $"{directoryPath}", e.GetException() );
		}
	}

	sealed class Hysterizer : Sys.IDisposable
	{
		readonly LifeGuard lifeGuard = LifeGuard.Create();
		readonly Sys.TimeSpan delay;
		readonly Sys.Action action;
		volatile bool pending;

		public Hysterizer( Sys.TimeSpan delay, Sys.Action action )
		{
			this.delay = delay;
			this.action = action;
		}

		public void Dispose()
		{
			Assert( lifeGuard.IsAliveAssertion() );
			lifeGuard.Dispose();
		}

		public void Action()
		{
			if( pending )
				return;
			pending = true;
			Task.Run( async () =>
			{
				await Task.Delay( delay );
				pending = false;
				action.Invoke();
			} );
		}
	}

	static void startWebServer( DirectoryPath webRoot, WaitableEvent waitableEvent )
	{
		// from Microsoft Learn
		//     https://learn.microsoft.com/en-us/aspnet/core/fundamentals/websockets?view=aspnetcore-9.0
		// an alternative, but very similar, implementation is here:
		//     https://www.tabsoverspaces.com/233883-simple-websocket-client-and-server-application-using-dotnet
		WebApplicationBuilder builder = WebApplication.CreateBuilder( new WebApplicationOptions() { WebRootPath = webRoot.Path } );
		builder.WebHost.UseUrls( "http://localhost:8080" );
		builder.Logging.ClearProviders();
		builder.Logging.AddDebug();
		WebApplication app = builder.Build();
		app.UseWebSockets();
		app.UseHostFiltering();
		app.UseDefaultFiles( new DefaultFilesOptions() { DefaultFileNames = ImmutableArray.Create( "index.html" ) } );
		app.UseStaticFiles( new StaticFileOptions() { ServeUnknownFileTypes = true } );
		app.Use( async ( context, next ) =>
		{
			if( context.Request.Path == "/live-reload-websocket" )
			{
				await doWebSocket( context, waitableEvent );
				return;
			}
			if( context.Request.Path == "/live-reload.js" )
			{
				await serveLiveReloadJs( context );
				return;
			}
			await next( context );
		} );
		app.RunAsync();
	}

	static async Task doWebSocket( HttpContext context, WaitableEvent waitableEvent )
	{
		if( !context.WebSockets.IsWebSocketRequest )
		{
			context.Response.StatusCode = StatusCodes.Status400BadRequest;
			return;
		}
		using( SysNetWebSock.WebSocket webSocket = await context.WebSockets.AcceptWebSocketAsync() )
		{
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
			Task.Run( async () =>
			{
				byte[] buffer = new byte[16];
				while( true )
				{
					SysNetWebSock.WebSocketReceiveResult result = await webSocket.ReceiveAsync( buffer, SysThread.CancellationToken.None );
					if( result.MessageType == SysNetWebSock.WebSocketMessageType.Close )
					{
						Log.Debug( $"Socket closed: {result.CloseStatus} {result.CloseStatusDescription}" );
						break;
					}
				}
			} );
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
			while( true )
			{
				await waitableEvent.Wait();
				if( webSocket.State != SysNetWebSock.WebSocketState.Open )
					break;
				Log.Debug( "Sending refresh message..." );
				byte[] bytes = SysText.Encoding.ASCII.GetBytes( "refresh" );
				await webSocket.SendAsync( bytes, SysNetWebSock.WebSocketMessageType.Text, true, SysThread.CancellationToken.None );
			}
		}
	}

	static async Task serveLiveReloadJs( HttpContext context )
	{
		FilePath liveReloadJavascriptFilePath = DotNetHelpers.GetMainModuleDirectoryPath().File( "live-reload.js" );
		context.Response.ContentType = "text/javascript";
		await context.Response.SendFileAsync( liveReloadJavascriptFilePath.Path );
	}
}
