using System.Diagnostics;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

using Apollo.Core;
using Apollo.DeviceViewers;
using Apollo.Elements;
using Apollo.Structures;
using Apollo.Undo;

namespace Apollo.Devices {
    public class OutputData: DeviceData {
        public OutputViewer Viewer => Instance?.SpecificViewer<OutputViewer>();

        int _target;
        public int Target {
            get => _target;
            set {
                _target = value;
                
                Viewer?.SetTarget(Target);
            }
        }

        public OutputData(int target = 0)
            => Target = target;

        protected override DeviceData CloneSpecific()
            => new OutputData();

        protected override Device ActivateSpecific(DeviceData data)
            => new Output((OutputData)data);
    }

    public class Output: Device {
        public new OutputData Data => (OutputData)Data;

        void IndexChanged(int value) {
            if (Track.Get(this)?.IsDisposing != false) return;

            Data.Target = value;
        }

        void IndexRemoved() {
            Track owner = Track.Get(this);

            if (Program.Project.IsDisposing || owner?.IsDisposing != false || owner?.ParentIndex == null) return;

            bool redoing = false;

            foreach (StackFrame call in new StackTrace().GetFrames()) {
                MethodBase method = call.GetMethod();
                if (redoing = method.DeclaringType == typeof(UndoManager) && method.Name == "Select") break;
            }

            if (!redoing)
                Program.Project.Undo.History.Last().AddPost(new IndexRemovedFix(this, Data.Target));

            Data.Target = owner.ParentIndex.Value;
        }

        public Output(OutputData data): base(data?? new(), "output") {
            if (data == null)
                Data.Target = Track.Get(this).ParentIndex.Value;
        }

        public override void MIDIProcess(List<Signal> n) {
            n.ForEach(i => i.Source = Program.Project.Tracks[Data.Target].Launchpad);
            InvokeExit(n);
        }
        
        public class TargetUndoEntry: SimplePathUndoEntry<Output, int> {
            protected override void Action(Output item, int element) => item.Data.Target = element;
            
            public TargetUndoEntry(Output output, int u, int r)
            : base($"Output Target Changed to {r}", output, u - 1, r - 1) {}
            
            TargetUndoEntry(BinaryReader reader, int version)
            : base(reader, version) {}
        }

        public class IndexRemovedFix: PathUndoEntry<Output> {
            int target;

            protected override void UndoPath(params Output[] items) => items[0].Data.Target = target;

            protected override void OnRedo() {}

            public IndexRemovedFix(Output output, int target)
            : base("Output Index Removed Fix", output) => this.target = target;
            
            IndexRemovedFix(BinaryReader reader, int version)
            : base(reader, version) => target = reader.ReadInt32();
            
            public override void Encode(BinaryWriter writer) {
                base.Encode(writer);
                
                writer.Write(target);
            }
        }
    }
}