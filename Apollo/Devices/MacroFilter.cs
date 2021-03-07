using System.Collections.Generic;
using System.IO;
using System.Linq;

using Apollo.Core;
using Apollo.DeviceViewers;
using Apollo.Elements;
using Apollo.Structures;
using Apollo.Undo;

namespace Apollo.Devices {
    public class MacroFilterData: DeviceData {
        public MacroFilterViewer Viewer => Instance?.SpecificViewer<MacroFilterViewer>();

        bool[] _filter;
        public bool[] Filter {
            get => _filter;
            set {
                if (value != null && value.Length == 100) {
                    _filter = value;

                    Viewer?.Set(_filter);
                }
            }
        }

        public bool this[int index] {
            get => _filter[index];
            set {
                if (0 <= index && index <= 99)
                    _filter[index] = value;
            }
        }

        int _macro;
        public int Macro {
            get => _macro;
            set {
                if (_macro != value && 1 <= value && value <= 4) {
                   _macro = value;

                   Viewer?.SetMacro(Macro);
                }
            }
        }

        public MacroFilterData(int target = 1, bool[] init = null) {
            Macro = target;

            if (init == null || init.Length != 100) {
                init = new bool[100];
                init[Program.Project.GetMacro(Macro) - 1] = true;
            }
            
            _filter = init;
        }

        protected override DeviceData CloneSpecific()
            => new MacroFilterData(Macro, Filter.ToArray());
        
        protected override Device ActivateSpecific(DeviceData data)
            => new MacroFilter((MacroFilterData)data);
    }

    public class MacroFilter: Device {
        public new MacroFilterData Data => (MacroFilterData)Data;

        public MacroFilter(MacroFilterData data = null): base(data?? new(), "macrofilter", "Macro Filter") {}

        public override void MIDIProcess(List<Signal> n) 
            => InvokeExit(n.Where(i => Data[i.GetMacro(Data.Macro) - 1]).ToList());
        
        public class TargetUndoEntry: SimplePathUndoEntry<MacroFilter, int> {
            protected override void Action(MacroFilter item, int element) => item.Data.Macro = element;
            
            public TargetUndoEntry(MacroFilter filter, int u, int r)
            : base($"Macro Filter Target Changed to {r}", filter, u, r) {}
            
            TargetUndoEntry(BinaryReader reader, int version)
            : base(reader, version) {}
        }
        
        public class FilterUndoEntry: SimplePathUndoEntry<MacroFilter, bool[]> {
            protected override void Action(MacroFilter item, bool[] element) => item.Data.Filter = element.ToArray();
            
            public FilterUndoEntry(MacroFilter filter, bool[] u)
            : base($"Macro Filter Changed", filter, u.ToArray(), filter.Data.Filter.ToArray()) {}
            
            FilterUndoEntry(BinaryReader reader, int version)
            : base(reader, version) {}
        }
    }
}