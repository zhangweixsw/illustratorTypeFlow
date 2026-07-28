# IllustratorTypeFlow pipe protocol

The native plugin connects as a client to the current-user-only named pipe:

```text
\\.\pipe\IllustratorTypeFlow.v1
```

Each connection carries one UTF-8 JSON line and is then closed:

```json
{"protocol":1,"state":"CanvasTextEditing","pid":12016,"timestamp":1785170000000}
```

`state` is one of:

- `CanvasTextEditing`
- `NotEditing`
- `Unavailable`

The plugin sends immediately after a state change and repeats the cached state every 500 milliseconds. Adobe SDK calls are made only on Illustrator's UI thread; the publisher thread reads an atomic enum.

