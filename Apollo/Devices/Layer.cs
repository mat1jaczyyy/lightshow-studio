using System.Collections.Generic;
using System.IO;

using Apollo.DeviceViewers;
using Apollo.Elements;
using Apollo.Enums;
using Apollo.Structures;
using Apollo.Undo;

namespace Apollo.Devices {
    public class LayerData: DeviceData {
        public LayerViewer Viewer => Instance?.SpecificViewer<LayerViewer>();

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

        BlendingType _mode;
        public BlendingType BlendingMode {
            get => _mode;
            set {
                _mode = value;

                Viewer?.SetMode(BlendingMode);
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
        
        public LayerData(int target = 0, BlendingType blending = BlendingType.Normal, int range = 200) {
            Target = target;
            BlendingMode = blending;
            Range = range;
        }

        protected override DeviceData CloneSpecific()
            => new LayerData(Target, BlendingMode, Range);
        
        protected override Device ActivateSpecific(DeviceData data)
            => new Layer((LayerData)data);
    }

    public class Layer: Device {
        public new LayerData Data => (LayerData)Data;

        public Layer(LayerData data = null): base(data?? new(), "layer") {}

        public override void MIDIProcess(List<Signal> n) {
            n.ForEach(i => {
                i.Layer = Data.Target;
                i.BlendingMode = Data.BlendingMode;
                i.BlendingRange = Data.Range;
            });

            InvokeExit(n);
        }
        
        public class TargetUndoEntry: SimplePathUndoEntry<Layer, int> {
            protected override void Action(Layer item, int element) => item.Data.Target = element;
            
            public TargetUndoEntry(Layer layer, int u, int r)
            : base($"Layer Target Changed to {r}", layer, u, r) {}
            
            TargetUndoEntry(BinaryReader reader, int version)
            : base(reader, version) {}
        }
        
        public class ModeUndoEntry: SimplePathUndoEntry<Layer, BlendingType> {
            protected override void Action(Layer item, BlendingType element) => item.Data.BlendingMode = element;
            
            public ModeUndoEntry(Layer layer, BlendingType u, BlendingType r)
            : base($"Layer Blending Changed to {r}", layer, u, r) {}
            
            ModeUndoEntry(BinaryReader reader, int version)
            : base(reader, version) {}
        }
        
        public class RangeUndoEntry: SimplePathUndoEntry<Layer, int> {
            protected override void Action(Layer item, int element) => item.Data.Range = element;
            
            public RangeUndoEntry(Layer layer, int u, int r)
            : base($"Layer Range Changed to {r}", layer, u, r) {}
            
            RangeUndoEntry(BinaryReader reader, int version)
            : base(reader, version) {}
        }
    }
}