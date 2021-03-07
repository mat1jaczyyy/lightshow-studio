using System.Collections.Generic;
using System.IO;
using System.Linq;

using Apollo.Core;
using Apollo.DeviceViewers;
using Apollo.Elements;
using Apollo.Structures;
using Apollo.Undo;

namespace Apollo.Devices {
    public class SwitchData: DeviceData {
        SwitchViewer Viewer => Instance?.SpecificViewer<SwitchViewer>();

        int _target = 1;
        public int Target {
            get => _target;
            set {
                if (1 <= value && value <= 4 && _target != value) {
                    _target = value;
                    
                    Viewer?.SetTarget(Target);
                }
            }
        }
        
        int _value = 1;
        public int Value {
            get => _value;
            set {
                if (1 <= value && value <= 100 && _value != value) {
                    _value = value;
                    
                    Viewer?.SetValue(Value);
                }
            }
        }

        public SwitchData(int target = 1, int value = 1) {
            Target = target;
            Value = value;
        }

        protected override DeviceData CloneSpecific()
            => new SwitchData(Target, Value);

        protected override Device ActivateSpecific(DeviceData data)
            => new Switch((SwitchData)data);
    }

    public class Switch: Device {
        public new SwitchData Data => (SwitchData)Data;

        public Switch(SwitchData data): base(data?? new(), "switch") {}

        public override void MIDIProcess(List<Signal> n) {
            if (n.Any(i => !i.Color.Lit))
                Program.Project.SetMacro(Data.Target, Data.Value);

            InvokeExit(n);
        }
        
        public class TargetUndoEntry: SimplePathUndoEntry<Switch, int> {
            protected override void Action(Switch item, int element) => item.Data.Target = element;
            
            public TargetUndoEntry(Switch macroswitch, int u, int r)
            : base($"Switch Target Changed to {r}", macroswitch, u, r) {}
            
            TargetUndoEntry(BinaryReader reader, int version)
            : base(reader, version) {}
        }
        
        public class ValueUndoEntry: SimplePathUndoEntry<Switch, int> {
            protected override void Action(Switch item, int element) => item.Data.Value = element;
            
            public ValueUndoEntry(Switch macroswitch, int u, int r)
            : base($"Switch Value Changed to {r}", macroswitch, u, r) {}
            
            ValueUndoEntry(BinaryReader reader, int version)
            : base(reader, version) {}
        }
    }
}