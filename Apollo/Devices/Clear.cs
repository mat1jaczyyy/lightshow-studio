using System.Collections.Generic;
using System.IO;
using System.Linq;

using Apollo.Core;
using Apollo.DeviceViewers;
using Apollo.Elements;
using Apollo.Enums;
using Apollo.Structures;
using Apollo.Undo;

namespace Apollo.Devices {
    public class ClearData: DeviceData {
        ClearType _mode;
        public ClearType Mode {
            get => _mode;
            set {
                _mode = value;

                SpecificViewer<ClearViewer>(i => i.SetMode(Mode));
            }
        }

        protected override DeviceData CloneSpecific() => new ClearData(Mode);

        public ClearData(ClearType mode = ClearType.Lights) => Mode = mode;

        protected override Device ActivateSpecific(DeviceData data) => new Clear((ClearData)data);
    }

    public class Clear: Device {
        public new ClearData Data => (ClearData)Data;
        
        public Clear(ClearData data): base(data, "clear") {}

        public override void MIDIProcess(List<Signal> n) {
            if (n.Any(i => !i.Color.Lit)) {
                if (Data.Mode == ClearType.Multi) Multi.InvokeReset();
                else MIDI.ClearState(multi: Data.Mode == ClearType.Both);
            }

            InvokeExit(n);
        }

        public class ModeUndoEntry: SimplePathUndoEntry<Clear, ClearType> {
            protected override void Action(Clear item, ClearType element) => item.Data.Mode = element;
            
            public ModeUndoEntry(Clear clear, ClearType u, ClearType r)
            : base($"Clear Mode Changed to {r.ToString()}", clear, u, r) {}
            
            ModeUndoEntry(BinaryReader reader, int version)
            : base(reader, version) {}
        }
    }
}