using System.Collections.Generic;
using System.IO;

using Apollo.DeviceViewers;
using Apollo.Elements;
using Apollo.Rendering;
using Apollo.Structures;
using Apollo.Undo;

namespace Apollo.Devices {
    public class DelayData: DeviceData {
        public DelayViewer Viewer => Instance?.SpecificViewer<DelayViewer>();

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

        public DelayData(TimeData time = null, double gate = 1) {
            Time = time?? new TimeData();
            Gate = gate;
        }

        protected override DeviceData CloneSpecific()
            => new DelayData(Time.Clone(), Gate);
        
        protected override Device ActivateSpecific(DeviceData data)
            => new Delay((DelayData)data);
    }

    public class Delay: Device {
        public new DelayData Data => (DelayData)Data;

        public readonly Time Time;

        void FreeChanged(int value)
            => Data.Viewer?.SetDurationValue(value);

        void ModeChanged(bool value)
            => Data.Viewer?.SetMode(value);

        void StepChanged(Length value)
            => Data.Viewer?.SetDurationStep(value);

        public Delay(DelayData data = null): base(data?? new(), "delay") {
            Time = Data.Time.Activate();

            Time.Minimum = 1;
            Time.Maximum = 30000;

            Time.FreeChanged += FreeChanged;
            Time.ModeChanged += ModeChanged;
            Time.StepChanged += StepChanged;
        }

        public override void MIDIProcess(List<Signal> n)
            => Schedule(() => InvokeExit(n), Heaven.Time + Time * Data.Gate);

        public override void Dispose() {
            if (Disposed) return;

            Stop();

            Time.Dispose();
            base.Dispose();
        }
        
        public class DurationUndoEntry: SimplePathUndoEntry<Delay, int> {
            protected override void Action(Delay item, int element) => item.Time.Data.Free = element;
            
            public DurationUndoEntry(Delay delay, int u, int r)
            : base($"Delay Duration Changed to {r}ms", delay, u, r) {}
            
            DurationUndoEntry(BinaryReader reader, int version)
            : base(reader, version) {}
        }
        
        public class DurationModeUndoEntry: SimplePathUndoEntry<Delay, bool> {
            protected override void Action(Delay item, bool element) => item.Time.Data.Mode = element;
            
            public DurationModeUndoEntry(Delay delay, bool u, bool r)
            : base($"Delay Duration Switched to {(r? "Steps" : "Free")}", delay, u, r) {}
            
            DurationModeUndoEntry(BinaryReader reader, int version)
            : base(reader, version) {}
        }
        
        public class DurationStepUndoEntry: SimplePathUndoEntry<Delay, int> {
            protected override void Action(Delay item, int element) => item.Time.Data.Length.Step = element;
            
            public DurationStepUndoEntry(Delay delay, int u, int r)
            : base($"Delay Duration Changed to {Length.Steps[r]}", delay, u, r) {}
            
            DurationStepUndoEntry(BinaryReader reader, int version)
            : base(reader, version) {}
        }
        
        public class GateUndoEntry: SimplePathUndoEntry<Delay, double> {
            protected override void Action(Delay item, double element) => item.Data.Gate = element;
            
            public GateUndoEntry(Delay delay, double u, double r)
            : base($"Delay Gate Changed to {r}%", delay, u / 100, r / 100) {}
            
            GateUndoEntry(BinaryReader reader, int version)
            : base(reader, version) {}
        }
    }
}