using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Apollo.DeviceViewers;
using Apollo.Elements;
using Apollo.Structures;
using Apollo.Undo;

namespace Apollo.Devices {
    public class LayerFilterData: DeviceData {
        public LayerFilterViewer Viewer => Instance?.SpecificViewer<LayerFilterViewer>();

        int _target;
        public int Target {
            get => _target;
            set {
                if (_target != value) {
                    _target = value;
                    
                    Viewer?.SetTarget(Target);
                }
            }
        }

        int _range;
        public int Range {
            get => _range;
            set {
                if (_range != value) {
                    _range = value;
                    
                    Viewer?.SetRange(Range);
                }
            }
        }

        public LayerFilterData(int target = 0, int range = 0) {
            Target = target;
            Range = range;
        }

        protected override DeviceData CloneSpecific()
            => new LayerFilterData(Target, Range);
        
        protected override Device ActivateSpecific(DeviceData data)
            => new LayerFilter((LayerFilterData)data);
    }

    public class LayerFilter: Device {
        public new LayerFilterData Data => (LayerFilterData)Data;

        public LayerFilter(LayerFilterData data): base(data, "layerfilter", "Layer Filter") {}

        public override void MIDIProcess(List<Signal> n)
            => InvokeExit(n.Where(i => Math.Abs(i.Layer - Data.Target) <= Data.Range).ToList());
        
        public class TargetUndoEntry: SimplePathUndoEntry<LayerFilter, int> {
            protected override void Action(LayerFilter item, int element) => item.Data.Target = element;
            
            public TargetUndoEntry(LayerFilter filter, int u, int r)
            : base($"Layer Filter Target Changed to {r}", filter, u, r) {}
            
            TargetUndoEntry(BinaryReader reader, int version)
            : base(reader, version) {}
        }
        
        public class RangeUndoEntry: SimplePathUndoEntry<LayerFilter, int> {
            protected override void Action(LayerFilter item, int element) => item.Data.Range = element;
            
            public RangeUndoEntry(LayerFilter filter, int u, int r)
            : base($"Layer Filter Range Changed to {r}", filter, u, r) {}
            
            RangeUndoEntry(BinaryReader reader, int version)
            : base(reader, version) {}
        }
    }
}