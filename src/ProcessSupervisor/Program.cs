using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Configuration;
using System.IO;
using System.Threading;

namespace ProcessSupervisor
{
    class Program
    {
        private static Process _targetProcess;
        private static string _logFilePathName;
        private static int _timerInverval;
        private static int _workingSetThreshold;

        static void Main(string[] args)
        {
            _logFilePathName = Path.Combine(Directory.GetCurrentDirectory(), "Log.txt");
            Console.WriteLine("Log file path: " + _logFilePathName);
            _timerInverval = int.Parse(ConfigurationManager.AppSettings["TimerInterval"]) * 60 * 1000;
            _workingSetThreshold = int.Parse(ConfigurationManager.AppSettings["WorkingSetThreshold"]);

            RunProcessSupervisor();

            Console.WriteLine("Press any key to quit...");
            Console.ReadKey(true);
        }

        private static void RunProcessSupervisor()
        {
            string appPathName = ConfigurationManager.AppSettings["ApplicationPathName"];

            if (string.IsNullOrEmpty(appPathName))
            {
                Console.Error.WriteLine("ApplicationPathName is not set in App.config.");
                Environment.Exit(-1);
                return;
            }

            string appName = Path.GetFileNameWithoutExtension(appPathName);

            Process[] processes = Process.GetProcesses();
            foreach (Process p in processes)
            {
                if (p.ProcessName.Equals(appName, StringComparison.CurrentCultureIgnoreCase))
                {
                    _targetProcess = p;
                    continue;
                }

                p.Dispose();
            }

            if (_targetProcess == null)
            {
                StartTargetProcess();
            }

            ThreadPool.QueueUserWorkItem(state =>
            {
                Queue<DateTime> dtQueue = new Queue<DateTime>();
                PopulateRebootTimeQueue(dtQueue);

                while (true)
                {
                    if (dtQueue.Count > 0)
                        RebootProcessIfNecessary(dtQueue);

                    RebootIfMemoryUsageExceeds();

                    Thread.Sleep(_timerInverval);
                }
            });
        }

        private static void PopulateRebootTimeQueue(Queue<DateTime> dtQueue)
        {
            string rebootTime = ConfigurationManager.AppSettings["RebootTime"];
            if (string.IsNullOrEmpty(rebootTime))
                return;

            string[] strTimes = rebootTime.Split('|');
            List<DateTime> dts = new List<DateTime>(strTimes.Length);
            DateTime dt;
            foreach (string time in strTimes)
            {
                dt = DateTime.Today.Add(TimeSpan.Parse(time));
                if (dt <= DateTime.Now)
                {
                    dts.Add(dt.AddDays(1));
                }
                else
                    dts.Add(dt);
            }

            foreach (DateTime t in dts.OrderBy(d => d))
            {
                dtQueue.Enqueue(t);
            }

            AppendLog("Reboot time in queue:");
            foreach (DateTime t in dtQueue)
            {
                AppendLog(t.ToString("g"));
            }
        }

        private static void RebootProcessIfNecessary(Queue<DateTime> dtQueue)
        {
            DateTime dt = dtQueue.Peek();
            if (DateTime.Now < dt)
                return;

            dtQueue.Dequeue();
            dtQueue.Enqueue(dt.AddDays(1));

            RebootTargetProcess();

            AppendLog("Application rebooted.");
            AppendLog("Reboot time in queue:");
            foreach (DateTime t in dtQueue)
            {
                AppendLog(t.ToString("g"));
            }
        }

        private static void RebootIfMemoryUsageExceeds()
        {
            if (_targetProcess.HasExited)
            {
                _targetProcess.Dispose();
                return;
            }

            PerformanceCounter counter = new PerformanceCounter("Process", "Working Set - Private", _targetProcess.ProcessName);
            float memoryUsage = (float)counter.RawValue / 1024 / 1024;
            counter.Dispose();

            if (memoryUsage > _workingSetThreshold)
            {
                AppendLog($"Application exceeds memory usage threshold and will be rebooted. Threshold: {_workingSetThreshold} MB, actual memory usage: {memoryUsage} MB");
                RebootTargetProcess();
            }
        }

        private static void RebootTargetProcess()
        {
            KillTargetProcess();
            Thread.Sleep(3000);
            StartTargetProcess();
        }

        private static void StartTargetProcess()
        {
            string appPathName = ConfigurationManager.AppSettings["ApplicationPathName"];
            string arguments = ConfigurationManager.AppSettings["Arguments"];
            _targetProcess = new Process();
            _targetProcess.StartInfo.FileName = appPathName;
            _targetProcess.StartInfo.UseShellExecute = true;
            _targetProcess.StartInfo.Arguments = arguments;
            _targetProcess.Start();
        }

        private static void KillTargetProcess()
        {
            if (_targetProcess.HasExited)
            {
                _targetProcess.Dispose();
                return;
            }

            _targetProcess.Kill();
            _targetProcess.Dispose();
        }

        private static void AppendLog(string msg)
        {
            File.AppendAllText(_logFilePathName, $"[{DateTime.Now.ToString("G")}] {msg}{Environment.NewLine}");
        }

        private static long ConvertToMB(long size)
        {
            return size / 1024 / 1024;
        }
    }
}
