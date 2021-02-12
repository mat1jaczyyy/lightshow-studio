using System;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

using Apollo.Components;
using Apollo.Core;
using Apollo.Devices;
using Apollo.Enums;
using Apollo.Structures;

namespace Apollo.DeviceViewers {
    public class CopyViewer: UserControl {
        public static readonly string DeviceIdentifier = "copy";

        void InitializeComponent() {
            AvaloniaXamlLoader.Load(this);

            Rate = this.Get<Dial>("Rate");
            Gate = this.Get<Dial>("Gate");
            Pinch = this.Get<PinchDial>("Pinch");

            CopyMode = this.Get<ComboBox>("CopyMode");
            GridMode = this.Get<ComboBox>("GridMode");
            Wrap = this.Get<CheckBox>("Wrap");

            Reverse = this.Get<CheckBox>("Reverse");
            Infinite = this.Get<CheckBox>("Infinite");
            
            Contents = this.Get<StackPanel>("Contents").Children;
            OffsetAdd = this.Get<HorizontalAdd>("OffsetAdd");
        }

        Copy _copy;

        Dial Rate, Gate;
        PinchDial Pinch;
        ComboBox CopyMode, GridMode;
        CheckBox Wrap, Reverse, Infinite;

        Controls Contents;
        HorizontalAdd OffsetAdd;

        public void Contents_Insert(int index, Offset offset, int angle) {
            CopyOffset viewer = new CopyOffset(offset, angle, _copy);
            viewer.OffsetAdded += Offset_Insert;
            viewer.OffsetRemoved += Offset_Remove;

            Contents.Insert(index + 1, viewer);
            OffsetAdd.AlwaysShowing = false;
        }

        public void Contents_Remove(int index) {
            Contents.RemoveAt(index + 1);
            if (Contents.Count == 1) OffsetAdd.AlwaysShowing = true;
        }

        public CopyViewer() => new InvalidOperationException();

        public CopyViewer(Copy copy) {
            InitializeComponent();

            _copy = copy;

            Rate.UsingSteps = _copy.Time.Data.Mode;
            Rate.Length = _copy.Time.Length;
            Rate.RawValue = _copy.Time.Data.Free;

            Gate.RawValue = _copy.Data.Gate * 100;
            Pinch.RawValue = _copy.Data.Pinch;
            Pinch.IsBilateral = _copy.Data.Bilateral;

            Reverse.IsChecked = _copy.Data.Reverse;
            Infinite.IsChecked = _copy.Data.Infinite;

            GridMode.SelectedIndex = (int)_copy.Data.GridMode;
            
            SetCopyMode(_copy.Data.CopyMode);

            Wrap.IsChecked = _copy.Data.Wrap;

            for (int i = 0; i < _copy.Offsets.Count; i++)
                Contents_Insert(i, _copy.Offsets[i], _copy.GetAngle(i));
        }

        void Unloaded(object sender, VisualTreeAttachmentEventArgs e) => _copy = null;

        void Rate_ValueChanged(Dial sender, double value, double? old) {
            if (old != null && old != value) 
                Program.Project.Undo.AddAndExecute(new Copy.RateUndoEntry(
                    _copy,
                    (int)old.Value, 
                    (int)value
                ));
        }

        public void SetRateValue(int rate) => Rate.RawValue = rate;

        void Rate_ModeChanged(bool value, bool? old) {
            if (old != null && old != value) 
                Program.Project.Undo.AddAndExecute(new Copy.RateModeUndoEntry(
                    _copy, 
                    old.Value, 
                    value
                ));
        }

        public void SetMode(bool mode) => Rate.UsingSteps = mode;

        void Rate_StepChanged(int value, int? old) {
            if (old != null && old != value) 
                Program.Project.Undo.AddAndExecute(new Copy.RateStepUndoEntry(
                    _copy, 
                    old.Value, 
                    value
                ));
        }

        public void SetRateStep(Length rate) => Rate.Length = rate;

        void Gate_Changed(Dial sender, double value, double? old) {
            if (old != null && old != value) 
                Program.Project.Undo.AddAndExecute(new Copy.GateUndoEntry(
                    _copy, 
                    old.Value, 
                    value
                ));
        }

        public void SetGate(double gate) => Gate.RawValue = gate * 100;

        void CopyMode_Changed(object sender, SelectionChangedEventArgs e) {
            CopyType selected = (CopyType)CopyMode.SelectedIndex;

            if (_copy.Data.CopyMode != selected)
                Program.Project.Undo.AddAndExecute(new Copy.CopyModeUndoEntry(
                    _copy,
                    _copy.Data.CopyMode,
                    selected,
                    CopyMode.Items
                ));
        }

        public void SetCopyMode(CopyType mode) {
            CopyMode.SelectedIndex = (int)mode;

            Rate.Enabled = Gate.Enabled = mode.SupportsRate();
            Pinch.Enabled = Reverse.IsEnabled = Infinite.IsEnabled = mode.SupportsPinch();
            
            for (int i = 1; i < Contents.Count; i++)
                ((CopyOffset)Contents[i]).AngleEnabled = mode.SupportsAngle();
        }

        void GridMode_Changed(object sender, SelectionChangedEventArgs e) {
            GridType selected = (GridType)GridMode.SelectedIndex;

            if (_copy.Data.GridMode != selected) 
                Program.Project.Undo.AddAndExecute(new Copy.GridModeUndoEntry(
                    _copy, 
                    _copy.Data.GridMode, 
                    selected,
                    GridMode.Items
                ));
        }

        public void SetGridMode(GridType mode) => GridMode.SelectedIndex = (int)mode;

        void Pinch_Changed(Dial sender, double value, double? old) {
            if (old != null && old != value)
                Program.Project.Undo.AddAndExecute(new Copy.PinchUndoEntry(
                    _copy, 
                    old.Value, 
                    value
                ));
        }

        public void SetPinch(double pinch) => Pinch.RawValue = pinch;
        
        void Bilateral_Changed(bool value, bool? old) {
            if (old != null && old != value)
                Program.Project.Undo.AddAndExecute(new Copy.BilateralUndoEntry(
                    _copy, 
                    old.Value, 
                    value
                ));
        }
        
        public void SetBilateral(bool bilateral) => Pinch.IsBilateral = bilateral;

        void Reverse_Changed(object sender, RoutedEventArgs e) {
            bool value = Reverse.IsChecked.Value;

            if (_copy.Data.Reverse != value)
                Program.Project.Undo.AddAndExecute(new Copy.ReverseUndoEntry(
                    _copy, 
                    _copy.Data.Reverse, 
                    value
                ));
        }

        public void SetReverse(bool value) => Reverse.IsChecked = value;

        void Infinite_Changed(object sender, RoutedEventArgs e) {
            bool value = Infinite.IsChecked.Value;

            if (_copy.Data.Infinite != value)
                Program.Project.Undo.AddAndExecute(new Copy.InfiniteUndoEntry(
                    _copy, 
                    _copy.Data.Infinite, 
                    value
                ));
        }

        public void SetInfinite(bool value) => Infinite.IsChecked = value;

        void Wrap_Changed(object sender, RoutedEventArgs e) {
            bool value = Wrap.IsChecked.Value;

            if (_copy.Data.Wrap != value)
                Program.Project.Undo.AddAndExecute(new Copy.WrapUndoEntry(
                    _copy, 
                    _copy.Data.Wrap, 
                    value
                ));
        }

        public void SetWrap(bool value) => Wrap.IsChecked = value;

        void Offset_InsertStart() => Offset_Insert(0);

        void Offset_Insert(int index) => Program.Project.Undo.AddAndExecute(new Copy.OffsetInsertUndoEntry(
            _copy, 
            index
        ));

        void Offset_Remove(int index) => Program.Project.Undo.AddAndExecute(new Copy.OffsetRemoveUndoEntry(
            _copy, 
            _copy.Offsets[index], 
            _copy.GetAngle(index),
            index
        ));

        public void SetOffset(int index, Offset offset) => ((CopyOffset)Contents[index + 1]).SetOffset(offset);
        
        public void SetOffsetAngle(int index, double angle) => ((CopyOffset)Contents[index + 1]).SetAngle(angle);
    }
}
