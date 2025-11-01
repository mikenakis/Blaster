namespace DevWebServer;

using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

// from Microsoft Learn
//     https://learn.microsoft.com/en-us/aspnet/core/fundamentals/websockets?view=aspnetcore-9.0
// an alternative, but very similar, implementation is here:
//     https://www.tabsoverspaces.com/233883-simple-websocket-client-and-server-application-using-dotnet

static class MsLearnWebSocketServer
{
	const bool awaitConfig = false;

	public static async void Run()
	{
		//Console.Title = "Server";
		WebApplicationBuilder builder = WebApplication.CreateBuilder();
		builder.WebHost.UseUrls( "http://localhost:8080" );
		WebApplication app = builder.Build();
		app.UseWebSockets();
		app.Use( async ( context, next ) =>
		{
			if( context.Request.Path == "/" ) // "/ws"
			{
				if( context.WebSockets.IsWebSocketRequest )
				{
					using SysNetWebSock.WebSocket webSocket = await context.WebSockets.AcceptWebSocketAsync().ConfigureAwait( awaitConfig );
					await echo( webSocket ).ConfigureAwait( awaitConfig );
				}
				else
				{
					context.Response.StatusCode = StatusCodes.Status400BadRequest;
				}
			}
			else
			{
				await next( context ).ConfigureAwait( awaitConfig );
			}
		} );
		await app.RunAsync().ConfigureAwait( awaitConfig );
	}

	static async Task echo( SysNetWebSock.WebSocket webSocket )
	{
		byte[] buffer = new byte[1024 * 4];
		SysNetWebSock.WebSocketReceiveResult receiveResult = await webSocket.ReceiveAsync(
			new Sys.ArraySegment<byte>( buffer ), CancellationToken.None ).ConfigureAwait( awaitConfig );

		while( !receiveResult.CloseStatus.HasValue )
		{
			await webSocket.SendAsync(
				new Sys.ArraySegment<byte>( buffer, 0, receiveResult.Count ),
				receiveResult.MessageType,
				receiveResult.EndOfMessage,
				CancellationToken.None ).ConfigureAwait( awaitConfig );

			receiveResult = await webSocket.ReceiveAsync(
				new Sys.ArraySegment<byte>( buffer ), CancellationToken.None ).ConfigureAwait( awaitConfig );
		}

		await webSocket.CloseAsync(
			receiveResult.CloseStatus.Value,
			receiveResult.CloseStatusDescription,
			CancellationToken.None ).ConfigureAwait( awaitConfig );
	}
}
