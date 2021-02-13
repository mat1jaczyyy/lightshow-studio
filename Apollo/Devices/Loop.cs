using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Apollo.DeviceViewers;
using Apollo.Elements;
using Apollo.Rendering;
using Apollo.Structures;
using Apollo.Undo;

namespace Apollo.Devices {
    public class LoopData: DeviceData {
        public LoopViewer Viewer => Instance?.SpecificViewer<LoopViewer>();

        public TimeData Rate;
        
        double _gate;
        public double Gate {
            get => _gate;
            set {
                if (0.01 <= value && value <= 4) {
                    _gate = value;
                    
                    Viewer?.SetGate(Gate);
                }
            }
        }
        
        int _repeats;
        public int Repeats {
            get => _repeats;
            set {
                if (1 <= value && value <= 128 && _repeats != value) {
                    _repeats = value;
                    
                    Viewer?.SetRepeats(Repeats);
                }
            }
        }
        
        bool _hold;
        public bool Hold {
            get => _hold;
            set {
                _hold = value;

                Viewer?.SetHold(Hold);
            }
        }

        public LoopData(TimeData rate = null, double gate = 1, int repeats = 2, bool hold = false) {
            Rate = rate?? new TimeData();
            Gate = gate;
            Repeats = repeats;
            Hold = hold;
        }

        protected override DeviceData CloneSpecific()
            => new LoopData(Rate.Clone(), Gate, Repeats, Hold);
        
        protected override Device ActivateSpecific(DeviceData data)
            => new Loop((LoopData)data);
    }

    public class Loop: Device {
        public new LoopData Data => (LoopData)Data;

        public readonly Time Rate;

        void FreeChanged(int value)
            => Data.Viewer?.SetRateValue(value);

        void ModeChanged(bool value)
            => Data.Viewer?.SetMode(value);

        void StepChanged(Length value)
            => Data.Viewer?.SetRateStep(value);

        public double RealTime => Rate * Data.Gate;
               
        public Loop(LoopData data): base(data, "loop") {
            Rate = Data.Rate.Activate();

            Rate.Minimum = 1;
            Rate.Maximum = 30000;

            Rate.FreeChanged += FreeChanged;
            Rate.ModeChanged += ModeChanged;
            Rate.StepChanged += StepChanged;
        }
        
        ConcurrentDictionary<Signal, Signal> buffer = new();
        
        public override void MIDIProcess(List<Signal> n)
            => InvokeExit(n.SelectMany(s => {
                double start = Heaven.Time;

                if (Data.Hold) {
                    Signal k = s.With(color: new Color());
                    
                    if (s.Color.Lit) { 
                        buffer[k] = s;
                        
                        void Next() {
                            if (buffer.ContainsKey(k) && ReferenceEquals(buffer[k], s)) {
                                Schedule(Next, start += RealTime);
                                InvokeExit(new List<Signal>() {s.Clone()});
                            }
                        };
                        
                        Schedule(Next, start += RealTime);

                    } else buffer.TryRemove(k, out _);
                    
                } else {
                    int index = 1;
                    
                    void Next() {
                        if (++index <= Data.Repeats) {
                            Schedule(Next, start += RealTime);
                            InvokeExit(new List<Signal>() {s.Clone()});
                        }
                    };
                    
                    Schedule(Next, start += RealTime);
                }

                return new [] {s.Clone()};
            }).ToList());

        protected override void Stopped() => buffer.Clear();
        
        public override void Dispose() {
            if (Disposed) return;
            
            Stop();

            base.Dispose();
        }
        
        public class RateUndoEntry: SimplePathUndoEntry<Loop, int> {
            protected override void Action(Loop item, int element) => item.Rate.Data.Free = element;
            
            public RateUndoEntry(Loop loop, int u, int r)
            : base($"Loop Rate Changed to {r}ms", loop, u, r) {}
            
            RateUndoEntry(BinaryReader reader, int version)
            : base(reader, version) {}
        }
        
        public class RateModeUndoEntry: SimplePathUndoEntry<Loop, bool> {
            protected override void Action(Loop item, bool element) => item.Rate.Data.Mode = element;
            
            public RateModeUndoEntry(Loop loop, bool u, bool r)
            : base($"Loop Rate Switched to {(r? "Steps" : "Free")}", loop, u, r) {}
            
            RateModeUndoEntry(BinaryReader reader, int version)
            : base(reader, version) {}
        }
        
        public class RateStepUndoEntry: SimplePathUndoEntry<Loop, int> {
            protected override void Action(Loop item, int element) => item.Rate.Length.Data.Step = element;
            
            public RateStepUndoEntry(Loop loop, int u, int r)
            : base($"Loop Rate Changed to {Length.Steps[r]}", loop, u, r) {}
            
            RateStepUndoEntry(BinaryReader reader, int version)
            : base(reader, version) {}
        }
        
        public class HoldUndoEntry: SimplePathUndoEntry<Loop, bool> {
            protected override void Action(Loop item, bool element) => item.Data.Hold = element;
            
            public HoldUndoEntry(Loop loop, bool u, bool r)
            : base($"Loop Hold Changed to {(r? "Enabled" : "Disabled")}", loop, u, r) {}
            
            HoldUndoEntry(BinaryReader reader, int version)
            : base(reader, version) {}
        }
        
        public class GateUndoEntry: SimplePathUndoEntry<Loop, double> {
            protected override void Action(Loop item, double element) => item.Data.Gate = element;
            
            public GateUndoEntry(Loop loop, double u, double r)
            : base($"Loop Gate Changed to {r}%", loop, u / 100, r / 100) {}
            
            GateUndoEntry(BinaryReader reader, int version)
            : base(reader, version) {}
        }
        
        public class RepeatsUndoEntry: SimplePathUndoEntry<Loop, int> {
            protected override void Action(Loop item, int element) => item.Data.Repeats = element;
            
            public RepeatsUndoEntry(Loop loop, int u, int r)
            : base($"Loop Repeats Changed to {r}", loop, u, r) {}
            
            RepeatsUndoEntry(BinaryReader reader, int version)
            : base(reader, version) {}
        }
    }
}