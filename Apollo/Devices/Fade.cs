using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Apollo.Binary;
using Apollo.Core;
using Apollo.DeviceViewers;
using Apollo.Elements;
using Apollo.Enums;
using Apollo.Rendering;
using Apollo.Structures;
using Apollo.Undo;

namespace Apollo.Devices {
    public class FadeData: DeviceData {
        public new Fade Instance => (Fade)Instance;
        public FadeViewer Viewer => Instance?.SpecificViewer<FadeViewer>();

        public TimeData Time;

        double _gate;
        public double Gate {
            get => _gate;
            set {
                if (0.01 <= value && value <= 4) {
                    _gate = value;
                    
                    Instance?.Generate();
                    Viewer?.SetGate(Gate);
                }
            }
        }

        FadePlaybackType _mode;
        public FadePlaybackType PlayMode {
            get => _mode;
            set {
                _mode = value;

                Viewer?.SetPlaybackMode(PlayMode);
            }
        }

        public List<Color> Colors = new();
        public void SetColor(int index, Color color) {
            if (Colors[index] != color) {
                Colors[index] = color;

                Instance?.Generate();
                Viewer?.SetColor(index, Colors[index]);
            }
        }

        public List<double> Positions = new();
        public void SetPosition(int index, double position) {
            if (Positions[index] != position) {
                Positions[index] = position;

                Instance?.Generate();
                Viewer?.SetPosition(index, Positions[index]);
            }
        }

        public List<FadeType> Types = new();
        public void SetFadeType(int index, FadeType type) {
            if (Types[index] != type) {
                Types[index] = type;

                Instance?.Generate();
            }
        }

        public int Count => Colors.Count;
        public int? Expanded;
        
        public FadeData(TimeData time = null, double gate = 1, FadePlaybackType playmode = FadePlaybackType.Mono, List<Color> colors = null, List<double> positions = null, List<FadeType> types = null, int? expanded = null) {
            Time = time?? new TimeData();
            Gate = gate;
            PlayMode = playmode;

            Colors = colors?? new List<Color>() {new Color(), new Color(0)};
            Positions = positions?? new List<double>() {0, 1};
            Types = types?? new List<FadeType>() {FadeType.Linear};
            Expanded = expanded;
        }

        protected override DeviceData CloneSpecific()
            => new FadeData(Time.Clone(), _gate, PlayMode, Colors.Select(i => i.Clone()).ToList(), Positions.ToList(), Types.ToList());
        
        protected override Device ActivateSpecific(DeviceData data)
            => new Fade((FadeData)data);
    }

    public class Fade: Device {
        public new FadeData Data => (FadeData)Data;

        public class FadeInfo {
            public Color Color;
            public double Time;
            public bool IsHold;

            public FadeInfo(Color color, double time, bool isHold = false) {
                Color = color;
                Time = time;
                IsHold = isHold;
            }

            public FadeInfo WithTime(double time) => new FadeInfo(Color, time, IsHold);
        }

        static Dictionary<FadeType, Func<double, double>> TimeEasing = new() { 
            {FadeType.Fast, proportion => Math.Pow(proportion, 2)},
            {FadeType.Slow, proportion => 1 - Math.Pow(1 - proportion, 2)},
            {FadeType.Sharp, proportion => (proportion < 0.5)
                ? Math.Pow(proportion - 0.5, 2) * -2 + 0.5
                : Math.Pow(proportion - 0.5, 2) * 2 + 0.5
            },
            {FadeType.Smooth, proportion => (proportion < 0.5)
                ? Math.Pow(proportion, 2) * 2
                : Math.Pow(proportion - 1, 2) * -2 + 1
            }
        };

        static double EaseTime(FadeType type, double start, double end, double val) {
            if (type == FadeType.Linear) return val;
            if (type == FadeType.Hold) return (start != val)? end - 0.1 : start;
            if (type == FadeType.Release) return start;

            double duration = end - start;
            return start + duration * TimeEasing[type].Invoke((val - start) / duration);
        }

        List<FadeInfo> fade;
    
        public readonly Time Time;

        void FreeChanged(int value) {
            Generate();
            Data.Viewer?.SetDurationValue(value);
        }

        void ModeChanged(bool value) {
            Generate();
            Data.Viewer?.SetMode(value);
        }

        void StepChanged(Length value) {
            Generate();
            Data.Viewer?.SetDurationStep(value);
        }

        public double RealTime => Time * Data.Gate;

        public delegate void GeneratedEventHandler(List<FadeInfo> points);
        public event GeneratedEventHandler Generated;

        public void Generate() => Generate(Preferences.FPSLimit);

        void Generate(int fps) {
            double frameTime = 1000.0 / fps;

            if (Data.Colors.Count < 2 || Data.Positions.Count < 2) return;
            if (Data.Types.Count < Data.Colors.Count - 1) return;

            List<Color> _steps = new();
            List<int> _counts = new();
            List<int> _cutoffs = new() {0};

            for (int i = 0; i < Data.Colors.Count - 1; i++) {
                int max = new [] {
                    Math.Abs(Data.Colors[i].Red - Data.Colors[i + 1].Red),
                    Math.Abs(Data.Colors[i].Green - Data.Colors[i + 1].Green),
                    Math.Abs(Data.Colors[i].Blue - Data.Colors[i + 1].Blue),
                    1
                }.Max();

                if(Data.Types[i] == FadeType.Hold) {
                    _steps.Add(Data.Colors[i]);
                    _counts.Add(1);
                    _cutoffs.Add(1 + _cutoffs.Last());

                } else {
                    for (double k = 0; k < max; k++) {
                        double factor = k / max;
                        _steps.Add(new Color(
                            (byte)(Data.Colors[i].Red + (Data.Colors[i + 1].Red - Data.Colors[i].Red) * factor),
                            (byte)(Data.Colors[i].Green + (Data.Colors[i + 1].Green - Data.Colors[i].Green) * factor),
                            (byte)(Data.Colors[i].Blue + (Data.Colors[i + 1].Blue - Data.Colors[i].Blue) * factor)
                        ));
                    }

                    _counts.Add(max);
                    _cutoffs.Add(max + _cutoffs.Last());
                }
            }

            _steps.Add(Data.Colors.Last());

            if (_steps.Last().Lit) {
                _cutoffs[_cutoffs.Count - 1]++;
                _counts[_counts.Count - 1]++;
            
                _steps.Add(new Color(0));
                _counts.Add(1);
                _cutoffs.Add(1 + _cutoffs.Last());
            }

            List<FadeInfo> fullFade = new() {
                new FadeInfo(_steps[0], 0, Data.Types[0] == FadeType.Hold)
            };

            int j = 0;
            for (int i = 1; i < _steps.Count; i++) {
                if (_cutoffs[j + 1] == i) j++;
                
                if (j < Data.Colors.Count - 1) {
                    double prevTime = (j != 0)? Data.Positions[j] * RealTime : 0;
                    double currTime = (Data.Positions[j] + (Data.Positions[j + 1] - Data.Positions[j]) * (i - _cutoffs[j]) / _counts[j]) * RealTime;
                    double nextTime = Data.Positions[j + 1] * RealTime;
                    
                    double time = EaseTime(Data.Types[j], prevTime, nextTime, currTime);
                    
                    fullFade.Add(new FadeInfo(_steps[i], time, Data.Types[j] == FadeType.Hold));
                }
            }
            
            if (fade != null) fade.Clear();
            else fade = new List<FadeInfo>();

            fade.Add(fullFade.First());

            for (int i = 1; i < fullFade.Count; i++) {
                double cutoff = fade.Last().Time + frameTime;

                if (cutoff < fullFade[i].Time)
                    fade.Add(fullFade[i]);
                    
                else if (fade.Last().Time + 2 * frameTime <= ((i < fullFade.Count - 1)? fullFade[i + 1].Time : RealTime))
                    fade.Add(fullFade[i].WithTime(cutoff));

                else if (i == fullFade.Count - 1 && fullFade[i].Color.Lit)
                    fade.Add(fullFade[i]);
            }

            fade.Add(new FadeInfo(_steps.Last(), RealTime));
            
            Generated?.Invoke(fullFade);
        }

        public void Insert(int index, Color color, double position, FadeType type) {
            Data.Colors.Insert(index, color);
            Data.Positions.Insert(index, position);
            Data.Types.Insert(index, type);

            Data.Viewer?.Contents_Insert(index, Data.Colors[index]);
            Data.Viewer?.Expand(index);

            Generate();
        }

        public void Remove(int index) {
            Data.Colors.RemoveAt(index);
            Data.Positions.RemoveAt(index);
            if (index < Data.Types.Count) Data.Types.RemoveAt(index);

            Data.Viewer?.Contents_Remove(index);

            Generate();
        }

        public Fade(FadeData data): base(data, "fade") {
            Time = Data.Time.Activate();

            Time.Minimum = 1;
            Time.Maximum = 30000;

            Time.FreeChanged += FreeChanged;
            Time.ModeChanged += ModeChanged;
            Time.StepChanged += StepChanged;

            Initialize();
        }

        protected override void Initialized() {
            Generate();

            Preferences.FPSLimitChanged += Generate;

            if (Program.Project != null)
                Program.Project.BPMChanged += Generate;
        }

        ConcurrentDictionary<Signal, Signal> buffer = new();

        public override void MIDIProcess(List<Signal> n)
            => InvokeExit(n.SelectMany(i => {
                Signal k = i.With(color: new Color(0));
                Signal v = i.Clone();

                Signal p = buffer.TryGetValue(k, out p)? p : null;

                if (!(Data.PlayMode == FadePlaybackType.Mono && !i.Color.Lit && buffer.ContainsKey(k) && (buffer[k]?.Color.Lit?? false)))
                    buffer[k] = v;

                if (i.Color.Lit) {
                    double start = Heaven.Time;
                    int index = 0;
                    
                    void Next() {
                        if (!object.ReferenceEquals(buffer[k], v)) {
                            if (Data.PlayMode == FadePlaybackType.Mono && !buffer[k].Color.Lit)
                                v = buffer[k];
                            
                            else return;
                        }

                        if (++index == fade.Count - 1 && Data.PlayMode == FadePlaybackType.Loop)
                            index = 0;
                        
                        if (index < fade.Count) {
                            if (index < fade.Count - 1) Schedule(Next, start += fade[index + 1].Time - fade[index].Time);

                            InvokeExit(new List<Signal>() {i.With(color: fade[index].Color.Clone())});
                        }
                    };

                    if (fade == null) Initialize();

                    Schedule(Next, start += fade[1].Time - fade[0].Time);

                    return new [] {i.With(color: fade[0].Color.Clone())};
                
                } else if (Data.PlayMode == FadePlaybackType.Loop && !(p is null)) {
                    buffer[k] = null;

                    return new [] {k};
                }

                return Enumerable.Empty<Signal>();
            }).ToList());

        protected override void Stopped() => buffer.Clear();

        public override void Dispose() {
            if (Disposed) return;

            Generated = null;
            Preferences.FPSLimitChanged -= Generate;

            if (Program.Project != null)
                Program.Project.BPMChanged -= Generate;

            Time.Dispose();
            base.Dispose();
        }

        public class ThumbInsertUndoEntry: PathUndoEntry<Fade> {
            int index;
            Color thumbColor;
            double pos;
            FadeType type;
            
            protected override void UndoPath(params Fade[] items) => items[0].Remove(index);
            protected override void RedoPath(params Fade[] items) => items[0].Insert(index, thumbColor, pos, type);
            
            public ThumbInsertUndoEntry(Fade fade, int index, Color thumbColor, double pos, FadeType type)
            : base($"Fade Color {index + 1} Inserted", fade) {
                this.index = index;
                this.thumbColor = thumbColor;
                this.pos = pos;
                this.type = type;
            }
            
            ThumbInsertUndoEntry(BinaryReader reader, int version)
            : base(reader, version) {
                index = reader.ReadInt32();
                thumbColor = Decoder.Decode<Color>(reader, version);
                pos = reader.ReadDouble();
                type = (FadeType)reader.ReadInt32();
            }
            
            public override void Encode(BinaryWriter writer) {
                base.Encode(writer);
                
                writer.Write(index);
                Encoder.Encode(writer, thumbColor);
                writer.Write(pos);
                writer.Write((int)type);
            }
        }
        
        public class ThumbRemoveUndoEntry: PathUndoEntry<Fade> {
            int index;
            Color uc;
            double up;
            FadeType ut;
            
            protected override void UndoPath(params Fade[] items) => items[0].Insert(index, uc.Clone(), up, ut);
            protected override void RedoPath(params Fade[] items) => items[0].Remove(index);
            
            public ThumbRemoveUndoEntry(Fade fade, int index)
            : base($"Fade Color {index + 1} Removed", fade) {
                this.index = index;
                
                uc = fade.Data.Colors[index].Clone();
                up = fade.Data.Positions[index];
                ut = fade.Data.Types[index];
            }
            
            ThumbRemoveUndoEntry(BinaryReader reader, int version): base(reader, version) {
                index = reader.ReadInt32();
                uc = Decoder.Decode<Color>(reader, version);
                up = reader.ReadDouble();
                ut = (FadeType)reader.ReadInt32();
            }
            
            public override void Encode(BinaryWriter writer) {
                base.Encode(writer);
                
                writer.Write(index);
                Encoder.Encode(writer, uc);
                writer.Write(up);
                writer.Write((int)ut);
            }
        }
        
        public class ThumbTypeUndoEntry: SimpleIndexPathUndoEntry<Fade, FadeType> {
            protected override void Action(Fade item, int index, FadeType element) => item.Data.SetFadeType(index, element);
            
            public ThumbTypeUndoEntry(Fade fade, int index, FadeType r)
            : base($"Fade Type {index + 1} Changed to {r.ToString()}", fade, index, fade.Data.Types[index], r) {}
            
            ThumbTypeUndoEntry(BinaryReader reader, int version)
            : base(reader, version) {}
        }
        
        public class ThumbMoveUndoEntry: SimpleIndexPathUndoEntry<Fade, double> {
            protected override void Action(Fade item, int index, double element) => item.Data.SetPosition(index, element);
            
            public ThumbMoveUndoEntry(Fade fade, int index, double u, double r)
            : base($"Fade Color {index + 1} Moved", fade, index, u, r) {}
            
            ThumbMoveUndoEntry(BinaryReader reader, int version)
            : base(reader, version) {}
        }
        
        public class ColorUndoEntry: SimpleIndexPathUndoEntry<Fade, Color> {
            protected override void Action(Fade item, int index, Color element) => item.Data.SetColor(index, element.Clone());
            
            public ColorUndoEntry(Fade fade, int index, Color u, Color r)
            : base($"Fade Color {index + 1} Changed to {r.ToHex()}", fade, index, u.Clone(), r.Clone()) {}
            
            ColorUndoEntry(BinaryReader reader, int version)
            : base(reader, version) {}
        }
        
        public class DurationUndoEntry: SimplePathUndoEntry<Fade, int> {
            protected override void Action(Fade item, int element) => item.Time.Data.Free = element;
            
            public DurationUndoEntry(Fade fade, int u, int r)
            : base($"Fade Duration Changed to {r}ms", fade, u, r) {}
            
            DurationUndoEntry(BinaryReader reader, int version)
            : base(reader, version) {}
        }
        
        public class DurationModeUndoEntry: SimplePathUndoEntry<Fade, bool> {
            protected override void Action(Fade item, bool element) => item.Time.Data.Mode = element;
            
            public DurationModeUndoEntry(Fade fade, bool u, bool r)
            : base($"Fade Duration Switched to {(r? "Steps" : "Free")}", fade, u, r) {}
            
            DurationModeUndoEntry(BinaryReader reader, int version)
            : base(reader, version) {}
        }
        
        public class DurationStepUndoEntry: SimplePathUndoEntry<Fade, int> {
            protected override void Action(Fade item, int element) => item.Time.Length.Data.Step = element;
            
            public DurationStepUndoEntry(Fade fade, int u, int r)
            : base($"Fade Duration Changed to {Length.Steps[r]}", fade, u, r) {}
            
            DurationStepUndoEntry(BinaryReader reader, int version)
            : base(reader, version) {}
        }
        
        public class GateUndoEntry: SimplePathUndoEntry<Fade, double> {
            protected override void Action(Fade item, double element) => item.Data.Gate = element;
            
            public GateUndoEntry(Fade fade, double u, double r)
            : base($"Fade Gate Changed to {r}%", fade, u / 100, r / 100) {}
            
            GateUndoEntry(BinaryReader reader, int version)
            : base(reader, version) {}
        }
        
        public class PlaybackModeUndoEntry: SimplePathUndoEntry<Fade, FadePlaybackType> {
            protected override void Action(Fade item, FadePlaybackType element) => item.Data.PlayMode = element;
            
            public PlaybackModeUndoEntry(Fade fade, FadePlaybackType u, FadePlaybackType r)
            : base($"Fade Playback Mode Changed to {r.ToString()}", fade, u, r) {}
            
            PlaybackModeUndoEntry(BinaryReader reader, int version)
            : base(reader, version) {}
        }
        
        public class ReverseUndoEntry: SymmetricPathUndoEntry<Fade> {
            protected override void Action(Fade item) {
                List<Color> colors = item.Data.Colors.ToList();
                List<double> positions = item.Data.Positions.ToList();
                List<FadeType> fadetypes = item.Data.Types.ToList();
                
                for (int i = 0; i < item.Data.Count; i++) {
                    item.Data.SetColor(i, colors[item.Data.Count - i - 1]);
                    item.Data.SetPosition(i, positions[item.Data.Count - i - 1]);
                }
                
                for (int i = 0; i < item.Data.Count - 1; i++)
                    item.Data.SetFadeType(i, fadetypes[item.Data.Count - i - 2].Opposite());

                int? expanded = item.Data.Count - item.Data.Expanded - 1;
                if (expanded != item.Data.Expanded)
                    item.Data.Viewer?.Expand(expanded);
            }
            
            public ReverseUndoEntry(Fade fade)
            : base("Fade Reversed", fade) {}
            
            ReverseUndoEntry(BinaryReader reader, int version)
            : base(reader, version) {}
        }
        
        public class EqualizeUndoEntry: SimplePathUndoEntry<Fade, double[]> {
            protected override void Action(Fade item, double[] element) {
                for (int i = 1; i < item.Data.Count - 1; i++)
                    item.Data.SetPosition(i, element[i - 1]);
            }
            
            public EqualizeUndoEntry(Fade fade)
            : base("Fade Equalized", fade,
                Enumerable.Range(1, fade.Data.Count - 2).Select(i => fade.Data.Positions[i]).ToArray(),
                Enumerable.Range(1, fade.Data.Count - 2).Select(i => (double)i / (fade.Data.Count - 1)).ToArray()
            ) {}
            
            EqualizeUndoEntry(BinaryReader reader, int version)
            : base(reader, version) {}
        }

        public abstract class CutUndoEntry: SinglePathUndoEntry<Fade> {
            List<Color> colors;
            List<double> positions;
            List<FadeType> fadetypes;
            int index;

            protected override void Undo(Fade item) {
                while (item.Data.Count > 0)
                    item.Remove(item.Data.Count - 1);
                
                for (int i = 0; i < colors.Count; i++)
                    item.Insert(i, colors[i].Clone(), positions[i], i < fadetypes.Count? fadetypes[i] : FadeType.Linear);
            }
            
            protected override void Redo(Fade item) => Redo(item, index);
            protected abstract void Redo(Fade item, int index);
            
            public CutUndoEntry(Fade fade, int index, string action)
            : base($"Fade {action} Here Applied To Color {index + 1}", fade) {
                this.index = index;
                
                colors = fade.Data.Colors.Select(i => i.Clone()).ToList();
                positions = fade.Data.Positions.ToList();
                fadetypes = fade.Data.Types.ToList();
            }
        
            protected CutUndoEntry(BinaryReader reader, int version)
            : base(reader, version) {
                colors = Enumerable.Range(0, reader.ReadInt32()).Select(i => Decoder.Decode<Color>(reader, version)).ToList();
                positions = Enumerable.Range(0, reader.ReadInt32()).Select(i => reader.ReadDouble()).ToList();
                fadetypes = Enumerable.Range(0, reader.ReadInt32()).Select(i => (FadeType)reader.ReadInt32()).ToList();
                index = reader.ReadInt32();
            }
            
            public override void Encode(BinaryWriter writer) {
                base.Encode(writer);
                
                writer.Write(colors.Count);
                for (int i = 0; i < colors.Count; i++)
                    Encoder.Encode(writer, colors[i]);
                
                writer.Write(positions.Count);
                for (int i = 0; i < positions.Count; i++)
                     writer.Write(positions[i]);
                
                writer.Write(fadetypes.Count);
                for (int i = 0; i < fadetypes.Count; i++)
                    writer.Write((int)fadetypes[i]);
                
                writer.Write(index);
            }
        }

        public class StartHereUndoEntry: CutUndoEntry {
            protected override void Redo(Fade item, int index) {
                for (int i = index - 1; i >= 0; i--)
                    item.Remove(i);
                
                for (int i = item.Data.Count - 1; i >= 0; i--)
                    item.Data.SetPosition(i, (item.Data.Positions[i] - item.Data.Positions[0]) / (1 - item.Data.Positions[0]));
            }
            
            public StartHereUndoEntry(Fade fade, int index)
            : base(fade, index, "Start") {}
            
            StartHereUndoEntry(BinaryReader reader, int version)
            : base(reader, version) {}
        }

        public class EndHereUndoEntry: CutUndoEntry {
            protected override void Redo(Fade item, int index) {
                for (int i = item.Data.Count - 1; i > index; i--)
                    item.Remove(i);
                
                for (int i = 0; i < item.Data.Count; i++)
                    item.Data.SetPosition(i, item.Data.Positions[i] / item.Data.Positions[i]);
            }
            
            public EndHereUndoEntry(Fade fade, int index)
            : base(fade, index, "End") {}
            
            EndHereUndoEntry(BinaryReader reader, int version)
            : base(reader, version) {}
        }
    }
}