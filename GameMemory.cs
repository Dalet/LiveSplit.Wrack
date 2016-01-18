using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Text.RegularExpressions;
using System.Globalization;

namespace LiveSplit.Wrack
{
    class GameMemory
    {
        public const int SLEEP_TIME = 15;

        public List<string> levels = new List<string>(new string[]
        {
        });

        public event EventHandler OnFirstLevelLoading;
        public delegate void TimerStartEventHandler(object sender, uint ticks);
        public event TimerStartEventHandler OnTimerStart;
        public delegate void OnTickEventHandler(object sender, uint ticks);
        public event OnTickEventHandler OnTick;
        public delegate void OnSplitEventHandler(object sender, string level);
        public event OnSplitEventHandler OnSplit;
        public event EventHandler OnDeath;

        private Task _thread;
        private CancellationTokenSource _cancelSource;
        private SynchronizationContext _uiThread;

        private DeepPointer _gameTimePtr;
        private DeepPointer _currentMapPtr;
        private DeepPointer _playerHealthPtr;
        private DeepPointer _isLevelDonePtr;

        public uint frameCounter = 0;

        public GameMemory()
        {
            _gameTimePtr = new DeepPointer(0x19B014);
            _currentMapPtr = new DeepPointer(0x19B140);
            _playerHealthPtr = new DeepPointer(0x0019D7A8, 0x14c);
            _isLevelDonePtr = new DeepPointer(0x199CF4);
        }

        public void StartMonitoring()
        {
            if (_thread != null && _thread.Status == TaskStatus.Running)
            {
                throw new InvalidOperationException();
            }
            if (!(SynchronizationContext.Current is WindowsFormsSynchronizationContext))
            {
                throw new InvalidOperationException("SynchronizationContext.Current is not a UI thread.");
            }

            _uiThread = SynchronizationContext.Current;
            _cancelSource = new CancellationTokenSource();
            _thread = Task.Factory.StartNew(MemoryReadThread);
        }

        public void Stop()
        {
            if (_cancelSource == null || _thread == null || _thread.Status != TaskStatus.Running)
            {
                return;
            }

            _cancelSource.Cancel();
            _thread.Wait();
        }
        void MemoryReadThread()
        {
            Trace.WriteLine("[NoLoads] MemoryReadThread");

            while (!_cancelSource.IsCancellationRequested)
            {
                try
                {
                    Trace.WriteLine("[NoLoads] Waiting for Wrack_steam.exe...");

                    Process game;
                    while ((game = GetGameProcess()) == null)
                    {
                        Thread.Sleep(250);
                        if (_cancelSource.IsCancellationRequested)
                        {
                            return;
                        }
                    }

                    Trace.WriteLine("[NoLoads] Got Wrack_steam.exe!");

                    frameCounter = 0;
                    uint prevGameTime = 0;
                    string prevCurrentMap = String.Empty;
                    int prevPlayerHealth = -1;
                    bool prevIsLevelDone = false;

                    while (!game.HasExited)
                    {
                        uint gameTime;
                        _gameTimePtr.Deref(game, out gameTime);

                        string currentMap;
                        _currentMapPtr.Deref(game, out currentMap, 50);

                        int playerHealth;
                        _playerHealthPtr.Deref(game, out playerHealth);

                        bool isLevelDone;
                        _isLevelDonePtr.Deref(game, out isLevelDone);

                        if (gameTime == 0 && currentMap.ToLower() == "e1l1.map")
                        {
                            _uiThread.Post(d =>
                            {
                                if (this.OnFirstLevelLoading != null)
                                {
                                    this.OnFirstLevelLoading(this, EventArgs.Empty);
                                }
                            }, null);
                        }

                        if (gameTime > 0 && prevGameTime == 0 && frameCounter != 0)
                        {
                            var ticks = gameTime;
                            _uiThread.Post(d =>
                            {
                                if (this.OnTimerStart != null)
                                {
                                    this.OnTimerStart(this, ticks);
                                }
                            }, null);
                        }
                        else if (gameTime != prevGameTime && gameTime > prevGameTime)
                        {
                            var timeToAdd = gameTime - prevGameTime;
                            //Debug.WriteLine("GameTime: " + gameTime + " prevGameTime: " + prevGameTime + " Result: " + timeToAdd + " - " + frameCounter);
                            _uiThread.Post(d =>
                            {
                                if (this.OnTick != null)
                                {
                                    this.OnTick(this, timeToAdd);
                                }
                            }, null);
                        }

                        if (prevPlayerHealth != playerHealth)
                        {
                            Debug.WriteLine("playerHealth changed from " + prevPlayerHealth + " to " + playerHealth + " - " + frameCounter);
                        }

						if (isLevelDone != prevIsLevelDone)
                        {
                            Debug.WriteLine("isLevelDone changed from " + prevIsLevelDone + " to " + isLevelDone + " - " + frameCounter);
                            if (isLevelDone)
                            {
                                if (playerHealth > 0)
                                {
                                    _uiThread.Post(d =>
                                    {
                                        if (this.OnSplit != null)
                                        {
                                            this.OnSplit(this, currentMap);
                                        }
                                    }, null);
                                }
                                else
                                {
                                    _uiThread.Post(d =>
                                    {
                                        if (this.OnDeath != null)
                                        {
                                            this.OnDeath(this, EventArgs.Empty);
                                        }
                                    }, null);
                                }
                            }
                        }

                        frameCounter++;
                        prevGameTime = gameTime;
                        prevCurrentMap = currentMap;
                        prevPlayerHealth = playerHealth;
                        prevIsLevelDone = isLevelDone;

                        Thread.Sleep(SLEEP_TIME);

                        if (_cancelSource.IsCancellationRequested)
                        {
                            return;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Trace.WriteLine(ex.ToString());
                    Thread.Sleep(1000);
                }
            }
        }

        Process GetGameProcess()
        {
            Process game = Process.GetProcesses().FirstOrDefault(p => p.ProcessName.ToLower() == "wrack_steam"
                && !p.HasExited);
            if (game == null)
            {
                return null;
            }

            return game;
        }
    }
}
