namespace Blaster;

using MikeNakis.Kit;

// From https://developer.mozilla.org/en-US/docs/Web/API/WebSockets_API/Writing_WebSocket_server
// The ultimate purpose of this is to do live-reload.
// There exists this monstrosity: https://github.com/gohugoio/hugo/blob/master/livereload/livereload.js
// But from that entire insane rigamarole, the only _actually_ useful line is this: this.window.document.location.reload();
static class WebSocketServer
{
	public static void Run()
	{
		string ip = "127.0.0.1";
		int port = 80;
		var server = new SysSockets.TcpListener( SysNet.IPAddress.Parse( ip ), port );

		server.Start();
		Sys.Console.WriteLine( "Server has started on {0}:{1}, Waiting for a connection…", ip, port );

		SysSockets.TcpClient client = server.AcceptTcpClient();
		Sys.Console.WriteLine( "A client connected." );

		SysSockets.NetworkStream stream = client.GetStream();

		// enter to an infinite cycle to be able to handle every change in stream
		while( true )
		{
			while( !stream.DataAvailable )
				;
			while( client.Available < 3 )
				; // match against "get"

			byte[] bytes = new byte[client.Available];
			stream.ReadExactly( bytes, 0, bytes.Length );
			string s = DotNetHelpers.BomlessUtf8.GetString( bytes );

			if( RegEx.Regex.IsMatch( s, "^GET", RegEx.RegexOptions.IgnoreCase ) )
			{
				Sys.Console.WriteLine( "=====Handshaking from client=====\n{0}", s );

				// 1. Obtain the value of the "Sec-WebSocket-Key" request header without any leading or trailing whitespace
				// 2. Concatenate it with "258EAFA5-E914-47DA-95CA-C5AB0DC85B11" (a special GUID specified by RFC 6455)
				// 3. Compute SHA-1 and Base64 hash of the new value
				// 4. Write the hash back as the value of "Sec-WebSocket-Accept" response header in an HTTP response
				string swk = RegEx.Regex.Match( s, "Sec-WebSocket-Key: (.*)" ).Groups[1].Value.Trim();
				string swkAndSalt = swk + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";
#pragma warning disable CA5350 // Do Not Use Weak Cryptographic Algorithms
				byte[] swkAndSaltSha1 = SysCrypto.SHA1.HashData( DotNetHelpers.BomlessUtf8.GetBytes( swkAndSalt ) );
#pragma warning restore CA5350 // Do Not Use Weak Cryptographic Algorithms
				string swkAndSaltSha1Base64 = Sys.Convert.ToBase64String( swkAndSaltSha1 );

				// HTTP/1.1 defines the sequence CR LF as the end-of-line marker
				byte[] response = DotNetHelpers.BomlessUtf8.GetBytes(
					"HTTP/1.1 101 Switching Protocols\r\n" +
					"Connection: Upgrade\r\n" +
					"Upgrade: websocket\r\n" +
					"Sec-WebSocket-Accept: " + swkAndSaltSha1Base64 + "\r\n\r\n" );

				stream.Write( response, 0, response.Length );
			}
			else
			{
				//bool fin = (bytes[0] & 0b10000000) != 0;
				bool mask = (bytes[1] & 0b10000000) != 0; // must be true, "All messages from the client to the server have this bit set"
														  //int opcode = bytes[0] & 0b00001111; // expecting 1 - text message
				ulong offset = 2,
					  msgLen = bytes[1] & (ulong)0b01111111;

				if( msgLen == 126 )
				{
					// bytes are reversed because websocket will print them in Big-Endian, whereas
					// BitConverter will want them arranged in little-endian on windows
					msgLen = Sys.BitConverter.ToUInt16( new byte[] { bytes[3], bytes[2] }, 0 );
					offset = 4;
				}
				else if( msgLen == 127 )
				{
					// To test the below code, we need to manually buffer larger messages — since the NIC's autobuffering
					// may be too latency-friendly for this code to run (that is, we may have only some of the bytes in this
					// websocket frame available through client.Available).
					msgLen = Sys.BitConverter.ToUInt64( new byte[] { bytes[9], bytes[8], bytes[7], bytes[6], bytes[5], bytes[4], bytes[3], bytes[2] }, 0 );
					offset = 10;
				}

				if( msgLen == 0 )
				{
					Sys.Console.WriteLine( "msgLen == 0" );
				}
				else if( mask )
				{
					byte[] decoded = new byte[msgLen];
					byte[] masks = new byte[4] { bytes[offset], bytes[offset + 1], bytes[offset + 2], bytes[offset + 3] };
					offset += 4;

					for( ulong i = 0; i < msgLen; ++i )
						decoded[i] = (byte)(bytes[offset + i] ^ masks[i % 4]);

					string text = DotNetHelpers.BomlessUtf8.GetString( decoded );
					Sys.Console.WriteLine( "{0}", text );
				}
				else
					Sys.Console.WriteLine( "mask bit not set" );

				Sys.Console.WriteLine();
			}
		}
	}
}
