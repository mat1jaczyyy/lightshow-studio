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
    public class MoveData: DeviceData {
        public MoveViewer Viewer => Instance?.SpecificViewer<MoveViewer>();

        public OffsetData Offset;

        GridType _gridmode;
        public GridType GridMode {
            get => _gridmode;
            set {
                _gridmode = value;

                Viewer?.SetGridMode(GridMode);
            }
        }

        bool _wrap;
        public bool Wrap {
            get => _wrap;
            set {
                _wrap = value;

                Viewer?.SetWrap(Wrap);
            }
        }

        public MoveData(OffsetData offset = null, GridType gridmode = GridType.Full, bool wrap = false) {
            Offset = offset?? new OffsetData();
            GridMode = gridmode;
            Wrap = wrap;
        }

        protected override DeviceData CloneSpecific()
            => new MoveData(Offset.Clone(), GridMode, Wrap);

        protected override Device ActivateSpecific(DeviceData data)
            => new Move((MoveData)data);
    }

    public class Move: Device {
        public new MoveData Data => (MoveData)Data;

        public readonly Offset Offset;

        void OffsetChanged(Offset sender)
            => Data.Viewer?.SetOffset(Offset);

        public Move(MoveData data): base(data, "move") {
            Offset = Data.Offset.Activate();
            Offset.Changed += OffsetChanged;
        }

        public override void MIDIProcess(List<Signal> n)
            => InvokeExit(n.SelectMany((i => {
                if (i.Index == 100) return new [] {i};

                if (Offset.Apply(i.Index, Data.GridMode, Data.Wrap, out int x, out int y, out int result)) {
                    i.Index = (byte)result;
                    return new [] {i};
                }

                return Enumerable.Empty<Signal>();
            })).ToList());

        public override void Dispose() {
            if (Disposed) return;

            Offset.Dispose();
            base.Dispose();
        }

        public abstract class OffsetUpdatedUndoEntry: PathUndoEntry<Move> {
            int ux, uy, rx, ry;

            protected abstract void Action(Offset item, int u, int r);

            protected override void UndoPath(params Move[] item)
                => Action(item[0].Offset, ux, uy);

            protected override void RedoPath(params Move[] item)
                => Action(item[0].Offset, rx, ry);
            
            public OffsetUpdatedUndoEntry(string kind, Move move, int ux, int uy, int rx, int ry)
            : base($"Move Offset {kind} Changed to {rx},{ry}", move) {
                this.ux = ux;
                this.uy = uy;
                this.rx = rx;
                this.ry = ry;
            }

            protected OffsetUpdatedUndoEntry(BinaryReader reader, int version)
            : base(reader, version) {
                this.ux = reader.ReadInt32();
                this.uy = reader.ReadInt32();
                this.rx = reader.ReadInt32();
                this.ry = reader.ReadInt32();
            }

            public override void Encode(BinaryWriter writer) {
                base.Encode(writer);

                writer.Write(ux);
                writer.Write(uy);
                writer.Write(rx);
                writer.Write(ry);
            }
        }
        
        public class OffsetRelativeUndoEntry: OffsetUpdatedUndoEntry {
            protected override void Action(Offset item, int x, int y) {
                item.Data.X = x;
                item.Data.Y = y;
            }
            
            public OffsetRelativeUndoEntry(Move move, int ux, int uy, int rx, int ry)
            : base("Relative", move, ux, uy, rx, ry) {}
            
            OffsetRelativeUndoEntry(BinaryReader reader, int version)
            : base(reader, version) {}
        }
        
        public class OffsetAbsoluteUndoEntry: OffsetUpdatedUndoEntry {
            protected override void Action(Offset item, int x, int y) {
                item.Data.AbsoluteX = x;
                item.Data.AbsoluteY = y;
            }
            
            public OffsetAbsoluteUndoEntry(Move move, int ux, int uy, int rx, int ry)
            : base("Absolute", move, ux, uy, rx, ry) {}
            
            OffsetAbsoluteUndoEntry(BinaryReader reader, int version)
            : base(reader, version) {}
        }

        public class OffsetSwitchedUndoEntry: SimplePathUndoEntry<Move, bool> {
            protected override void Action(Move item, bool element) => item.Offset.Data.IsAbsolute = element;
            
            public OffsetSwitchedUndoEntry(Move move, bool u, bool r)
            : base($"Move Offset Switched to {(r? "Absolute" : "Relative")}", move, u, r) {}
            
            OffsetSwitchedUndoEntry(BinaryReader reader, int version)
            : base(reader, version) {}
        }
        
        public class GridModeUndoEntry: EnumSimplePathUndoEntry<Move, GridType> {
            protected override void Action(Move item, GridType element) => item.Data.GridMode = element;
            
            public GridModeUndoEntry(Move move, GridType u, GridType r, IEnumerable source)
            : base("Move Grid", move, u, r, source) {}
            
            GridModeUndoEntry(BinaryReader reader, int version)
            : base(reader, version) {}
        }
        
        public class WrapUndoEntry: SimplePathUndoEntry<Move, bool> {
            protected override void Action(Move item, bool element) => item.Data.Wrap = element;
            
            public WrapUndoEntry(Move move, bool u, bool r)
            : base($"Move Wrap Changed to {(r? "Enabled" : "Disabled")}", move, u, r) {}
            
            WrapUndoEntry(BinaryReader reader, int version)
            : base(reader, version) {}
        }
    }
}