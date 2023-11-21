using System.Net.WebSockets;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Text;

namespace TestLibary
{
    public class WebSocketEx
    {
        public const string NO_STREAM = "soocket disconnect";
        public const int TCP_HEAD_LENGTH = 4;

        public WebSocket WebSocket { get; }
        public int Id { get; }

        private SyncQueue<SocketMsg> _sendingMsgs = new SyncQueue<SocketMsg>();
        private readonly ILogger _iLogger;

        public WebSocketEx(WebSocket webSocket, int id, ILogger iLogger)
        {
            WebSocket = webSocket;
            Id = id;
            _iLogger = iLogger;
            _ = Task.Run(HandleMsgSend);
        }

        public void SendMsg(SocketMsg msg)
        {
            _sendingMsgs.Enqueue(msg);
        }

        private async Task HandleMsgSend()
        {
            while (true)
            {
                var msg = _sendingMsgs.Dequeue();
                if (msg == null)
                {
                    return;
                }
                await SendMsgAsync(msg);
            }
        }

        private async Task SendMsgAsync(SocketMsg msg)
        {
            var msgStr = JsonConvert.SerializeObject(msg);
            var msgBytes = Encoding.UTF8.GetBytes(msgStr);
            var headBytes = BitConverter.GetBytes(msgBytes.Length);

            await WebSocket.SendAsync(new ArraySegment<byte>(headBytes, 0, headBytes.Length), WebSocketMessageType.Binary, true, CancellationToken.None);
            await WebSocket.SendAsync(new ArraySegment<byte>(msgBytes, 0, msgBytes.Length), WebSocketMessageType.Binary, true, CancellationToken.None);

            _iLogger.LogInformation(msg.ClientIndex + 1, $"{Id}_SendOneMsg:{msg.MsgIndex}");
        }

        public async Task<SocketMsg?> ReadOneMsg()
        {
            var headBytes = await ReadBytes(WebSocket, TCP_HEAD_LENGTH);
            var msgLen = BitConverter.ToInt32(headBytes, 0);
            var msgBytes = await ReadBytes(WebSocket, msgLen);
            var msgStr = Encoding.UTF8.GetString(msgBytes);
            SocketMsg? msg;
            try
            {
                msg = JsonConvert.DeserializeObject<SocketMsg>(msgStr);
                if (msg == null)
                {
                    _iLogger.LogCritical("ReadOneMsg MsgIsNull Exception");
                    return null;
                }
            }
            catch (Exception ex)
            {
                _iLogger.LogCritical($"ReadOneMsg JsonConvert Exception: {ex}");
                throw;
            }
            _iLogger.LogInformation(msg.ClientIndex + 1, $"{Id}ReceiveOneMsg:{msg.MsgIndex}");
            return msg;
        }

        private static async Task<byte[]> ReadBytes(WebSocket webSocket, long length)
        {
            var buffer = new byte[length];
            var leftLength = buffer.Length;
            var readPos = 0;
            while (leftLength > 0)
            {
                var memory = new ArraySegment<byte>(buffer, readPos, leftLength);
                var readRes = await webSocket.ReceiveAsync(memory, CancellationToken.None);
                if (readRes.MessageType == WebSocketMessageType.Close)
                {
                    await webSocket.CloseAsync(readRes.CloseStatus ?? default, readRes.CloseStatusDescription, CancellationToken.None);
                    throw new Exception(NO_STREAM);
                }
                if (readRes.Count == 0)
                {
                    throw new Exception(NO_STREAM);
                }
                readPos += readRes.Count;
                leftLength -= readRes.Count;
            }
            return buffer;
        }
    }
}
