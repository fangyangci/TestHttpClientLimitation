using Newtonsoft.Json;
using TestHttpClientClient.Controllers;
using TestLibary;

namespace TestHttpClientClient
{
    public class ASEHttp
    {
        public static bool[,]? Finished;

        public static int FinishedCount;
        public static int TotalCount;
        public static Semaphore Semaphore = new Semaphore(0, 1);
        public static ASEHttp[]? _aseClients;

        public static async Task StartAsync(int clientCount, int msgCount, int delay, string? mark, HttpClient httpClient, ILogger iLogger)
        {
            FinishedCount = 0;
            TotalCount = clientCount * msgCount;
            Finished = new bool[clientCount, msgCount];
            _aseClients = new ASEHttp[clientCount];
            var tasks = new List<Task>();
            for (int i = 0; i < clientCount; i++)
            {
                _aseClients[i] = new ASEHttp(i, msgCount, delay, mark, httpClient, iLogger);
                _aseClients[i].StartSendMsg();
            }
            await Task.WhenAll(tasks);
        }

        private readonly int _clientIndex;
        private int _msgCount;
        private int _delay;
        private string? _mark;
        private readonly HttpClient _httpClient;
        private readonly ILogger _iLogger;

        public ASEHttp(int clientIndex, int msgCount, int delay, string? mark, HttpClient httpClient, ILogger iLogger)
        {
            _clientIndex = clientIndex;
            _msgCount = msgCount;
            _delay = delay;
            _mark = mark;
            _httpClient = httpClient;
            _iLogger = iLogger;
        }

        public void StartSendMsg()
        {
            for (int i = 0; i < _msgCount; i++)
            {
                var tmp = i;
                try
                {
                    _iLogger.LogInformation(_clientIndex + 1, $"SendMsg:{tmp}");

                    var httpMsg = new HttpRequestMessage(HttpMethod.Get, $"https://{HomeController.TargetHost}/http?delay={_delay}&mark={_mark}");

                    httpMsg.Content = new StringContent(JsonConvert.SerializeObject(new HttpJsonMsg
                    {
                        ClientIndex = _clientIndex,
                        MsgIndex = tmp,
                        ReceiveUrl = $"https://{HomeController.SelfHost}/receivehttp?mark={_mark}"
                    }));
                    httpMsg.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");

                    _ = _httpClient.SendAsync(httpMsg);
                }
                catch (Exception ex)
                {
                    _iLogger.LogCritical($"ASEClientStartAsyncException:{ex}");
                }
            }
        }
    }
}
