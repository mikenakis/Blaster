# WebSocketTest

## An alternative way to do a WebSocket server.

The ultimate purpose of this is to do live-reload.
There exists this monstrosity: https://github.com/gohugoio/hugo/blob/master/livereload/livereload.js
But from that entire insane rigamarole, the only _actually_ useful but is this:

```JavaScript
this.window.document.location.reload();
```
