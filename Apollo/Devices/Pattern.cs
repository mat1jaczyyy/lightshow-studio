using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Avalonia.Controls;

using Apollo.Binary;
using Apollo.Elements;
using Apollo.Enums;
using Apollo.Helpers;
using Apollo.Selection;
using Apollo.Structures;
using Apollo.Undo;
using Apollo.Windows;
using Apollo.Rendering;

namespace Apollo.Devices {
    public class Pattern: Device, ISelectParent {
        public ISelectParentViewer IViewer {
            get => Window;
        }

        public List<ISelect> IChildren {
            get => Frames.Select(i => (ISelect)i).ToList();
        }

        public bool IRoot {
            get => true;
        }

        public void IInsert(int index, ISelect item) => Insert(index, (Frame)item);
        
        public Window IWindow => Window;
        public SelectionManager Selection => Window?.Selection;

        public Type ChildType => typeof(Frame);
        public string ChildString => "Frame";
        public string ChildFileExtension => null;

        public PatternWindow Window;

        List<Frame> _frames;
        public List<Frame> Frames {
            get => _frames;
            set {
                if (value != null && value.Count != 0) {
                    _frames = value;

                    Reroute();

                    Window?.RecreateFrames();
                }
            }
        }

        void Reroute() {
            for (int i = 0; i < Frames.Count; i++) {
                Frames[i].Parent = this;
                Frames[i].ParentIndex = i;
            }
            
            Window?.SetInfinite(Infinite);
        }

        public Frame this[int index] {
            get => Frames[index];
        }

        public int Count {
            get => Frames.Count;
        }
        
        public void Insert(int index, Frame frame) {
            Frames.Insert(index, frame);
            Reroute();

            Window?.Contents_Insert(index, Frames[index]);
            
            Window?.Selection.Select(frame);
            Window?.Frame_Select(index);
        }

        public void Remove(int index, bool dispose = true) {
            Window?.Selection.Select(null);
            Window?.Contents_Remove(index);

            if (dispose) Frames[index].Dispose();
            Frames.RemoveAt(index);
            Reroute();

            Window?.Frame_Select(Expanded);
            Window?.Selection.Select(Frames[Expanded]);
        }

        double _gate;
        public double Gate {
            get => _gate;
            set {
                if (0.01 <= value && value <= 4) {
                    _gate = value;
                    
                    Window?.SetGate(Gate);
                }
            }
        }

        double _pinch;
        public double Pinch {
            get => _pinch;
            set {
                if (-2 <= value && value <= 2) {
                    _pinch = value;
                    
                    Window?.SetPinch(_pinch);
                }
            }
        }

        bool _bilateral;
        public bool Bilateral {
            get => _bilateral;
            set {
                if (_bilateral != value) {
                    _bilateral = value;

                    Window?.SetBilateral(_bilateral);
                }
            }
        }

        public double ApplyPinch(double time) => Pincher.ApplyPinch(
            time,
            Enumerable.Sum(Frames.Select(i => (double)i.Time)) * AdjustedRepeats * Gate,
            Pinch,
            Bilateral
        );

        PlaybackType _mode;
        public PlaybackType Mode {
            get => _mode;
            set {
                _mode = value;

                Window?.SetPlaybackMode(Mode);

                Stop();
            }
        }

        bool _infinite;
        public bool Infinite {
            get => _infinite;
            set {
                if (_infinite != value) {
                    _infinite = value;

                    Window?.SetInfinite(_infinite);
                }
            }
        }

        int _repeats;
        public int Repeats {
            get => _repeats;
            set {
                if (_repeats != value && 1 <= value && value <= 128) {
                    _repeats = value;

                    Window?.SetRepeats(_repeats);
                }
            }
        }
        public int AdjustedRepeats => (Mode == PlaybackType.Loop || _infinite)? 1 : Repeats;

        int? _root;
        public int? RootKey {
            get => _root;
            set {
                if (_root != value && value != 100) {
                    _root = value;

                    Window?.SetRootKey(_root);
                }
            }
        }

        bool _wrap;
        public bool Wrap {
            get => _wrap;
            set {
                _wrap = value;

                Window?.SetWrap(Wrap);
            }
        }
        
        public override Device Clone() => new Pattern(Repeats, Gate, Pinch, Bilateral, Frames.Select(i => i.Clone()).ToList(), Mode, Infinite, RootKey, Wrap, Expanded) {
            Collapsed = Collapsed,
            Enabled = Enabled
        };

        int _expanded;
        public int Expanded {
            get => _expanded;
            set {
                if (!(0 <= value && value < Frames.Count)) value = 0;
                _expanded = value;                
            }
        }

        public Pattern(int repeats = 1, double gate = 1, double pinch = 0, bool bilateral = false, List<Frame> frames = null, PlaybackType mode = PlaybackType.Mono, bool infinite = false, int? root = null, bool wrap = false, int expanded = 0): base("pattern") {
            if (frames == null || frames.Count == 0) frames = new List<Frame>() {new Frame()};

            Repeats = repeats;
            Gate = gate;
            Pinch = pinch;
            Bilateral = bilateral;
            Frames = frames;
            Mode = mode;
            Infinite = infinite;
            RootKey = root;
            Wrap = wrap;
            Expanded = expanded;

            Reroute();
        }

        bool ApplyRootKey(int index, int trigger, out int result) {
            if (RootKey == null) {
                result = index;
                return true;
            }

            int x = index % 10 + trigger % 10 - RootKey.Value % 10;
            int y = index / 10 + trigger / 10 - RootKey.Value / 10;

            if (Wrap) {
                x = (x + 10) % 10;
                y = (y + 10) % 10;
            }

            result = y * 10 + x;

            if (0 <= x && x <= 9 && 0 <= y && y <= 9)
                return true;
            
            if (y == -1 && 4 <= x && x <= 5) {
                result = 100;
                return true;
            }
             
            return false;
        }
        
        void ScheduleWithPinch(Action Next, int index, ref double start, ref double total) {
            double time = Frames[index % Frames.Count].Time;

            total += time * _gate;
            Schedule(Next, start += (ApplyPinch(total) - ApplyPinch(total - time * _gate)));
        }
        
        class PlayingState {
            public Signal Root;
            public int Frame = 0;
            
            public PlayingState(Signal init) => Root = init;
        }
        
        ConcurrentDictionary<Signal, PlayingState> buffer = new();
        ConcurrentHashSet<PlayingState> poly = new();
        
        void Stop(Signal k, Signal r, List<Signal> ret) {
            if (buffer.ContainsKey(k))
                if (buffer[k].Frame < Frames.Count * AdjustedRepeats - Convert.ToInt32(Infinite) && !(buffer[k].Root is null)) 
                    RenderFrame(ret, buffer[k].Frame, buffer[k].Root, false);
            
            buffer[k] = new PlayingState(r);
        }

        public override void MIDIProcess(List<Signal> n) {
            if (Frames.Count == 0 || !n.Any()) return;
            
            List<Signal> ret = new();
            
            Signal root = n.LastOrDefault(i => i.Color.Lit)?.Clone();
            
            n = n.Select(s => {
                bool lit = s.Color.Lit;

                Signal i = s.With(color: new Color());
                i.HashIndex = false;
                
                if ((Mode == PlaybackType.Mono && lit) || Mode == PlaybackType.Loop)
                    Stop(s, root, ret);

                return i;
            }).ToList();
            
            if (!(root is null)) {
                RenderFrame(ret, 0, root);
                
                if (Mode == PlaybackType.Poly) {
                    foreach (Signal polyRoot in n.Where(i => i.Color.Lit)) {
                        PlayingState state = new PlayingState(polyRoot);
            
                        double start = Heaven.Time;
                        double total = 0;
                        
                        poly.Add(state);
                        
                        void Next() {
                            if (!poly.Contains(state)) return;
                            
                            List<Signal> ret = new();
                           
                            if (++state.Frame < Frames.Count * AdjustedRepeats) {
                                RenderFrameDifference(ret, state.Frame, state.Root);
                                
                                ScheduleWithPinch(Next, state.Frame, ref start, ref total);

                            } else {
                                poly.Remove(state);
                                
                                if (!Infinite)
                                    RenderFrame(ret, state.Frame - 1, state.Root, false);
                            }
                            
                            InvokeExit(ret);
                        }
                        
                        ScheduleWithPinch(Next, 0, ref start, ref total);
                    }

                } else {
                    double start = Heaven.Time;
                    double total = 0;
                    
                    void Next() {
                        if (!ReferenceEquals(buffer[root].Root, root)) return;
                        
                        List<Signal> ret = new();
                        
                        if (++buffer[root].Frame < Frames.Count * AdjustedRepeats) {
                            RenderFrameDifference(ret, buffer[root].Frame, root);
                            
                            ScheduleWithPinch(Next, buffer[root].Frame, ref start, ref total);

                        } else if (Mode == PlaybackType.Mono) {
                            if (!Infinite)
                                RenderFrame(ret, buffer[root].Frame - 1, root, false);
                                
                        } else if (Mode == PlaybackType.Loop) {
                            if (Infinite) RenderFrame(ret, 0, root);
                            else RenderFrameDifference(ret, 0, root);
                            
                            if (ReferenceEquals(buffer[root].Root, root)) buffer[root].Frame = 0;
                            start = Heaven.Time;
                            total = 0;
                            
                            ScheduleWithPinch(Next, 0, ref start, ref total);
                        }
                        
                        InvokeExit(ret);
                    }
                    
                    ScheduleWithPinch(Next, 0, ref start, ref total);
                }
            }
            
            InvokeExit(ret);
        }

        protected override void Stopped() {
            buffer.Clear();
            poly.Clear();
        }

        public override void Dispose() {
            if (Disposed) return;

            Stop();

            Window?.Close();
            Window = null;

            foreach (Frame frame in Frames) frame.Dispose();
            base.Dispose();
        }
        
        void RenderFrame(List<Signal> list, int n, Signal s, bool on = true)
            => list.AddRange(Frames[n % Frames.Count].Screen.SelectMany((f, i) =>
                (f.Lit && ApplyRootKey(i, s.Index, out int index))
                    ? new [] {s.With((byte)index, on? f.Clone() : new Color(0))}
                    : Enumerable.Empty<Signal>()
            ));

        void RenderFrameDifference(List<Signal> list, int n, Signal s)
            => list.AddRange(Frames[n % Frames.Count].Screen.Zip(Frames[(n + Frames.Count - 1) % Frames.Count].Screen).SelectMany((f, i) =>
                (f.First != f.Second && ApplyRootKey(i, s.Index, out int index))
                    ? new [] {s.With((byte)index, f.First.Clone())}
                    : Enumerable.Empty<Signal>()
            ));
        
        public class FrameInsertedUndoEntry: PathUndoEntry<Pattern> {
            int index;
            Frame frame;

            protected override void UndoPath(params Pattern[] items) => items[0].Remove(index);
            protected override void RedoPath(params Pattern[] items) => items[0].Insert(index, frame.Clone());
            
            protected override void OnDispose() => frame.Dispose();
            
            public FrameInsertedUndoEntry(Pattern pattern, int index, Frame frame)
            : base($"Pattern Frame {index + 1} Inserted", pattern) {
                this.index = index;
                this.frame = frame.Clone();
            }
            
            FrameInsertedUndoEntry(BinaryReader reader, int version)
            : base(reader, version) {
                index = reader.ReadInt32();
                frame = Decoder.Decode<Frame>(reader, version);
            }
            
            public override void Encode(BinaryWriter writer) {
                base.Encode(writer);
                
                writer.Write(index);
                Encoder.Encode(writer, frame);
            }
        }
        
        public class FrameChangedUndoEntry: SimpleIndexPathUndoEntry<Pattern, Color[]> {
            static Color[] Clone(Color[] arr) => arr.Select(i => i.Clone()).ToArray();

            protected override void Action(Pattern item, int index, Color[] element) => item[index].Screen = Clone(element);
            
            public FrameChangedUndoEntry(Pattern pattern, int index, Color[] u)
            : base($"Pattern Frame {index + 1} Changed", pattern, index, Clone(u), Clone(pattern[index].Screen)) {}
            
            FrameChangedUndoEntry(BinaryReader reader, int version)
            : base(reader, version) {}
        }
        
        public class InfiniteUndoEntry: SimplePathUndoEntry<Pattern, bool> {
            protected override void Action(Pattern item, bool element) => item.Infinite = element;
            
            public InfiniteUndoEntry(Pattern pattern, bool u, bool r)
            : base($"Pattern Infinite Changed to {(r? "Enabled" : "Disabled")}", pattern, u, r) {}
            
            InfiniteUndoEntry(BinaryReader reader, int version)
            : base(reader, version) {}
        }
        
        public abstract class DurationUndoEntry<I>: PathUndoEntry<Pattern> {
            int left;
            List<Time> u;
            I r;

            protected abstract void SetValue(Time item, I element);

            protected override void UndoPath(params Pattern[] items) {
                for (int i = 0; i < u.Count; i++)
                    items[0][left + i].Time = u[i].Clone();
            }

            protected override void RedoPath(params Pattern[] items) {
                for (int i = 0; i < u.Count; i++)
                    SetValue(items[0][left + i].Time, r);
            }
            
            protected override void OnDispose() {
                foreach (Time time in u) time.Dispose();
                u = null;
            }
            
            public DurationUndoEntry(string desc, Pattern pattern, int left, List<Time> u, I r)
            : base(desc, pattern) {
                this.left = left;
                this.u = u.Select(i => i.Clone()).ToList();
                this.r = r;
            }
        
            protected DurationUndoEntry(BinaryReader reader, int version): base(reader, version) {
                left = reader.ReadInt32();
                u = Enumerable.Range(0, reader.ReadInt32()).Select(i => Decoder.Decode<Time>(reader, version)).ToList();
                r = Decoder.DecodeAnything<I>(reader, version);
            }
            
            public override void Encode(BinaryWriter writer) {
                base.Encode(writer);
                
                writer.Write(left);
                
                writer.Write(u.Count);
                for (int i = 0; i < u.Count; i++)
                    Encoder.Encode(writer, u[i]);
                
                Encoder.EncodeAnything<I>(writer, r);
            }
        }

        public class DurationValueUndoEntry: DurationUndoEntry<int> {
            protected override void SetValue(Time item, int element) {
                item.Free = element;
                item.Mode = false;
            }

            public DurationValueUndoEntry(Pattern pattern, int left, List<Time> u, int r)
            : base($"Pattern Frame {pattern.Expanded + 1} Duration Changed to {r}ms", pattern, left, u, r) {}
            
            DurationValueUndoEntry(BinaryReader reader, int version)
            : base(reader, version) {}
        }

        public class DurationStepUndoEntry: DurationUndoEntry<int> {
            protected override void SetValue(Time item, int element) {
                item.Length.Step = element;
                item.Mode = true;
            }

            public DurationStepUndoEntry(Pattern pattern, int left, List<Time> u, int r)
            : base($"Pattern Frame {pattern.Expanded + 1} Duration Changed to {Length.Steps[r]}", pattern, left, u, r) {}
            
            DurationStepUndoEntry(BinaryReader reader, int version)
            : base(reader, version) {}
        }

        public class DurationModeUndoEntry: DurationUndoEntry<bool> {
            protected override void SetValue(Time item, bool element) => item.Mode = element;

            public DurationModeUndoEntry(Pattern pattern, int left, List<Time> u, bool r)
            : base($"Pattern Frame {pattern.Expanded + 1} Duration Switched to {(r? "Steps" : "Free")}", pattern, left, u, r) {}
            
            DurationModeUndoEntry(BinaryReader reader, int version)
            : base(reader, version) {}
        }
        
        public class FrameReversedUndoEntry: SymmetricPathUndoEntry<Pattern> {
            int index, left, right;

            protected override void Action(Pattern item) {
                if (item.Window != null) item.Window.Draw = false;

                for (int i = left; i < right; i++) {
                    Frame frame = item[right];
                    item.Remove(right, false);
                    item.Insert(i, frame);
                }

                if (item.Window != null) {
                    item.Window.Draw = true;

                    item.Window.Frame_Select(index);
                    item.Window.Selection.Select(item[left]);
                    item.Window.Selection.Select(item[right], true);
                }
            }
            
            public FrameReversedUndoEntry(Pattern pattern, int index, int left, int right)
            : base($"Pattern Frames Reversed", pattern) {
                this.index = index;
                this.left = left;
                this.right = right;
            }
        
            FrameReversedUndoEntry(BinaryReader reader, int version)
            : base(reader, version) {
                index = reader.ReadInt32();
                left = reader.ReadInt32();
                right = reader.ReadInt32();
            }
            
            public override void Encode(BinaryWriter writer) {
                base.Encode(writer);
                
                writer.Write(index);
                writer.Write(left);
                writer.Write(right);
            }
        }
        
        public class FrameInvertedUndoEntry: SymmetricPathUndoEntry<Pattern> {
            int left, right;

            protected override void Action(Pattern item) {
                for (int i = left; i <= right; i++)
                    item[i].Invert();
            }
            
            public FrameInvertedUndoEntry(Pattern pattern, int left, int right)
            : base($"Pattern Frames Inverted", pattern) {
                this.left = left;
                this.right = right;
            }
            
            FrameInvertedUndoEntry(BinaryReader reader, int version)
            : base(reader, version) {
                left = reader.ReadInt32();
                right = reader.ReadInt32();
            }
            
            public override void Encode(BinaryWriter writer) {
                base.Encode(writer);
                
                writer.Write(left);
                writer.Write(right);
            }
        }
        
        public class PlaybackModeUndoEntry: SimplePathUndoEntry<Pattern, PlaybackType> {
            protected override void Action(Pattern item, PlaybackType element) => item.Mode = element;
            
            public PlaybackModeUndoEntry(Pattern pattern, PlaybackType u, PlaybackType r)
            : base($"Pattern Playback Mode Changed to {r}", pattern, u, r) {}
            
            PlaybackModeUndoEntry(BinaryReader reader, int version)
            : base(reader, version) {}
        }
        
        public class GateUndoEntry: SimplePathUndoEntry<Pattern, double> {
            protected override void Action(Pattern item, double element) => item.Gate = element;
            
            public GateUndoEntry(Pattern pattern, double u, double r)
            : base($"Pattern Gate Changed to {r}%", pattern, u / 100, r / 100) {}
            
            GateUndoEntry(BinaryReader reader, int version)
            : base(reader, version) {}
        }
        
        public class RepeatsUndoEntry: SimplePathUndoEntry<Pattern, int> {
            protected override void Action(Pattern item, int element) => item.Repeats = element;
            
            public RepeatsUndoEntry(Pattern pattern, int u, int r)
            : base($"Pattern Repeats Changed to {r}", pattern, u, r) {}
            
            RepeatsUndoEntry(BinaryReader reader, int version)
            : base(reader, version) {}
        }
        
        public class PinchUndoEntry: SimplePathUndoEntry<Pattern, double> {
            protected override void Action(Pattern item, double element) => item.Pinch = element;
            
            public PinchUndoEntry(Pattern pattern, double u, double r)
            : base($"Pattern Pinch Changed to {r}", pattern, u, r) {}
            
            PinchUndoEntry(BinaryReader reader, int version)
            : base(reader, version) {}
        }
        
        public class BilateralUndoEntry: SimplePathUndoEntry<Pattern, bool> {
            protected override void Action(Pattern item, bool element) => item.Bilateral = element;
            
            public BilateralUndoEntry(Pattern pattern, bool u, bool r)
            : base($"Pattern Bilateral Changed to {(r? "Enabled" : "Disabled")}", pattern, u, r) {}
            
            BilateralUndoEntry(BinaryReader reader, int version)
            : base(reader, version) {}
        }
        
        public class RootKeyUndoEntry: SimplePathUndoEntry<Pattern, int?> {
            protected override void Action(Pattern item, int? element) => item.RootKey = element;
            
            public RootKeyUndoEntry(Pattern pattern, int? u, int? r)
            : base($"Pattern Root Key Changed", pattern, u, r) {}
            
            RootKeyUndoEntry(BinaryReader reader, int version)
            : base(reader, version) {}
        }
        
        public class WrapUndoEntry: SimplePathUndoEntry<Pattern, bool> {
            protected override void Action(Pattern item, bool element) => item.Wrap = element;
            
            public WrapUndoEntry(Pattern pattern, bool u, bool r)
            : base($"Pattern Wrap Changed to {(r? "Enabled" : "Disabled")}", pattern, u, r) {}
            
            WrapUndoEntry(BinaryReader reader, int version)
            : base(reader, version) {}
        }
        
        public class ImportUndoEntry: SimplePathUndoEntry<Pattern, Pattern> {
            protected override void Action(Pattern item, Pattern element) {
                item.Repeats = element.Repeats;
                item.Gate = element.Gate;
                item.Pinch = element.Pinch;
                item.Bilateral = element.Bilateral;
                item.Frames = element.Frames.Select(i => i.Clone()).ToList();
                item.Mode = element.Mode;
                item.Infinite = element.Infinite;
                item.RootKey = element.RootKey;
                item.Wrap = element.Wrap;
                item.Expanded = element.Expanded;
            }

            protected override void OnDispose(Pattern undo, Pattern redo) {
                undo.Dispose();
                redo.Dispose();
            }
            
            public ImportUndoEntry(Pattern pattern, string filename, Pattern r)
            : base($"Pattern File Imported from {filename}", pattern, (Pattern)pattern.Clone(), r) {}
            
            ImportUndoEntry(BinaryReader reader, int version)
            : base(reader, version) {}
        }
    }
}