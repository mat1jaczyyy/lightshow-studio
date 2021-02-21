using System.Collections.Generic;
using System.IO;
using System.Linq;

using Apollo.Core;
using Apollo.DeviceViewers;
using Apollo.Elements;
using Apollo.Structures;
using Apollo.Undo;

namespace Apollo.Devices {
    public class RefreshData: DeviceData {
        RefreshViewer Viewer => Instance?.SpecificViewer<RefreshViewer>();

        bool[] _macros = new bool[4];

        public bool GetMacro(int index) => _macros[index];
        public void SetMacro(int index, bool macro) {
            _macros[index] = macro;

            Viewer?.SetMacro(index, macro);
        }

        public RefreshData(bool[] macros = null) {
            if (macros == null || macros.Length != 4) macros = new bool[4];
            _macros = macros;
        }

        protected override DeviceData CloneSpecific()
            => new RefreshData(_macros.ToArray());

        protected override Device ActivateSpecific(DeviceData data)
            => new Refresh((RefreshData)data);
    }

    public class Refresh: Device {
        public new RefreshData Data => (RefreshData)Data;

        public Refresh(RefreshData data): base(data?? new(), "refresh") {}

        public override void MIDIProcess(List<Signal> n) {
            n.ForEach(i => {
                for (int j = 0; j < 4; j++) {
                    if (Data.GetMacro(j))
                        i.Macros[j] = (int)Program.Project.GetMacro(j + 1);
                }
            });
            
            InvokeExit(n);
        }
        
        public class MacroUndoEntry: SimpleIndexPathUndoEntry<Refresh, bool> {
            protected override void Action(Refresh item, int index, bool element) => item.Data.SetMacro(index, element);
            
            public MacroUndoEntry(Refresh refresh, int index, bool u, bool r)
            : base($"Refresh Macro {index + 1} changed to {(r? "Enabled" : "Disabled")}", refresh, index, u, r) {}
            
            MacroUndoEntry(BinaryReader reader, int version)
            : base(reader, version) {}
        }
    }
}