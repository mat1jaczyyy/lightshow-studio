using System.Collections.Generic;
using System.IO;

using Apollo.DeviceViewers;
using Apollo.Elements;
using Apollo.Structures;
using Apollo.Undo;

namespace Apollo.Devices {
    public class PaintData: DeviceData {
        PaintViewer Viewer => Instance?.SpecificViewer<PaintViewer>();

        Color _color;
        public Color Color {
            get => _color;
            set {
                if (_color != value) {
                    _color = value;

                    Viewer?.Set(Color);
                }
            }
        }

        public PaintData(Color color = null)
            => Color = color?? new Color();

        protected override DeviceData CloneSpecific()
            => new PaintData(Color.Clone());

        protected override Device ActivateSpecific(DeviceData data)
            => new Paint((PaintData)data);
    }

    public class Paint: Device {
        public new PaintData Data => (PaintData)Data;

        public Paint(PaintData data = null): base(data?? new(), "paint") {}

        public override void MIDIProcess(List<Signal> n) {
            n.ForEach(i => {
                if (i.Color.Lit)
                    i.Color = Data.Color.Clone();
            });

            InvokeExit(n);
        }
        
        public class ColorUndoEntry: SimplePathUndoEntry<Paint, Color> {
            protected override void Action(Paint item, Color element) => item.Data.Color = element.Clone();
            
            public ColorUndoEntry(Paint paint, Color u, Color r)
            : base($"Paint Color Changed to {r.ToHex()}", paint, u, r) {}
            
            ColorUndoEntry(BinaryReader reader, int version)
            : base(reader, version) {}
        }
    }
}