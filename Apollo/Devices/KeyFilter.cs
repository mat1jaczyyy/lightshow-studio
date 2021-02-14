using System.Collections.Generic;
using System.IO;
using System.Linq;

using Apollo.DeviceViewers;
using Apollo.Elements;
using Apollo.Structures;
using Apollo.Undo;

namespace Apollo.Devices {
    public class KeyFilterData: DeviceData {
        public KeyFilterViewer Viewer => Instance?.SpecificViewer<KeyFilterViewer>();

        bool[] _filter;
        public bool[] Filter {
            get => _filter;
            set {
                if (value != null && value.Length == 101) {
                    _filter = value;

                    Viewer?.Set(_filter);
                }
            }
        }

        public bool this[int index] {
            get => _filter[index];
            set {
                if (0 <= index && index <= 100)
                    _filter[index] = value;
            }
        }

        public KeyFilterData(bool[] init = null) {
            if (init == null || init.Length != 101) init = new bool[101];
            _filter = init;
        }

        protected override DeviceData CloneSpecific()
            => new KeyFilterData(Filter.ToArray());
        
        protected override Device ActivateSpecific(DeviceData data)
            => new KeyFilter((KeyFilterData)data);
    }

    public class KeyFilter: Device {
        public new KeyFilterData Data => (KeyFilterData)Data;

        public KeyFilter(KeyFilterData data): base(data?? new(), "keyfilter", "Key Filter") {}

        public override void MIDIProcess(List<Signal> n)
            => InvokeExit(n.Where(i => Data[i.Index]).ToList());
        
        public class ChangedUndoEntry: SimplePathUndoEntry<KeyFilter, bool[]> {
            protected override void Action(KeyFilter item, bool[] element) => item.Data.Filter = element.ToArray();
            
            public ChangedUndoEntry(KeyFilter filter, bool[] u)
            : base($"Key Filter Changed", filter, u.ToArray(), filter.Data.Filter.ToArray()) {}
            
            ChangedUndoEntry(BinaryReader reader, int version)
            : base(reader, version) {}
        }
    }
}