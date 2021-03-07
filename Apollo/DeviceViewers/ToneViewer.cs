using System;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

using Apollo.Components;
using Apollo.Core;
using Apollo.Devices;

namespace Apollo.DeviceViewers {
    public class ToneViewer: UserControl {
        public static readonly string DeviceIdentifier = "tone";

        void InitializeComponent() {
            AvaloniaXamlLoader.Load(this);

            Hue = this.Get<Dial>("Hue");
            SaturationHigh = this.Get<Dial>("SaturationHigh");
            SaturationLow = this.Get<Dial>("SaturationLow");
            ValueHigh = this.Get<Dial>("ValueHigh");
            ValueLow = this.Get<Dial>("ValueLow");
        }
        
        Tone _tone;

        Dial Hue, SaturationHigh, SaturationLow, ValueHigh, ValueLow;

        public ToneViewer() => new InvalidOperationException();

        public ToneViewer(Tone tone) {
            InitializeComponent();

            _tone = tone;

            Hue.RawValue = _tone.Data.Hue;
            SaturationHigh.RawValue = _tone.Data.SaturationHigh * 100;
            SaturationLow.RawValue = _tone.Data.SaturationLow * 100;
            ValueHigh.RawValue = _tone.Data.ValueHigh * 100;
            ValueLow.RawValue = _tone.Data.ValueLow * 100;
        }

        void Unloaded(object sender, VisualTreeAttachmentEventArgs e) => _tone = null;

        void Hue_Changed(Dial sender, double value, double? old) {
            if (old != null && old != value)
                Program.Project.Undo.AddAndExecute(new Tone.HueUndoEntry(
                    _tone, 
                    old.Value, 
                    value
                ));
        }

        public void SetHue(double value) => Hue.RawValue = value;

        void SaturationHigh_Changed(Dial sender, double value, double? old) {
            if (old != null && old != value)
                Program.Project.Undo.AddAndExecute(new Tone.SatHighUndoEntry(
                    _tone, 
                    old.Value, 
                    value
                ));
        }

        public void SetSaturationHigh(double value) => SaturationHigh.RawValue = value * 100;

        void SaturationLow_Changed(Dial sender, double value, double? old) {
            if (old != null && old != value)
                Program.Project.Undo.AddAndExecute(new Tone.SatLowUndoEntry(
                    _tone, 
                    old.Value, 
                    value
                ));
        }

        public void SetSaturationLow(double value) => SaturationLow.RawValue = value * 100;

        void ValueHigh_Changed(Dial sender, double value, double? old) {
            if (old != null && old != value)
                Program.Project.Undo.AddAndExecute(new Tone.ValueHighUndoEntry(
                    _tone, 
                    old.Value, 
                    value
                ));
        }

        public void SetValueHigh(double value) => ValueHigh.RawValue = value * 100;

        void ValueLow_Changed(Dial sender, double value, double? old) {
            if (old != null && old != value)
                Program.Project.Undo.AddAndExecute(new Tone.ValueLowUndoEntry(
                    _tone, 
                    old.Value, 
                    value
                ));
        }

        public void SetValueLow(double value) => ValueLow.RawValue = value * 100;
    }
}
