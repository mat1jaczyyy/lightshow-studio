using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Apollo.DeviceViewers;
using Apollo.Elements;
using Apollo.Enums;
using Apollo.Structures;
using Apollo.Undo;

namespace Apollo.Devices {
    public class RotateData: DeviceData {
        RotateViewer Viewer => Instance?.SpecificViewer<RotateViewer>();

        RotateType _mode;
        public RotateType Mode {
            get => _mode;
            set {
                _mode = value;
                
                Viewer?.SetMode(Mode);
            }
        }

        bool _bypass;
        public bool Bypass {
            get => _bypass;
            set {
                _bypass = value;
                
                Viewer?.SetBypass(Bypass);
            }
        }

        public RotateData(RotateType mode = RotateType.D90, bool bypass = false) {
            Mode = mode;
            Bypass = bypass;
        }

        protected override DeviceData CloneSpecific()
            => new RotateData(Mode, Bypass);

        protected override Device ActivateSpecific(DeviceData data)
            => new Rotate((RotateData)data);
    }

    public class Rotate: Device {
        public new RotateData Data => (RotateData)Data;

        public Rotate(RotateData data = null): base(data?? new(), "rotate") {}

        public override void MIDIProcess(List<Signal> n)
            => InvokeExit((Data.Bypass? n.Select(i => i.Clone()) : Enumerable.Empty<Signal>()).Concat(n.SelectMany(i => {
                if (i.Index == 100) 
                    return Data.Bypass? Enumerable.Empty<Signal>() : new [] {i};

                if (Data.Mode == RotateType.D90)
                    i.Index = (byte)((9 - i.Index % 10) * 10 + i.Index / 10);

                else if (Data.Mode == RotateType.D180)
                    i.Index = (byte)((9 - i.Index / 10) * 10 + 9 - i.Index % 10);

                else if (Data.Mode == RotateType.D270)
                    i.Index = (byte)((i.Index % 10) * 10 + 9 - i.Index / 10);

                return new [] {i};
            })).ToList());
        
        public class ModeUndoEntry: EnumSimplePathUndoEntry<Rotate, RotateType> {
            protected override void Action(Rotate item, RotateType element) => item.Data.Mode = element;
            
            public ModeUndoEntry(Rotate rotate, RotateType u, RotateType r, IEnumerable source)
            : base("Rotate Angle", rotate, u, r, source) {}
            
            ModeUndoEntry(BinaryReader reader, int version)
            : base(reader, version) {}

        }
        
        public class BypassUndoEntry: SimplePathUndoEntry<Rotate, bool> {
            protected override void Action(Rotate item, bool element) => item.Data.Bypass = element;
            
            public BypassUndoEntry(Rotate rotate, bool u, bool r)
            : base($"Rotate Bypass Changed to {(r? "Enabled" : "Disabled")}", rotate, u, r) {}
            
            BypassUndoEntry(BinaryReader reader, int version)
            : base(reader, version) {}
        }
    }
}