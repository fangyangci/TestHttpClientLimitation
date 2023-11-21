using ASEDirectlineClient;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using TestLibary;

namespace TestHttpClientClient.Controllers
{
    [ApiController]
    [Route("")]
    public class HomeController : ControllerBase
    {
        //public static string TargetHost { set; get; } = "localhost:7226";
        //public static string SelfHost { set; get; } = "localhost:44382";

        public static string TargetHost { set; get; } = "cfy-testhttpclient-service.azurewebsites.net";
        public static string SelfHost { set; get; } = "0c54-58-208-230-172.ngrok-free.app";

        private readonly ILogger<HomeController> _iLogger;
        private ASEWebSocket _aseWebSocket;
        private static HttpClient _httpClient = new HttpClient(new HttpClientHandler
        {
            //MaxConnectionsPerServer = 50,
        })
        {
            Timeout = TimeSpan.FromDays(10)
        };
        public HomeController(ASEWebSocket aseWebSocket, ILogger<HomeController> iLogger)
        {
            _aseWebSocket = aseWebSocket;
            _iLogger = iLogger;
        }

        [Route("")]
        public IActionResult Home()
        {
            return Ok($"TargetHost:{TargetHost}\r\nSelfHost:{SelfHost}");
        }

        [Route("SetTarget")]
        public IActionResult SetTarget(string url)
        {
            TargetHost = new Uri(url).Host;
            return Ok($"TargetHost:{TargetHost}");
        }

        [Route("SetSelf")]
        public IActionResult SetSelf(string url)
        {
            SelfHost = new Uri(url).Host;
            return Ok($"SelfHost:{SelfHost}");
        }

        private static bool _isTestHttpRunning;

        [Route("testhttp")]
        public async Task<IActionResult> TestHttp(int c = 100, int m = 100, int d = 3, string? mark = "")
        {
            if (_isTestHttpRunning)
            {
                return Ok($"Test Http Is Already Running: {ASEHttp.FinishedCount}/{ASEHttp.TotalCount}");
            }

            var curTimeStr = DateTime.Now.ToString("yyyy-M-d_HH-mm-ss");
            var curLogFolder = $"{curTimeStr}_ch_c{c}_m{m}_mark{mark}";
            ASELogProvider.SetLogDateTimeFolder(curLogFolder);
            await _httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Get, $"https://{TargetHost}/setcurfolder?f={curTimeStr}_sh_c{c}_m{m}_mark{mark}"));

           
            _ = Task.Run(async () =>
            {
                _isTestHttpRunning = true;
                var now = DateTime.Now;
                await ASEHttp.StartAsync(c, m, d, mark, _httpClient, _iLogger);
                _iLogger.LogCritical($"ClientCount:{c} MsgCount:{m} Delay:{d} Mark:{mark}\r\nSendMsgSucceed SpendTime:{DateTime.Now - now}");
                ASEHttp.Semaphore.WaitOne();
                _iLogger.LogCritical($"ClientCount:{c} MsgCount:{m} Delay:{d} Mark:{mark}\r\nAllSucceed SpendTime:{DateTime.Now - now}");
                _isTestHttpRunning = false;
            });

            return Ok("StartSucceed");
        }

        [Route("receivehttp")]
        public async Task<IActionResult> ResHttp()
        {
            if (ASEHttp.Finished == null)
            {
                _iLogger.LogCritical("ResHttp ASEHttp.Finished IsNull Exception");
                return StatusCode(400);
            }
            string body;

            using (var sr = new StreamReader(Request.Body))
            {
                body = await sr.ReadToEndAsync();
            }
            var httpJsonMsg = JsonConvert.DeserializeObject<HttpJsonMsg>(body);
            if (httpJsonMsg == null)
            {
                return StatusCode(401);
            }
            var ci = httpJsonMsg.ClientIndex;
            var mi = httpJsonMsg.MsgIndex;
            if (ASEHttp.Finished[ci, mi])
            {
                _iLogger.LogCritical($"ResHttp ASEHttp.Finished IsTrue Exception: {ci}--{mi}");
                return StatusCode(402);
            }
            _iLogger.LogInformation(ci + 1, $"ReceiveMsg:{mi}");
            var tmp = Interlocked.Increment(ref ASEHttp.FinishedCount);
            if (tmp == ASEHttp.TotalCount)
            {
                ASEHttp.Semaphore.Release();
                return Ok();
            }            
            return Ok();
        }

        private static bool _isTestWebSocketRunning;

        [Route("testwebsocket")]
        public async Task<IActionResult> TestWebSocket(int c = 100, int m = 100, int d = 3)
        {
            if (_aseWebSocket.Inited != 2)
            {
                _ = _aseWebSocket.InitAll();
                return Ok("AseWebSocket Is _initing");
            }
            if (_isTestWebSocketRunning)
            {
                return Ok($"Test WebSocket Is Already Running: {ClientWebSocketEx.FinishedCount}/{ClientWebSocketEx.TotalCount}");
            }

            var curTimeStr = DateTime.Now.ToString("yyyy-M-d_HH-mm-ss");
            var curLogFolder = $"{curTimeStr}_cw_c{c}_m{m}";
            ASELogProvider.SetLogDateTimeFolder(curLogFolder);
            await _httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Get, $"https://{TargetHost}/setcurfolder?f={curTimeStr}_sw_c{c}_m{m}"));

            _ = Task.Run(async () =>
            {
                _isTestWebSocketRunning = true;
                var now = DateTime.Now;
                await _aseWebSocket.StartAsync(c, m, d);
                _iLogger.LogCritical($"ClientCount:{c} MsgCount:{m} Delay:{d} \r\nAllSucceed SpendTime:{DateTime.Now - now}");
                _isTestWebSocketRunning = false;
            });

            return Ok("StartSucceed");
        }
    }
}