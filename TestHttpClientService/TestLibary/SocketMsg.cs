using System.Net.WebSockets;
using System.Text;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace TestLibary
{
    public class SocketMsg
    {
        public int ClientIndex;
        public int MsgIndex;
        public int Delay;
    }
}
