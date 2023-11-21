using System.Net.WebSockets;
using Microsoft.Extensions.Logging;

namespace TestLibary
{
    public class HttpJsonMsg
    {
        public int ClientIndex;
        public int MsgIndex;
        public string? ReceiveUrl;
    }
}
