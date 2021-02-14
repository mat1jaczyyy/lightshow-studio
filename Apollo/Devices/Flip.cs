using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.IO;

using Apollo.DeviceViewers;
using Apollo.Elements;
using Apollo.Enums;
using Apollo.Structures;
using Apollo.Undo;

namespace Apollo.Devices {
    public class FlipData: DeviceData {
        FlipViewer Viewer => Instance?.SpecificViewer<FlipViewer>();

        FlipType _mode;
        public FlipType Mode {
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

        public FlipData(FlipType mode = FlipType.Horizontal, bool bypass = false) {
            Mode = mode;
            Bypass = bypass;
        }

        protected override DeviceData CloneSpecific()
            => new FlipData(Mode, Bypass);

        protected override Device ActivateSpecific(DeviceData data)
            => new Flip((FlipData)data);
    }

    public class Flip: Device {
        public new FlipData Data => (FlipData)Data;

        public Flip(FlipData data): base(data?? new(), "flip") {}

        public override void MIDIProcess(List<Signal> n)
            => InvokeExit((Data.Bypass? n.Select(i => i.Clone()) : Enumerable.Empty<Signal>()).Concat(n.SelectMany(i => {
                if (i.Index == 100) 
                    return Data.Bypass? Enumerable.Empty<Signal>() : new [] {i};
                    
                int x = i.Index % 10;
                int y = i.Index / 10;

                if (Data.Mode == FlipType.Horizontal) x = 9 - x;
                else if (Data.Mode == FlipType.Vertical) y = 9 - y;

                else if (Data.Mode == FlipType.Diagonal1) {
                    int temp = x;
                    x = y;
                    y = temp;
                
                } else if (Data.Mode == FlipType.Diagonal2) {
                    x = 9 - x;
                    y = 9 - y;

                    int temp = x;
                    x = y;
                    y = temp;
                }

                i.Index = (byte)(y * 10 + x);
                return new [] {i};
            })).ToList());
        
        public class ModeUndoEntry: EnumSimplePathUndoEntry<Flip, FlipType> {
            protected override void Action(Flip item, FlipType element) => item.Data.Mode = element;
            
            public ModeUndoEntry(Flip flip, FlipType u, FlipType r, IEnumerable source)
            : base("Flip Orientation", flip, u, r, source) {}
            
            ModeUndoEntry(BinaryReader reader, int version)
            : base(reader, version) {}
        }
        
        public class BypassUndoEntry: SimplePathUndoEntry<Flip, bool> {
            protected override void Action(Flip item, bool element) => item.Data.Bypass = element;
            
            public BypassUndoEntry(Flip flip, bool u, bool r)
            : base($"Flip Bypass Changed to {(r? "Enabled" : "Disabled")}", flip, u, r) {}
            
            BypassUndoEntry(BinaryReader reader, int version)
            : base(reader, version) {}
        }
    }
}