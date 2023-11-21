using System.Net.WebSockets;
using TestHttpClientClient.Controllers;
using TestLibary;

namespace TestHttpClientClient
{
    public class ClientWebSocketEx
    {
        public static bool[,]? Finished;

        public static int FinishedCount;
        public static int TotalCount;
        public static Semaphore Semaphore = new Semaphore(0, 1);
        private static int _curId;
        private readonly ILogger _iLogger;

        public bool Initing { get; private set; }

        public WebSocketEx? WebSocketEx { get; private set; }

        public ClientWebSocketEx(ILogger iLogger)
        {
            _iLogger = iLogger;
        }

        public async Task ReInit()
        {
            if (Initing)
            {
                return;
            }
            Initing = true;

            while (true)
            {
                try
                {
                    WebSocketEx?.WebSocket?.Dispose();

                    var tmpId = Interlocked.Increment(ref _curId);
                    var ws = new ClientWebSocket();
                    var uri = new Uri($"wss://{HomeController.TargetHost}/websocket?id={tmpId}");
                    await ws.ConnectAsync(uri, CancellationToken.None);
                    WebSocketEx = new WebSocketEx(ws, tmpId, _iLogger);
                    _ = Task.Run(() => ReceiveMsg(WebSocketEx, _iLogger));
                }
                catch (Exception ex)
                {
                    _iLogger.LogCritical($"ReinitWebSocketClient Exception: {ex}");
                    continue;
                }
                break;
            }

            Initing = false;
        }

        private async Task ReceiveMsg(WebSocketEx webSocket, ILogger iLogger)
        {
            while (webSocket.WebSocket.State == WebSocketState.Open)
            {
                var msg = await webSocket.ReadOneMsg();
                if (msg == null)
                {
                    iLogger.LogCritical($"ReceiveMsg MsgIsNull Exception");
                    return;
                }
                if (Finished == null)
                {
                    iLogger.LogCritical($"ReceiveMsg FinishedIsNull Exception");
                    return;
                }
                if (!Finished[msg.ClientIndex, msg.MsgIndex])
                {
                    Finished[msg.ClientIndex, msg.MsgIndex] = true;
                    var tmp = Interlocked.Increment(ref FinishedCount);
                    if (tmp == TotalCount)
                    {
                        Semaphore.Release();
                    }
                }
            }
        }
    }

    public class ASEWebSocket
    {
        public int Inited { get; private set; }
        private readonly ILogger _iLogger;

        private ClientWebSocketEx[] _clients = new ClientWebSocketEx[50];
        private long _curIndex;
        public ASEWebSocket(ILogger<ASEWebSocket> iLogger)
        {
            _iLogger = iLogger;
        }

        private async Task<ClientWebSocketEx> GetWebSocketClient()
        {
            while (true)
            {
                var tmp = Interlocked.Increment(ref _curIndex);
                var wsc = _clients[tmp % _clients.Length];
                if (!wsc.Initing)
                {
                    return wsc;
                }
                await Task.Delay(200).ConfigureAwait(false);
            }
        }

        private void RemoveWebSocketClient(ClientWebSocketEx webSocketClient)
        {
            _ = webSocketClient.ReInit();
        }

        public async Task InitAll()
        {
            if (Inited != 0)
            {
                return;
            }
            Inited = 1;
            var tasks = new List<Task>();
            for (int i = 0; i < _clients.Length; i++)
            {
                _clients[i] = new ClientWebSocketEx(_iLogger);
                tasks.Add(_clients[i].ReInit());
            }
            await Task.WhenAll(tasks);
            Inited = 2;
        }

        public async Task StartAsync(int clientCount, int msgCount, int delay)
        {
            ClientWebSocketEx.Finished = new bool[clientCount, msgCount];
            ClientWebSocketEx.TotalCount = clientCount * msgCount;
            ClientWebSocketEx.FinishedCount = 0;
            for (int clientIndex = 0; clientIndex < clientCount; clientIndex++)
            {
                for (int msgIndex = 0; msgIndex < msgCount; msgIndex++)
                {
                    var wsEx = await GetWebSocketClient();
                    if (wsEx.WebSocketEx == null)
                    {
                        RemoveWebSocketClient(wsEx);
                        return;
                    }
                    try
                    {
                        wsEx.WebSocketEx.SendMsg(new SocketMsg
                        {
                            ClientIndex = clientIndex,
                            MsgIndex = msgIndex,
                            Delay = delay,
                        });
                    }
                    catch (Exception ex)
                    {
                        _iLogger.LogCritical($"StartSendMsg SendMsgAsync Excpetion: {ex}");
                        RemoveWebSocketClient(wsEx);
                    }
                }
            }

            ClientWebSocketEx.Semaphore.WaitOne();            
        }
    }
}
