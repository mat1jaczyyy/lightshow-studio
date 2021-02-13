using System;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

using Apollo.Components;
using Apollo.Core;
using Apollo.Devices;
using Apollo.Structures;

namespace Apollo.DeviceViewers {
    public class DelayViewer: UserControl {
        public static readonly string DeviceIdentifier = "delay";

        void InitializeComponent() {
            AvaloniaXamlLoader.Load(this);
            
            Duration = this.Get<Dial>("Duration");
            Gate = this.Get<Dial>("Gate");
        }
        
        Delay _delay;
        Dial Duration, Gate;

        public DelayViewer() => new InvalidOperationException();

        public DelayViewer(Delay delay) {
            InitializeComponent();

            _delay = delay;
            
            Duration.UsingSteps = _delay.Time.Data.Mode;
            Duration.Length = _delay.Time.Length;
            Duration.RawValue = _delay.Time.Data.Free;

            Gate.RawValue = _delay.Data.Gate * 100;
        }

        void Duration_Changed(Dial sender, double value, double? old) {
            if (old != null && old != value)
                Program.Project.Undo.AddAndExecute(new Delay.DurationUndoEntry(
                    _delay,
                    (int)old.Value,
                    (int)value
                ));
        }

        void Unloaded(object sender, VisualTreeAttachmentEventArgs e) => _delay = null;

        public void SetDurationValue(int duration) => Duration.RawValue = duration;

        void Duration_ModeChanged(bool value, bool? old) {
            if (old != null && old != value)
                Program.Project.Undo.AddAndExecute(new Delay.DurationModeUndoEntry(
                    _delay, 
                    old.Value, 
                    value
                ));
        }

        public void SetMode(bool mode) => Duration.UsingSteps = mode;

        void Duration_StepChanged(int value, int? old) {
            if (old != null && old != value)
                Program.Project.Undo.AddAndExecute(new Delay.DurationStepUndoEntry(
                    _delay, 
                    old.Value, 
                    value
                ));
        }

        public void SetDurationStep(Length duration) => Duration.Length = duration;

        void Gate_Changed(Dial sender, double value, double? old) {
            if (old != null && old != value)
                Program.Project.Undo.AddAndExecute(new Delay.GateUndoEntry(
                    _delay, 
                    old.Value, 
                    value
                ));
        }

        public void SetGate(double gate) => Gate.RawValue = gate * 100;
    }
}
