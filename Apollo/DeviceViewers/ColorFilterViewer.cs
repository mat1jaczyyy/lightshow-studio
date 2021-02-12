using System;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

using Apollo.Components;
using Apollo.Core;
using Apollo.Devices;

namespace Apollo.DeviceViewers {
    public class ColorFilterViewer: UserControl {
        public static readonly string DeviceIdentifier = "colorfilter";

        void InitializeComponent() {
            AvaloniaXamlLoader.Load(this);

            Hue = this.Get<Dial>("Hue");
            Saturation = this.Get<Dial>("Saturation");
            Value = this.Get<Dial>("Value");

            HueTolerance = this.Get<Dial>("HueTolerance");
            SaturationTolerance = this.Get<Dial>("SaturationTolerance");
            ValueTolerance = this.Get<Dial>("ValueTolerance");
        }
        
        ColorFilter _filter;

        Dial Hue, Saturation, Value, HueTolerance, SaturationTolerance, ValueTolerance;

        public ColorFilterViewer() => new InvalidOperationException();

        public ColorFilterViewer(ColorFilter filter) {
            InitializeComponent();

            _filter = filter;

            Hue.RawValue = _filter.Data.Hue;
            Saturation.RawValue = _filter.Data.Saturation * 100;
            Value.RawValue = _filter.Data.Value * 100;
            
            HueTolerance.RawValue = _filter.Data.HueTolerance * 100;
            SaturationTolerance.RawValue = _filter.Data.SaturationTolerance * 100;
            ValueTolerance.RawValue = _filter.Data.ValueTolerance * 100;
        }

        void Unloaded(object sender, VisualTreeAttachmentEventArgs e) => _filter = null;

        void Hue_Changed(Dial sender, double value, double? old) {
            if (old != null && old != value) 
                Program.Project.Undo.AddAndExecute(new ColorFilter.HueUndoEntry(
                    _filter, 
                    old.Value, 
                    value
                ));
        }

        public void SetHue(double value) => Hue.RawValue = value;

        void Saturation_Changed(Dial sender, double value, double? old) {
            if (old != null && old != value) 
                Program.Project.Undo.AddAndExecute(new ColorFilter.SaturationUndoEntry(
                    _filter, 
                    old.Value, 
                    value
                ));
        }

        public void SetSaturation(double value) => Saturation.RawValue = value * 100;

        void Value_Changed(Dial sender, double value, double? old) {
            if (old != null && old != value) 
                Program.Project.Undo.AddAndExecute(new ColorFilter.ValueUndoEntry(
                    _filter, 
                    old.Value, 
                    value
                ));
        }

        public void SetValue(double value) => Value.RawValue = value * 100;

        void HueTolerance_Changed(Dial sender, double value, double? old) {
            if (old != null && old != value) 
                Program.Project.Undo.AddAndExecute(new ColorFilter.HueToleranceUndoEntry(
                    _filter, 
                    old.Value, 
                    value
                ));
        }

        public void SetHueTolerance(double value) => HueTolerance.RawValue = value * 100;

        void SaturationTolerance_Changed(Dial sender, double value, double? old) {
            if (old != null && old != value) 
                Program.Project.Undo.AddAndExecute(new ColorFilter.SaturationToleranceUndoEntry(
                    _filter, 
                    old.Value, 
                    value
                ));
        }

        public void SetSaturationTolerance(double value) => SaturationTolerance.RawValue = value * 100;

        void ValueTolerance_Changed(Dial sender, double value, double? old) {
            if (old != null && old != value) 
                Program.Project.Undo.AddAndExecute(new ColorFilter.ValueToleranceUndoEntry(
                    _filter, 
                    old.Value, 
                    value
                ));
        }

        public void SetValueTolerance(double value) => ValueTolerance.RawValue = value * 100;
    }
}
