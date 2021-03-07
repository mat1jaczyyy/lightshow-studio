using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Apollo.DeviceViewers;
using Apollo.Elements;
using Apollo.Enums;
using Apollo.Rendering;
using Apollo.Structures;
using Apollo.Undo;

namespace Apollo.Devices {
    public class HoldData: DeviceData {
        public HoldViewer Viewer => Instance?.SpecificViewer<HoldViewer>();

        public TimeData Time;

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

        HoldType _holdmode;
        public HoldType HoldMode {
            get => _holdmode;
            set {
                _holdmode = value;
                
                Viewer?.SetHoldMode(HoldMode);
                Instance?.Stop();
            }
        }

        bool _release;
        public bool Release {
            get => _release;
            set {
                _release = value;
                
                Viewer?.SetRelease(Release);
            }
        }

        public bool ActualRelease => HoldMode == HoldType.Minimum? false : Release;

        public HoldData(TimeData time = null, double gate = 1, HoldType holdmode = HoldType.Trigger, bool release = false) {
            Time = time?? new TimeData();
            Gate = gate;
            HoldMode = holdmode;
            Release = release;
        }

        protected override DeviceData CloneSpecific()
            => new HoldData(Time.Clone(), Gate, HoldMode, Release);
        
        protected override Device ActivateSpecific(DeviceData data)
            => new Hold((HoldData)data);
    }

    public class Hold: Device {
        public new HoldData Data => (HoldData)Data;

        public readonly Time Time;

        void FreeChanged(int value)
            => Data.Viewer?.SetDurationValue(value);

        void ModeChanged(bool value)
            => Data.Viewer?.SetMode(value);

        void StepChanged(Length value)
            => Data.Viewer?.SetDurationStep(value);

        public Hold(HoldData data = null): base(data?? new(), "hold") {
            Time = Data.Time.Activate();

            Time.Minimum = 1;
            Time.Maximum = 30000;

            Time.FreeChanged += FreeChanged;
            Time.ModeChanged += ModeChanged;
            Time.StepChanged += StepChanged;
        }
        
        ConcurrentDictionary<Signal, Color> buffer = new();
        ConcurrentDictionary<Signal, int> minimum = new();
        
        public override void MIDIProcess(List<Signal> n) => InvokeExit(n.SelectMany(s => {
            Signal k = s.With(color: new Color(0));
            
            if (s.Color.Lit)
                buffer[k] = s.Color;
            
            if (s.Color.Lit != Data.ActualRelease) {
                s.Color = buffer[k];
                
                if (Data.HoldMode != HoldType.Infinite) {
                    if (Data.HoldMode == HoldType.Minimum) minimum[k] = 0;

                    Schedule(() => {
                        if (ReferenceEquals(buffer[k], s.Color)) {
                            if (Data.HoldMode == HoldType.Minimum && minimum[k] == 0) {
                                minimum[k] = 2;
                                return;
                            }

                            InvokeExit(new List<Signal>() {k.Clone()});
                        }
                    }, Heaven.Time + Time * Data.Gate);
                }
                
                return new [] {s};

            } else if (Data.HoldMode == HoldType.Minimum) {
                if (minimum[k] == 0) minimum[k] = 1;
                else if (minimum[k] == 2)
                    InvokeExit(new List<Signal>() {k.Clone()});
            }
            
            return Enumerable.Empty<Signal>();
        }).Select(i => i.Clone()).ToList());

        protected override void Stopped() {
            buffer.Clear();
            minimum.Clear();
        }

        public override void Dispose() {
            if (Disposed) return;

            Stop();

            Time.Dispose();
            base.Dispose();
        }
        
        public class DurationUndoEntry: SimplePathUndoEntry<Hold, int> {
            protected override void Action(Hold item, int element) => item.Time.Data.Free = element;
            
            public DurationUndoEntry(Hold hold, int u, int r)
            : base($"Hold Duration Changed to {r}ms", hold, u, r) {}
            
            DurationUndoEntry(BinaryReader reader, int version)
            : base(reader, version) {}
        }
        
        public class DurationModeUndoEntry: SimplePathUndoEntry<Hold, bool> {
            protected override void Action(Hold item, bool element) => item.Time.Data.Mode = element;
            
            public DurationModeUndoEntry(Hold hold, bool u, bool r)
            : base($"Hold Duration Switched to {(r? "Steps" : "Free")}", hold, u, r) {}
            
            DurationModeUndoEntry(BinaryReader reader, int version)
            : base(reader, version) {}
        }
        
        public class DurationStepUndoEntry: SimplePathUndoEntry<Hold, int> {
            protected override void Action(Hold item, int element) => item.Time.Length.Data.Step = element;
            
            public DurationStepUndoEntry(Hold hold, int u, int r)
            : base($"Hold Duration Changed to {Length.Steps[r]}", hold, u, r) {}
            
            DurationStepUndoEntry(BinaryReader reader, int version)
            : base(reader, version) {}
        }
        
        public class GateUndoEntry: SimplePathUndoEntry<Hold, double> {
            protected override void Action(Hold item, double element) => item.Data.Gate = element;
            
            public GateUndoEntry(Hold hold, double u, double r)
            : base($"Hold Gate Changed to {r}%", hold, u / 100, r / 100) {}
            
            GateUndoEntry(BinaryReader reader, int version)
            : base(reader, version) {}
        }
        
        public class HoldModeUndoEntry: EnumSimplePathUndoEntry<Hold, HoldType> {
            protected override void Action(Hold item, HoldType element) => item.Data.HoldMode = element;
            
            public HoldModeUndoEntry(Hold hold, HoldType u, HoldType r, IEnumerable source)
            : base("Hold Mode", hold, u, r, source) {}
            
            HoldModeUndoEntry(BinaryReader reader, int version)
            : base(reader, version) {}
        }
        
        public class ReleaseUndoEntry: SimplePathUndoEntry<Hold, bool> {
            protected override void Action(Hold item, bool element) => item.Data.Release = element;
            
            public ReleaseUndoEntry(Hold hold, bool u, bool r)
            : base($"Hold Release Changed to {(r? "Enabled" : "Disabled")}", hold, u, r) {}
            
            ReleaseUndoEntry(BinaryReader reader, int version)
            : base(reader, version) {}
        }
    }
}