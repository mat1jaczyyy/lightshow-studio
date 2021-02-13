using System;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

using Apollo.Components;
using Apollo.Core;
using Apollo.Devices;
using Apollo.Structures;

namespace Apollo.DeviceViewers {
    public class LoopViewer: UserControl {
        public static readonly string DeviceIdentifier = "loop";
        
        Loop _loop;
        
        Dial Rate, Gate, Repeats;
        CheckBox Hold;
        
        void InitializeComponent(){
            AvaloniaXamlLoader.Load(this);
            
            Rate = this.Get<Dial>("Rate");
            Gate = this.Get<Dial>("Gate");
            Repeats = this.Get<Dial>("Repeats");
            
            Hold = this.Get<CheckBox>("Hold");
        }
        
        public LoopViewer() => new InvalidOperationException();

        public LoopViewer(Loop loop) {
            InitializeComponent();
            
            _loop = loop;
            
            Rate.UsingSteps = _loop.Rate.Data.Mode;
            Rate.Length = _loop.Rate.Length;
            Rate.RawValue = _loop.Rate.Data.Free;
            
            Gate.RawValue = _loop.Data.Gate * 100;
            
            Repeats.RawValue = _loop.Data.Repeats;
            SetHold(_loop.Data.Hold);
        }
        
        void Unloaded(object sender, VisualTreeAttachmentEventArgs e) => _loop = null;

        void Rate_Changed(Dial sender, double value, double? old) {
            if (old != null && old != value)
                Program.Project.Undo.AddAndExecute(new Loop.RateUndoEntry(
                    _loop,
                    (int)old.Value, 
                    (int)value
                ));
        }
        
        public void SetRateValue(int value) => Rate.RawValue = value;
        
        void Rate_StepChanged(int value, int? old) {
            if (old != null && old != value)
                Program.Project.Undo.AddAndExecute(new Loop.RateStepUndoEntry(
                    _loop, 
                    old.Value, 
                    value
                ));
        }
        
        public void SetRateStep(Length rate) => Rate.Length = rate;
        
        void Rate_ModeChanged(bool value, bool? old) {
            if (old != null && old != value)
                Program.Project.Undo.AddAndExecute(new Loop.RateModeUndoEntry(
                    _loop, 
                    old.Value, 
                    value
                ));
        }
        
        void Hold_Changed(object sender, RoutedEventArgs e) {
            bool value = Hold.IsChecked.Value;

            if (_loop.Data.Hold != value)
                Program.Project.Undo.AddAndExecute(new Loop.HoldUndoEntry(
                    _loop, 
                    _loop.Data.Hold, 
                    value
                ));
        }
        
        public void SetHold(bool hold) {
            Hold.IsChecked = hold;
            Repeats.Enabled = !hold;
        }
        
        public void SetMode(bool mode) => Rate.UsingSteps = mode;
        
        void Gate_Changed(Dial sender, double value, double? old){
            if (old != null && old != value)
                Program.Project.Undo.AddAndExecute(new Loop.GateUndoEntry(
                    _loop, 
                    old.Value, 
                    value
                ));
        }
        
        public void SetGate(double gate) => Gate.RawValue = gate * 100;
        
        void Repeats_Changed(Dial sender, double value, double? old){
            if (old != null && old != value)
                Program.Project.Undo.AddAndExecute(new Loop.RepeatsUndoEntry(
                    _loop,
                    (int)old.Value, 
                    (int)value
                ));
        }
        public void SetRepeats(int repeats) => Repeats.RawValue = repeats;
    }   
}