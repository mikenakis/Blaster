const websocket = new WebSocket("ws://localhost:8080/ws");

websocket.onopen = (e) =>
{
	console.log( "WebSocket connected" );
};

websocket.onclose = (e) => 
{
	console.log( "WebSocket closed" );
};

websocket.onmessage = (e) => 
{
	console.log( "WebSocket received %s", e.data );
	this.window.document.location.reload();
};

websocket.onerror = (e) => 
{
	console.log( "WebSocket error! All we know is '%s'. (Whoever is responsible for this, you are a fucking moron, go kill yourself so that your obviously defective genetic material does not pollute our collective gene pool.)", e.type );
};
