using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Apollo.DeviceViewers;
using Apollo.Elements;
using Apollo.Structures;
using Apollo.Undo;

namespace Apollo.Devices {
    public class ChokeData: DeviceData {
        ChokeViewer Viewer => Instance?.SpecificViewer<ChokeViewer>();

        int _target = 1;
        public int Target {
            get => _target;
            set {
                if (_target != value && 1 <= value && value <= 16) {
                   _target = value;

                   Viewer?.SetTarget(Target);
                }
            }
        }

        public ChainData Chain { get; private set; }

        public ChokeData(int target = 1, ChainData chain = null) {
            Target = target;
            Chain = chain?? new ChainData();
        }

        protected override DeviceData CloneSpecific()
            => new ChokeData(Target, Chain.Clone());

        protected override Device ActivateSpecific(DeviceData data)
            => new Choke((ChokeData)data);
    }

    public class Choke: Device, IChainParent {
        public new ChokeData Data => (ChokeData)Data;

        public delegate void ChokedEventHandler(Choke sender, int index);
        public static event ChokedEventHandler Choked;

        public readonly Chain Chain;

        bool choked = true;
        ConcurrentDictionary<(Launchpad, int, int), Signal> signals = new();

        void HandleChoke(Choke sender, int index) {
            if (Data.Target == index && sender != this && !choked) {
                choked = true;
                Chain.MIDIEnter(StopSignal.Instance);
                
                List<Signal> o = signals.Values.ToList();
                o.ForEach(i => i.Color = new Color(0));
                InvokeExit(o);

                signals.Clear();
            }
        }

        public Choke(ChokeData data): base(data?? new(), "choke") {
            Chain = data.Chain.Activate();

            Chain.Parent = this;
            Chain.ParentIndex = 0;
            Chain.MIDIExit = ChainExit;

            Choked += HandleChoke;
        }

        void ChainExit(List<Signal> n) {
            if (choked) return;

            InvokeExit(n);
            
            n.ForEach(i => {
                (Launchpad, int, int) index = (i.Source, i.Index, -i.Layer);
                if (i.Color.Lit) signals[index] = i.Clone();
                else if (signals.ContainsKey(index)) signals.TryRemove(index, out _);
            });
        }

        public override void MIDIProcess(List<Signal> n) {
            IEnumerable<Signal> o = n;

            if (choked) {
                IEnumerable<Signal> m = n.SkipWhile(i => !i.Color.Lit);

                if (m.Any()) {
                    Choked?.Invoke(this, Data.Target);
                    choked = false;

                    o = m;
                }
            }

            if (!choked) Chain.MIDIEnter(o.Select(i => i.Clone()).ToList());
        }
        
        protected override void Stopped() {
            Chain.MIDIEnter(StopSignal.Instance);

            signals.Clear();
            choked = true;
        }

        public override void Dispose() {
            if (Disposed) return;

            Choked -= HandleChoke;

            Chain.Dispose();
            base.Dispose();
        }
        
        public class TargetUndoEntry: SimplePathUndoEntry<Choke, int> {
            protected override void Action(Choke item, int element) => item.Data.Target = element;

            public TargetUndoEntry(Choke choke, int u, int r)
            : base($"Choke Target Changed to {r}", choke, u, r) {}
            
            TargetUndoEntry(BinaryReader reader, int version)
            : base(reader, version) {}
        }
    }
}