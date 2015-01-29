using LiveSplit.Model;
using LiveSplit.TimeFormatters;
using LiveSplit.UI.Components;
using LiveSplit.UI;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Xml;
using System.Windows.Forms;
using System.Diagnostics;
using System.Reflection;

namespace LiveSplit.Wrack
{
    class WrackComponent : LogicComponent
    {
        public override string ComponentName
        {
            get { return "Wrack"; }
        }

        public WrackSettings Settings { get; set; }
        public bool Disposed { get; private set; }
        public bool IsLayoutComponent { get; private set; }

        private TimerModel _timer;
        private GameMemory _gameMemory;
        private LiveSplitState _state;
        public uint TimerTicks { get; protected set; }
        public uint DeathCounter { get; protected set; }
        public WrackComponent(LiveSplitState state, bool isLayoutComponent)
        {
            bool debug = false;
#if DEBUG
            debug = true;
#endif
            Trace.WriteLine("[NoLoads] Using LiveSplit.Wrack component version " + Assembly.GetExecutingAssembly().GetName().Version + " " + ((debug) ? "Debug" : "Release") + " build");
            _state = state;
            this.IsLayoutComponent = isLayoutComponent;

            this.Settings = new WrackSettings();

            _timer = new TimerModel { CurrentState = state };

            TimerTicks = 0;

            _gameMemory = new GameMemory();
            _gameMemory.OnFirstLevelLoading += gameMemory_OnFirstLevelLoading;
            _gameMemory.OnTimerStart += gameMemory_OnTimerStart;
            _gameMemory.OnSplit += gameMemory_OnSplit;
            _gameMemory.OnTick += gameMemory_OnTick;
            _gameMemory.OnDeath += gameMemory_OnDeath;
            state.OnStart += State_OnStart;
            _gameMemory.StartMonitoring();
        }

        public override void Dispose()
        {
            this.Disposed = true;

            _state.OnStart -= State_OnStart;

            if (_gameMemory != null)
            {
                _gameMemory.Stop();
            }

        }

        void State_OnStart(object sender, EventArgs e)
        {
            _state.IsGameTimePaused = true;
            TimerTicks = 0;
            DeathCounter = 0;
            _gameMemory.levels.Clear();
        }

        void gameMemory_OnTick(object sender, uint ticks)
        {
            if (_state.CurrentPhase == TimerPhase.Running)
            {
                TimerTicks += ticks;
                _state.SetGameTime(TimeSpan.FromSeconds((float)TimerTicks / (float)60));
                Debug.WriteLine("OnTick: GameTime changed to: " + _state.CurrentTime.GameTime + " | Added " + ticks + " ticks | Total ticks " + TimerTicks + " - " + _gameMemory.frameCounter);
            }
        }

        void gameMemory_OnFirstLevelLoading(object sender, EventArgs e)
        {
            if ((_state.CurrentPhase == TimerPhase.Running || _state.CurrentPhase == TimerPhase.Ended) && this.Settings.AutoReset)
            {
                Trace.WriteLine(String.Format("[NoLoads] Reset - {0}", _gameMemory.frameCounter));
                _timer.Reset();
            }
        }

        void gameMemory_OnTimerStart(object sender, uint ticks)
        {
            if (_state.CurrentPhase == TimerPhase.NotRunning && this.Settings.AutoStart)
            {
                var timeToAdd = TimeSpan.FromSeconds((float)ticks / (float)60);
                TimeSpan originalOffset = _state.Run.Offset;
                _state.Run.Offset = timeToAdd;
                _timer.Start();
                _state.Run.Offset = originalOffset;
                TimerTicks = ticks;
                Trace.WriteLine(String.Format("[NoLoads] Start at GT: {1} ({2} ticks) RT: {3} - {0}", _gameMemory.frameCounter, _state.CurrentTime.GameTime, ticks, _state.CurrentTime.RealTime));
            }
        }

        void gameMemory_OnSplit(object sender, string level)
        {
            level = level.ToLower();
            if (_state.CurrentPhase == TimerPhase.Running && Settings.AutoSplit && !_gameMemory.levels.Contains(level))
            {
                Trace.WriteLine(String.Format("[NoLoads] Split \"{1}\" triggered - {0}", _gameMemory.frameCounter, level));
                _timer.Split();
                _gameMemory.levels.Add(level);
            }
            else
                Trace.WriteLine(String.Format("[NoLoads] Tried to split \"{1}\" but it failed {2} - {0}", _gameMemory.frameCounter, level, (!_gameMemory.levels.Contains(level)) ? "because it was already split before" : ""));
        }

        void gameMemory_OnDeath(object sender, EventArgs e)
        {
            DeathCounter++;
        }
        public override XmlNode GetSettings(XmlDocument document)
        {
            return this.Settings.GetSettings(document);
        }

        public override Control GetSettingsControl(LayoutMode mode)
        {
            return this.Settings;
        }

        public override void SetSettings(XmlNode settings)
        {
            this.Settings.SetSettings(settings);
        }

        public override void Update(IInvalidator invalidator, LiveSplitState state, float width, float height, LayoutMode mode) { }
        public override void RenameComparison(string oldName, string newName) { }
    }
}
