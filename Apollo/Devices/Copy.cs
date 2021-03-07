using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Apollo.Binary;
using Apollo.DeviceViewers;
using Apollo.Elements;
using Apollo.Enums;
using Apollo.Helpers;
using Apollo.Rendering;
using Apollo.Structures;
using Apollo.Undo;

namespace Apollo.Devices {
    public class CopyData: DeviceData {
        public CopyViewer Viewer => Instance?.SpecificViewer<CopyViewer>();
        
        public List<OffsetData> Offsets;
        public List<int> Angles;

        public TimeData Time;

        double _gate;
        public double Gate {
            get => _gate;
            set {
                if (0.01 <= value && value <= 4) {
                    _gate = value;
                    
                    Viewer?.SetGate(Gate);
                }
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

        CopyType _copymode;
        public CopyType CopyMode {
            get => _copymode;
            set {
                _copymode = value;
                
                Viewer?.SetCopyMode(CopyMode);

                Instance?.Stop();
            }
        }

        GridType _gridmode;
        public GridType GridMode {
            get => _gridmode;
            set {
                _gridmode = value;
                
                Viewer?.SetGridMode(GridMode);
            }
        }
        
        double _pinch;
        public double Pinch {
            get => _pinch;
            set {
                if (-2 <= value && value <= 2) {
                    _pinch = value;
                    
                    Viewer?.SetPinch(Pinch);
                }
            }
        }
        
        bool _bilateral;
        public bool Bilateral {
            get => _bilateral;
            set {
                if (value != _bilateral) {
                    _bilateral = value;
                    
                    Viewer?.SetBilateral(Bilateral);
                }
            }
        }

        bool _reverse;
        public bool Reverse {
            get => _reverse;
            set {
                _reverse = value;

                Viewer?.SetReverse(Reverse);
            }
        }
        
        bool _infinite;
        public bool Infinite {
            get => _infinite;
            set {
                _infinite = value;

                Viewer?.SetInfinite(Infinite);
            }
        }

        public CopyData(TimeData time = null, double gate = 1, double pinch = 0, bool bilateral = false, bool reverse = false, bool infinite = false, CopyType copymode = CopyType.Static, GridType gridmode = GridType.Full, bool wrap = false, List<OffsetData> offsets = null, List<int> angles = null) {
            Time = time?? new TimeData(free: 500);
            Gate = gate;
            Pinch = pinch;
            Bilateral = bilateral;

            Reverse = reverse;
            Infinite = infinite;

            CopyMode = copymode;
            GridMode = gridmode;
            Wrap = wrap;

            Offsets = offsets?? new List<OffsetData>();
            Angles = angles?? new List<int>();
        }

        protected override DeviceData CloneSpecific()
            => new CopyData(Time.Clone(), _gate, Pinch, Bilateral, Reverse, Infinite, CopyMode, GridMode, Wrap, Offsets.Select(i => i.Clone()).ToList(), Angles.ToList());

        protected override Device ActivateSpecific(DeviceData data)
            => new Copy((CopyData)data);
    }

    public class Copy: Device {
        public new CopyData Data => (CopyData)Data;

        readonly Random RNG = new Random();

        public List<Offset> Offsets;

        public void Insert(int index, OffsetData offset = null, int angle = 0) {
            Data.Offsets.Insert(index, offset?? new OffsetData());
            Offsets.Insert(index, Data.Offsets[index].Activate());
            Offsets[index].Changed += OffsetChanged;
            
            Data.Angles.Insert(index, Math.Clamp(angle, -150, 150));

            Data.Viewer?.Contents_Insert(index, Offsets[index], Data.Angles[index]);
        }

        public void Remove(int index) {
            Data.Viewer?.Contents_Remove(index);

            Offsets[index].Changed -= OffsetChanged;
            Offsets.RemoveAt(index);
            Data.Offsets.RemoveAt(index);

            Data.Angles.RemoveAt(index);
        }

        void OffsetChanged(Offset sender)
            => Data.Viewer?.SetOffset(Offsets.IndexOf(sender), sender);

        public int GetAngle(int index) => Data.Angles[index];
        public void SetAngle(int index, int angle) {
            if (-150 <= angle && angle <= 150 && angle != Data.Angles[index]) {
                Data.Angles[index] = angle;

                Data.Viewer?.SetOffsetAngle(index, angle);
            }
        }

        public readonly Time Time;

        void FreeChanged(int value)
            => Data.Viewer?.SetRateValue(value);

        void ModeChanged(bool value)
            => Data.Viewer?.SetMode(value);

        void StepChanged(Length value)
            => Data.Viewer?.SetRateStep(value);

        public Copy(CopyData data = null): base(data?? new(), "copy") {
            Offsets = Data.Offsets.Select(i => i.Activate()).ToList();

            foreach (Offset offset in Offsets)
                offset.Changed += OffsetChanged;

            Time = Data.Time.Activate();

            Time.Minimum = 1;
            Time.Maximum = 5000;

            Time.FreeChanged += FreeChanged;
            Time.ModeChanged += ModeChanged;
            Time.StepChanged += StepChanged;
        }

        record DoubleTuple(double X, double Y) {
            public IntTuple Round() => new IntTuple((int)Math.Round(X), (int)Math.Round(Y));

            public static DoubleTuple operator *(DoubleTuple t, double f) => new DoubleTuple(t.X * f, t.Y * f);
            public static DoubleTuple operator +(DoubleTuple a, DoubleTuple b) => new DoubleTuple(a.X + b.X, a.Y + b.Y);
        };

        record IntTuple(int X, int Y) {
            public IntTuple Apply(Func<int, int> action) => new IntTuple(
                action.Invoke(X),
                action.Invoke(Y)
            );
        }
        
        DoubleTuple CircularInterp(DoubleTuple center, double radius, double startAngle, double endAngle, double t, double pointCount) {
            double angle = startAngle + (endAngle - startAngle) * ((double)t / pointCount);

            return new DoubleTuple(
                center.X + radius * Math.Cos(angle),
                center.Y + radius * Math.Sin(angle)
            );
        }
        
        DoubleTuple LineGenerator(IntTuple p, IntTuple a, IntTuple b, int t) => (a.X > a.Y)
            ? new DoubleTuple(
                p.X + t * b.X,
                p.Y + (int)Math.Round((double)t / a.X * a.Y) * b.Y
            )
            : new DoubleTuple(
                p.X + (int)Math.Round((double)t / a.Y * a.X) * b.X,
                p.Y + t * b.Y
            );
        
        ConcurrentDictionary<Signal, int> buffer = new();
        ConcurrentDictionary<Signal, int> screen = new();
        ConcurrentDictionary<Signal, object> locker = new();
        ConcurrentHashSet<Signal> offbuf = new();
        
        void ScreenOutput(IEnumerable<Signal> n, bool output = true) {
            List<Signal> data = n.SelectMany(s => {
                Signal m = s.With(s.Index, new Color());
                
                if (!locker.ContainsKey(m))
                    locker[m] = new object();

                lock (locker[m]) {
                    if (!screen.ContainsKey(m))
                        screen[m] = 0;
                    
                    if (s.Color.Lit) {
                        screen[m]++;
                        return new [] {s};

                    } else {
                        screen[m]--;
                        if (screen[m] <= 0) return new [] {s};
                    }
                }
                
                return Enumerable.Empty<Signal>();
            }).ToList();

            if (output) InvokeExit(data);
        }

        double RealTime => Time * Data.Gate;

        IEnumerable<Signal> CopyCalc(Signal s, bool output = true) {
            int px = s.Index % 10;
            int py = s.Index / 10;

            List<int> validOffsets = new() {s.Index};

            for (int i = 0; i < Offsets.Count; i++) {
                if (Offsets[i].Apply(s.Index, Data.GridMode, Data.Wrap, out int _x, out int _y, out int result) && Data.CopyMode != CopyType.Interpolate)
                    validOffsets.Add(result);

                if (Data.CopyMode == CopyType.Interpolate) {
                    double angle = Data.Angles[i] / 90.0 * Math.PI;
                    
                    double x = _x;
                    double y = _y;
                    
                    int pointCount;
                    Func<int, DoubleTuple> pointGenerator;

                    DoubleTuple source = new DoubleTuple(px, py);
                    DoubleTuple target = new DoubleTuple(_x, _y);

                    if (angle != 0) {
                        // https://www.desmos.com/calculator/hizsxmojxz

                        double diam = Math.Sqrt(Math.Pow(px - x, 2) + Math.Pow(py - y, 2));
                        double commonTan = Math.Atan((px - x) / (y - py));

                        double cord = diam / (2 * Math.Tan(Math.PI - angle / 2)) * (((y - py) >= 0)? 1 : -1);
                        
                        DoubleTuple center = new DoubleTuple(
                            (px + x) / 2 + Math.Cos(commonTan) * cord,
                            (py + y) / 2 + Math.Sin(commonTan) * cord
                        );
                        
                        double radius = diam / (2 * Math.Sin(Math.PI - angle / 2));
                        
                        double u = (Convert.ToInt32(angle < 0) * (Math.PI + angle) + Math.Atan2(py - center.Y, px - center.X)) % (2 * Math.PI);
                        double v = (Convert.ToInt32(angle < 0) * (Math.PI - angle) + Math.Atan2(y - center.Y, x - center.X)) % (2 * Math.PI);
                        v += (u <= v)? 0 : 2 * Math.PI;
                        
                        double startAngle = (angle < 0)? v : u;
                        double endAngle = (angle < 0)? u : v;

                        pointCount = (int)(Math.Abs(radius) * Math.Abs(endAngle - startAngle) * 1.5);
                        pointGenerator = t => CircularInterp(center, radius, startAngle, endAngle, t, pointCount);
                        
                    } else {
                        IntTuple p = new IntTuple(px, py);

                        IntTuple d = new IntTuple(
                            _x - px,
                            _y - py
                        );

                        IntTuple a = d.Apply(v => Math.Abs(v));
                        IntTuple b = d.Apply(v => (v < 0)? -1 : 1);

                        pointCount = Math.Max(a.X, a.Y);
                        pointGenerator = t => LineGenerator(p, a, b, t);
                    }
                        
                    for (int p = 1; p <= pointCount; p++) {
                        DoubleTuple doublepoint = pointGenerator.Invoke(p);
                        IntTuple point = doublepoint.Round();

                        if (Math.Pow(doublepoint.X - point.X, 2.16) + Math.Pow(doublepoint.Y - point.Y, 2.16) > .25) continue;

                        bool valid = Offset.Validate(point.X, point.Y, Data.GridMode, Data.Wrap, out int iresult);

                        if (iresult != validOffsets.Last())
                            validOffsets.Add(valid? iresult : -1);
                    }
                }

                px = _x;
                py = _y;
            }
                    
            double start = Heaven.Time;
                    
            switch (Data.CopyMode) {
                case CopyType.Static:
                    return validOffsets.Select(offset => s.With((byte)offset));

                case CopyType.Animate:
                case CopyType.Interpolate:
                    if (Data.Reverse) validOffsets.Reverse();
                    
                    int index = 0;
                    double total = RealTime * (validOffsets.Count - 1);

                    void Next() {
                        index++;
                        if (index < validOffsets.Count) {
                            if (index < validOffsets.Count - 1) Schedule(Next, start += (
                                Pincher.ApplyPinch(RealTime * (index + 1), total, Data.Pinch, Data.Bilateral) -
                                Pincher.ApplyPinch(RealTime * (index), total, Data.Pinch, Data.Bilateral)
                            ));
                            
                            if (validOffsets[index] == -1 || !(!Data.Infinite || index < validOffsets.Count - 1 || s.Color.Lit)) return;
                            
                            ScreenOutput(new [] {s.With((byte)validOffsets[index])}, output);
                        }
                    };

                    Schedule(Next, start += Pincher.ApplyPinch(RealTime, total, Data.Pinch, Data.Bilateral));
                    return new [] {s.Clone()};
                
                case CopyType.RandomSingle:
                    Signal m = s.Clone();
                    s.Color = new Color();
                    
                    if (!buffer.ContainsKey(s)) {
                        if (!m.Color.Lit) break;
                        buffer[s] = m.Index = (byte)validOffsets[RNG.Next(validOffsets.Count)];

                    } else {
                        m.Index = (byte)buffer[s];
                        if (!m.Color.Lit) buffer.Remove(s, out _);
                    }
                    
                    return new [] {m};
                
                case CopyType.RandomLoop:
                    Signal k = s.With(color: new Color());
                    Signal v;
                    
                    if (!buffer.ContainsKey(k)) {
                        if (!s.Color.Lit) break;

                        buffer[k] = RNG.Next(validOffsets.Count);
                        v = s.With((byte)validOffsets[buffer[k]]);

                        if (validOffsets.Count > 1) {
                            void RandNext() {
                                if (!buffer.Keys.Any(key => ReferenceEquals(key, k))) return;

                                int old = buffer[k];
                                
                                buffer[k] = RNG.Next(validOffsets.Count - 1);
                                if (buffer[k] >= old) buffer[k]++;
                                
                                Schedule(RandNext, start += RealTime);
                                
                                ScreenOutput(new [] {
                                    s.With((byte)validOffsets[old], new Color(0)),
                                    s.With((byte)validOffsets[buffer[k]])
                                }, output);
                            };
                            
                            Schedule(RandNext, start += RealTime);
                        }

                    } else {
                        if (s.Color.Lit) break;

                        v = s.With((byte)validOffsets[buffer[k]]);
                        buffer.Remove(k, out _);
                    }

                    return new [] {v};
            }

            return Enumerable.Empty<Signal>();
        }
        
        public override void MIDIProcess(List<Signal> n)
            => ScreenOutput(n.SelectMany((s => {
                if (s.Index == 100) return new [] {s};

                Signal off = s.With(color: new Color(0));
                if (s.Color.Lit) {
                    if (offbuf.Contains(off))
                        ScreenOutput(CopyCalc(off, false), false);

                    else offbuf.Add(off);
                
                } else offbuf.Remove(off);
                
                return CopyCalc(s);
            })));
        
        protected override void Stopped() {
            buffer.Clear();
            screen.Clear();
            locker.Clear();
            offbuf.Clear();
        }

        public override void Dispose() {
            if (Disposed) return;

            Stop();

            foreach (Offset offset in Offsets) offset.Dispose();
            Time.Dispose();
            base.Dispose();
        }
            
        public class RateUndoEntry: SimplePathUndoEntry<Copy, int> {
            protected override void Action(Copy item, int element) => item.Time.Data.Free = element;
            
            public RateUndoEntry(Copy copy, int u, int r)
            : base($"Copy Rate Changed to {r}ms", copy, u, r) {}
            
            RateUndoEntry(BinaryReader reader, int version)
            : base(reader, version) {}
        }   
        
        public class RateModeUndoEntry: SimplePathUndoEntry<Copy, bool> {
            protected override void Action(Copy item, bool element) => item.Time.Data.Mode = element;
            
            public RateModeUndoEntry(Copy copy, bool u, bool r)
            : base($"Copy Rate Switched to {(r? "Steps" : "Free")}", copy, u, r) {}
            
            RateModeUndoEntry(BinaryReader reader, int version)
            : base(reader, version) {}
        }
        
        public class RateStepUndoEntry: SimplePathUndoEntry<Copy, int> {
            protected override void Action(Copy item, int element) => item.Time.Data.Length.Step = element;
            
            public RateStepUndoEntry(Copy copy, int u, int r)
            : base($"Copy Rate Changed to {Length.Steps[r]}", copy, u, r) {}
            
            RateStepUndoEntry(BinaryReader reader, int version)
            : base(reader, version) {}
        }
        
        public class GateUndoEntry: SimplePathUndoEntry<Copy, double> {
            protected override void Action(Copy item, double element) => item.Data.Gate = element;
            
            public GateUndoEntry(Copy copy, double u, double r)
            : base($"Copy Gate Changed to {r}%", copy, u / 100, r / 100) {}
            
            GateUndoEntry(BinaryReader reader, int version)
            : base(reader, version) {}
        }
        
        public class CopyModeUndoEntry: EnumSimplePathUndoEntry<Copy, CopyType> {
            protected override void Action(Copy item, CopyType element) => item.Data.CopyMode = element;
            
            public CopyModeUndoEntry(Copy copy, CopyType u, CopyType r, IEnumerable source)
            : base("Copy Mode", copy, u, r, source) {}
            
            CopyModeUndoEntry(BinaryReader reader, int version)
            : base(reader, version) {}
        }
        
        public class GridModeUndoEntry: EnumSimplePathUndoEntry<Copy, GridType> {
            protected override void Action(Copy item, GridType element) => item.Data.GridMode = element;
            
            public GridModeUndoEntry(Copy copy, GridType u, GridType r, IEnumerable source)
            : base("Copy Grid", copy, u, r, source) {}
            
            GridModeUndoEntry(BinaryReader reader, int version)
            : base(reader, version) {}
        }
        
        public class PinchUndoEntry: SimplePathUndoEntry<Copy, double> {
            protected override void Action(Copy item, double element) => item.Data.Pinch = element;
            
            public PinchUndoEntry(Copy copy, double u, double r)
            : base($"Copy Pinch Changed to {r}", copy, u, r) {}
            
            PinchUndoEntry(BinaryReader reader, int version)
            : base(reader, version) {}
        }
        
        public class BilateralUndoEntry: SimplePathUndoEntry<Copy, bool> {
            protected override void Action(Copy item, bool element) => item.Data.Bilateral = element;
            
            public BilateralUndoEntry(Copy copy, bool u, bool r)
            : base($"Copy Bilateral Changed to {(r? "Enabled" : "Disabled")}", copy, u, r) {}
            
            BilateralUndoEntry(BinaryReader reader, int version)
            : base(reader, version) {}
        }
        
        public class ReverseUndoEntry: SimplePathUndoEntry<Copy, bool> {
            protected override void Action(Copy item, bool element) => item.Data.Reverse = element;
            
            public ReverseUndoEntry(Copy copy, bool u, bool r)
            : base($"Copy Reverse Changed to {(r? "Enabled" : "Disabled")}", copy, u, r) {}
            
            ReverseUndoEntry(BinaryReader reader, int version)
            : base(reader, version) {}
        }
        
        public class InfiniteUndoEntry: SimplePathUndoEntry<Copy, bool> {
            protected override void Action(Copy item, bool element) => item.Data.Infinite = element;
            
            public InfiniteUndoEntry(Copy copy, bool u, bool r)
            : base($"Copy Infinite Changed to {(r? "Enabled" : "Disabled")}", copy, u, r) {}
            
            InfiniteUndoEntry(BinaryReader reader, int version)
            : base(reader, version) {}
        }
        
        public class WrapUndoEntry: SimplePathUndoEntry<Copy, bool> {
            protected override void Action(Copy item, bool element) => item.Data.Wrap = element;
            
            public WrapUndoEntry(Copy copy, bool u, bool r)
            : base($"Copy Wrap Changed to {(r? "Enabled" : "Disabled")}", copy, u, r) {}
            
            WrapUndoEntry(BinaryReader reader, int version)
            : base(reader, version) {}
        }
        
        public class OffsetInsertUndoEntry: PathUndoEntry<Copy> {
            int index;
            
            protected override void UndoPath(params Copy[] items) => items[0].Remove(index);
            protected override void RedoPath(params Copy[] items) => items[0].Insert(index);
            
            public OffsetInsertUndoEntry(Copy copy, int index)
            : base($"Copy Offset {index + 1} Inserted", copy) => this.index = index;
            
            OffsetInsertUndoEntry(BinaryReader reader, int version)
            : base(reader, version) => index = reader.ReadInt32();
            
            public override void Encode(BinaryWriter writer) {
                base.Encode(writer);
                
                writer.Write(index);
            }
        }
        
        public class OffsetRemoveUndoEntry: PathUndoEntry<Copy> {
            int index;
            OffsetData offset;
            int angle;
            
            protected override void UndoPath(params Copy[] items) => items[0].Insert(index, offset.Clone(), angle);
            protected override void RedoPath(params Copy[] items) => items[0].Remove(index);
            
            public OffsetRemoveUndoEntry(Copy copy, Offset offset, int angle, int index)
            : base($"Copy Offset {index + 1} Removed", copy) {
                this.index = index;
                this.offset = offset.Data.Clone();
                this.angle = angle;
            }
            
            OffsetRemoveUndoEntry(BinaryReader reader, int version): base(reader, version) {
                index = reader.ReadInt32();
                offset = Decoder.Decode<OffsetData>(reader, version);
                angle = reader.ReadInt32();
            }
            
            public override void Encode(BinaryWriter writer) {
                base.Encode(writer);
                
                writer.Write(index);
                Encoder.Encode(writer, offset);
                writer.Write(angle);
            }
        }

        public abstract class OffsetUpdatedUndoEntry: PathUndoEntry<Copy> {
            int index, ux, uy, rx, ry;

            protected abstract void Action(Offset item, int u, int r);

            protected override void UndoPath(params Copy[] item)
                => Action(item[0].Offsets[index], ux, uy);

            protected override void RedoPath(params Copy[] item)
                => Action(item[0].Offsets[index], rx, ry);
            
            public OffsetUpdatedUndoEntry(string kind, Copy copy, int index, int ux, int uy, int rx, int ry)
            : base($"Copy Offset {index + 1} {kind} Changed to {rx},{ry}", copy) {
                this.index = index;
                this.ux = ux;
                this.uy = uy;
                this.rx = rx;
                this.ry = ry;
            }

            protected OffsetUpdatedUndoEntry(BinaryReader reader, int version)
            : base(reader, version) {
                this.index = reader.ReadInt32();
                this.ux = reader.ReadInt32();
                this.uy = reader.ReadInt32();
                this.rx = reader.ReadInt32();
                this.ry = reader.ReadInt32();
            }

            public override void Encode(BinaryWriter writer) {
                base.Encode(writer);

                writer.Write(index);
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
            
            public OffsetRelativeUndoEntry(Copy copy, int index, int ux, int uy, int rx, int ry)
            : base("Relative", copy, index, ux, uy, rx, ry) {}

            OffsetRelativeUndoEntry(BinaryReader reader, int version)
            : base(reader, version) {}
        }

        public class OffsetAbsoluteUndoEntry: OffsetUpdatedUndoEntry {
            protected override void Action(Offset item, int x, int y) {
                item.Data.AbsoluteX = x;
                item.Data.AbsoluteY = y;
            }
            
            public OffsetAbsoluteUndoEntry(Copy copy, int index, int ux, int uy, int rx, int ry)
            : base("Absolute", copy, index, ux, uy, rx, ry) {}

            OffsetAbsoluteUndoEntry(BinaryReader reader, int version)
            : base(reader, version) {}
        }

        public class OffsetSwitchedUndoEntry: SimpleIndexPathUndoEntry<Copy, bool> {
            protected override void Action(Copy item, int index, bool element)
                => item.Offsets[index].Data.IsAbsolute = element;
            
            public OffsetSwitchedUndoEntry(Copy copy, int index, bool u, bool r)
            : base($"Copy Offset {index + 1} Switched to {(r? "Absolute" : "Relative")}", copy, index, u, r) {}
            
            OffsetSwitchedUndoEntry(BinaryReader reader, int version)
            : base(reader, version) {}
        }

        public class OffsetAngleUndoEntry: SimpleIndexPathUndoEntry<Copy, int> {
            protected override void Action(Copy item, int index, int element) => item.SetAngle(index, element);
            
            public OffsetAngleUndoEntry(Copy copy, int index, int u, int r)
            : base($"Copy Angle {index + 1} Changed to {r}°", copy, index, u, r) {}
            
            OffsetAngleUndoEntry(BinaryReader reader, int version)
            : base(reader, version) {}
        }
    }
}