using System;
using System.Buffers.Text;
using System.Collections;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using ASEDirectlineClient;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TestLibary;

namespace TestHttpClientService.Controllers
{
    [ApiController]
    [Route("")]
    public class HomeController : ControllerBase
    {
        private readonly ILogger _iLogger;
        private IHttpClientFactory _httpClientFactory;
        static SyncQueue<WebSocketEx> _allWsEx = new SyncQueue<WebSocketEx>();

        public HomeController(IHttpClientFactory httpClientFactory, ILogger<HomeController> iLogger)
        {
            _httpClientFactory = httpClientFactory;
            _iLogger = iLogger;
        }

        [Route("")]
        public IActionResult Home()
        {
            return Ok(_allWsEx.Count + "\r\n" + JsonConvert.SerializeObject(_allWsEx.Select(
                s => new 
                {
                    Id = s?.Id,
                    Status = s?.WebSocket?.State                    
                })));
        }

        [Route("SetCurFolder")]
        public IActionResult SetCurFolder(string f)
        {
            ASELogProvider.SetLogDateTimeFolder(f);
            return Ok(ASELogProvider.CurLogDir);
        }

        [Route("http")]
        public async Task<IActionResult> Http(int delay)
        {
            string body;
            using (var sr = new StreamReader(Request.Body))
            {
                body = await sr.ReadToEndAsync();
            }
            var httpJsonMsg = JsonConvert.DeserializeObject<HttpJsonMsg>(body);
            if (httpJsonMsg == null)
            {
                return StatusCode(400);
            }
            _iLogger.LogInformation(httpJsonMsg.ClientIndex + 1, $"ReceiveMsg:{httpJsonMsg.MsgIndex}");

            await Task.Delay(TimeSpan.FromSeconds(delay));
            var httpRequestMsg = new HttpRequestMessage(HttpMethod.Get, httpJsonMsg?.ReceiveUrl);
            httpRequestMsg.Content = new StringContent(body);
            httpRequestMsg.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");

            using (var httpClient = _httpClientFactory.CreateClient())
            {
                var res = await httpClient.SendAsync(httpRequestMsg);
                _iLogger.LogInformation(httpJsonMsg.ClientIndex + 1, $"SendMsg:{httpJsonMsg.MsgIndex}");
                return StatusCode((int)res.StatusCode);
            }          
        }


        [Route("webSocket")]
        public async Task<IActionResult> WebSocket(int id)
        {
            var ws = await Request.HttpContext.WebSockets.AcceptWebSocketAsync();
            var wsEx = new WebSocketEx(ws, id, _iLogger);
            _allWsEx.Enqueue(wsEx);
            await ReceiveAsync(wsEx);
            
            return Ok();
        }

        private async Task ReceiveAsync(WebSocketEx webSocket)
        {
            while (webSocket.WebSocket.State == WebSocketState.Open)
            {
                var msg = await webSocket.ReadOneMsg();
                if (msg == null)
                {
                    throw new Exception("MsgIsNull");
                }
                _ = HandleMsgAsync(msg, webSocket);
            }
        }

        private async Task HandleMsgAsync(SocketMsg msg, WebSocketEx webSocket)
        {
            await Task.Delay(TimeSpan.FromSeconds(msg.Delay));
            webSocket.SendMsg(msg);
        }
    }
}