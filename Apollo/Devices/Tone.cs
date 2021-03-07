using System.Collections.Generic;
using System.IO;

using Apollo.DeviceViewers;
using Apollo.Elements;
using Apollo.Structures;
using Apollo.Undo;

namespace Apollo.Devices {
    public class ToneData: DeviceData {
        ToneViewer Viewer => Instance?.SpecificViewer<ToneViewer>();

        double _h, _sh, _sl, _vh, _vl;

        public double Hue {
            get => _h;
            set {
                if (-180 <= value && value <= 180 && _h != value) {
                    _h = value;

                    Viewer?.SetHue(Hue);
                }
            }
        }

        public double SaturationHigh {
            get => _sh;
            set {
                if (0 <= value && value <= 1 && _sh != value) {
                    _sh = value;

                    Viewer?.SetSaturationHigh(SaturationHigh);
                }
            }
        }

        public double SaturationLow {
            get => _sl;
            set {
                if (0 <= value && value <= 1 && _sl != value) {
                    _sl = value;

                    Viewer?.SetSaturationLow(SaturationLow);
                }
            }
        }

        public double ValueHigh {
            get => _vh;
            set {
                if (0 <= value && value <= 1 && _vh != value) {
                    _vh = value;

                    Viewer?.SetValueHigh(ValueHigh);
                }
            }
        }

        public double ValueLow {
            get => _vl;
            set {
                if (0 <= value && value <= 1 && _vl != value) {
                    _vl = value;

                    Viewer?.SetValueLow(ValueLow);
                }
            }
        }

        public ToneData(double hue = 0, double saturation_high = 1, double saturation_low = 0, double value_high = 1, double value_low = 0) {
            Hue = hue;

            SaturationHigh = saturation_high;
            SaturationLow = saturation_low;

            ValueHigh = value_high;
            ValueLow = value_low;
        }

        protected override DeviceData CloneSpecific()
            => new ToneData(Hue, SaturationHigh, SaturationLow, ValueHigh, ValueLow);

        protected override Device ActivateSpecific(DeviceData data)
            => new Tone((ToneData)data);
    }

    public class Tone: Device {
        public new ToneData Data => (ToneData)Data;

        public Tone(ToneData data = null): base(data?? new(), "tone") {}

        public override void MIDIProcess(List<Signal> n) {
            n.ForEach(i => {
                if (i.Color.Lit) {
                    (double hue, double saturation, double value) = i.Color.ToHSV();

                    hue = (hue + Data.Hue + 360) % 360;
                    saturation = saturation * (Data.SaturationHigh - Data.SaturationLow) + Data.SaturationLow;
                    value = value * (Data.ValueHigh - Data.ValueLow) + Data.ValueLow;

                    i.Color = Color.FromHSV(hue, saturation, value);
                }
            });

            InvokeExit(n);
        }
        
        public class HueUndoEntry: SimplePathUndoEntry<Tone, double> {
            protected override void Action(Tone item, double element) => item.Data.Hue = element;
            
            public HueUndoEntry(Tone tone, double u, double r)
            : base($"Tone Hue Changed to {r}°", tone, u, r) {}
            
            HueUndoEntry(BinaryReader reader, int version)
            : base(reader, version) {}
        }
        
        public class SatHighUndoEntry: SimplePathUndoEntry<Tone, double> {
            protected override void Action(Tone item, double element) => item.Data.SaturationHigh = element;
            
            public SatHighUndoEntry(Tone tone, double u, double r)
            : base($"Tone Sat Hi Changed to {r}%", tone, u / 100, r / 100) {}
            
            SatHighUndoEntry(BinaryReader reader, int version)
            : base(reader, version) {}
        }
        
        public class SatLowUndoEntry: SimplePathUndoEntry<Tone, double> {
            protected override void Action(Tone item, double element) => item.Data.SaturationLow = element;
            
            public SatLowUndoEntry(Tone tone, double u, double r)
            : base($"Tone Sat Lo Changed to {r}%", tone, u / 100, r / 100) {}
            
            SatLowUndoEntry(BinaryReader reader, int version)
            : base(reader, version) {}
        }
        
        public class ValueHighUndoEntry: SimplePathUndoEntry<Tone, double> {
            protected override void Action(Tone item, double element) => item.Data.ValueHigh = element;
            
            public ValueHighUndoEntry(Tone tone, double u, double r)
            : base($"Tone Value Hi Changed to {r}%", tone, u / 100, r / 100) {}
            
            ValueHighUndoEntry(BinaryReader reader, int version)
            : base(reader, version) {}
        }
        
        public class ValueLowUndoEntry: SimplePathUndoEntry<Tone, double> {
            protected override void Action(Tone item, double element) => item.Data.ValueLow = element;
            
            public ValueLowUndoEntry(Tone tone, double u, double r)
            : base($"Tone Value Lo Changed to {r}%", tone, u / 100, r / 100) {}
            
            ValueLowUndoEntry(BinaryReader reader, int version)
            : base(reader, version) {}
        }
    }
}