namespace TMGWebRole
{
    using System;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;

    public class RestartingProcessHost
    {
        internal class ValueTypeWrapper<T>
        {
            // Maybe I'm stoopid, but how can I change a value type which is wrapped in a closure, unless I wrap it in an object?!?
            public T Value { get; set; }
        }

        private readonly Func<Process> CreateProcess;
        private readonly CancellationTokenSource cancellationTokenSource;
        private bool _onStopRequested = false;

        public RestartingProcessHost(Func<Process> createProcess, CancellationTokenSource cancellationTokenSource)
        {
            this.CreateProcess = createProcess;
            this.cancellationTokenSource = cancellationTokenSource;

            this.cancellationTokenSource.Token.Register(() => 
            { 
                this._onStopRequested = true;
            });
        }

        #region Logging

        private readonly Action<string> _defaultLogger = (s) => { }; 

        private Action<string> _error;
        public Action<string> error
        {
            get { return _error ?? (_error = _defaultLogger); }
            set { _error = value; }
        }
        private Action<string> _warn;
        public Action<string> warn
        {
            get { return _warn ?? (_warn = _defaultLogger); }
            set { _warn = value; }
        }
        private Action<string> _info;
        public Action<string> info
        {
            get { return _info ?? (_info = _defaultLogger); }
            set { _info = value; }
        }

        #endregion

        public Task StartRunTask()
        {
            var cancellationToken = cancellationTokenSource.Token;
            var processLaunched = new ValueTypeWrapper<bool> { Value = false };
            var processExitedEventHappened = new ValueTypeWrapper<bool> { Value = false };
            var pid = new ValueTypeWrapper<int>();
            var processName = new ValueTypeWrapper<string>();

            Action hostProcess = () =>
            {
                #region hostProcess

                var process = this.CreateProcess();

                process.Exited += (s, a) => processExitedEventHappened.Value = true;

                info(string.Format("Try to launch {0}", process.StartInfo.FileName));

                process.Start();
                if (process.StartInfo.RedirectStandardOutput) process.BeginOutputReadLine();
                if (process.StartInfo.RedirectStandardError) process.BeginErrorReadLine();
                pid.Value = process.Id;
                processLaunched.Value = true;
                processName.Value = process.StartInfo.FileName;

                info(string.Format("Launched {0} (pid {1})", processName.Value, pid.Value));

                if (cancellationToken.WaitHandle.WaitOne()) // wait infinite 
                {
                    warn(string.Format("Killing {0} now", processName.Value));
                    if (!process.HasExited)
                    {
                        process.Kill();
                    }
                }

                #endregion
            };

            var processTask = Task.Factory.StartNew(hostProcess, cancellationToken);
            while (!processLaunched.Value)
            {
                warn("Waiting a until the task is launched...");

                Thread.Sleep(TimeSpan.FromSeconds(1));
            }

            Action hostMonitor = () =>
            {
                int i = 0;
                while (true)
                {
                    #region Ensure task didn't crash accidentally, and if so, re-launch

                    bool processFoundInMemory = false;
                    try
                    {
                        var processFromSystem = Process.GetProcessById(pid.Value);
                        processFoundInMemory = true;
                    }
                    catch (ArgumentException) { }

                    bool unexpectedTermination = processExitedEventHappened.Value || !processFoundInMemory;

                    if (processTask.IsCompleted || unexpectedTermination)
                    {
                        if (_onStopRequested)
                        {
                            error(string.Format("Process {0} successfully shut down because of an OnStop() call", processName.Value));
                            return; // Leave the Run() method
                        }
                        else if (unexpectedTermination)
                        {
                            error(string.Format("Process {0} stopped working for unknown reasons... Restarting it", processName.Value));
                            processExitedEventHappened.Value = false;

                            processLaunched.Value = false;
                            processTask = Task.Factory.StartNew(hostProcess, cancellationToken);
                            while (!processLaunched.Value)
                            {
                                Thread.Sleep(TimeSpan.FromSeconds(1));
                            }
                        }
                    }

                    #endregion

                    Thread.Sleep(TimeSpan.FromMilliseconds(500));

                    if ((i++) % 600 == 0)
                    {
                        info("Running");
                    }
                }
            };

            return Task.Factory.StartNew(hostMonitor, cancellationToken);
        }

        public void Stop()
        {
            // this._onStopRequested = true;

            cancellationTokenSource.Cancel();
        }
    }
}