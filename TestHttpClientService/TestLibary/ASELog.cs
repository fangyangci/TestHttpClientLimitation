namespace ASEDirectlineClient
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;

    public class ASELogProvider : ILoggerProvider
    {
        private const string RootPath = "../../LocalLogs";
        public static string? LogRoot { set; get; }
        public static string? CurLogDir { set;  get; }
        public static string LogKey => $"{Environment.MachineName}_{Environment.GetEnvironmentVariable("WEBSITE_INSTANCE_ID")?.Substring(0, 5)}";

        private ASELog? _iLogger;

        public ILogger CreateLogger(string categoryName)
        {
            _iLogger = new ASELog(categoryName);
            return _iLogger;
        }

        public static void SetLogDateTimeFolder(string? folderName)
        {
            LogRoot = $"{RootPath}/{folderName ?? DateTime.Now.ToString("yyyy-M-d_HH-mm-ss")}";

            var machineDir = $"{LogRoot}/{LogKey}";
            if (Directory.Exists(machineDir))
            {
                Directory.Delete(machineDir, true);
            }
            Directory.CreateDirectory(machineDir);
            CurLogDir = Path.GetFullPath(machineDir);
        }

        public void Dispose()
        {
            _iLogger?.Dispose();
        }
    }

    internal class ASELog : ILogger, IDisposable
    {
        class LogMsg
        {
            public LogLevel LogLevel { get; }
            public string Msg { get; }
            public int PathIndex { get; }

            public LogMsg(string msg, int pathIndex, LogLevel level)
            {
                Msg = msg;
                PathIndex = pathIndex;
                LogLevel = level;
            }
        }

        private const string End = "END";
        private readonly Queue<LogMsg> _logs = new Queue<LogMsg>();
        private readonly Semaphore _semaphore = new Semaphore(0, int.MaxValue);
        private readonly string _categoryName;
        private bool _disposed;
        private Task _logTask;

        internal ASELog(string categoryName = "Local") 
        {
            _logTask = Task.Run(HandleLog);
            _categoryName = categoryName;
        }

        private void Log(string msg, int eId = 0, LogLevel logLevel = LogLevel.Information)
        {
            if (_disposed)
            {
                return;
            }
            lock (_logs)
            {
                _logs.Enqueue(new LogMsg(msg, eId, logLevel));
            }
            _semaphore.Release();
        }

        private async Task HandleLog()
        {
            while (true)
            {
                _semaphore.WaitOne();
                LogMsg msg;
                lock (_logs)
                {
                    msg = _logs.Dequeue();
                }
                if (msg.Msg == End)
                {
                    return;
                }
                var logDir = $"{ASELogProvider.CurLogDir}/{_categoryName}/";
                var tmpPath = $"{logDir}/{msg.PathIndex}.log";
                if (!Directory.Exists(logDir))
                {
                    Directory.CreateDirectory(logDir);
                }
                if (msg.LogLevel == LogLevel.Critical)
                {
                    while (true)
                    {
                        try
                        {
                            using (var fs = new FileStream($"{ASELogProvider.LogRoot}/Critical.log", FileMode.Append))
                            {
                                using (var sw = new StreamWriter(fs))
                                {
                                    await sw.WriteLineAsync(
                                        $"{DateTime.Now.ToString("HH:mm:ss.fff")}\r\n" +
                                        $"{ASELogProvider.LogKey}\r\n" +
                                        $"{msg.Msg}\r\n" +
                                        $"--------------------------");
                                }
                            }
                        }
                        catch
                        {
                            Thread.Sleep(1000);
                            continue;
                        }
                        break;
                    }
                }
                using (var fs = new FileStream(tmpPath, FileMode.Append))
                {
                    using(var sw = new StreamWriter(fs))
                    {
                        await sw.WriteLineAsync($"{DateTime.Now.ToString("HH:mm:ss.fff")}\r\nLog{msg.LogLevel.ToString()}:{msg.Msg}\r\n--------------------------");
                    }
                }
            }
        }

        public void Dispose()
        {
            Log(End);
            _disposed = true;
            _logTask.Wait();
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            if (string.IsNullOrEmpty(ASELogProvider.CurLogDir))
            {
                return;
            }
            var msg = $"{formatter(state, exception)}";
            Log(msg, eventId.Id, logLevel);
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public IDisposable BeginScope<TState>(TState state)
        {
            return this;
        }
    }
}
