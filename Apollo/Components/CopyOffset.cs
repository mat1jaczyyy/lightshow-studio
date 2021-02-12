using System;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

using Apollo.Core;
using Apollo.Devices;
using Apollo.Enums;
using Apollo.Structures;

namespace Apollo.Components {
    public class CopyOffset: UserControl {
        void InitializeComponent() {
            AvaloniaXamlLoader.Load(this);
            
            _viewer = this.Get<MoveDial>("Offset");
            
            Angle = this.Get<Dial>("Angle");
        }

        public delegate void OffsetEventHandler(int index);
        public event OffsetEventHandler OffsetAdded;
        public event OffsetEventHandler OffsetRemoved;
        
        Offset _offset;
        Copy _copy;
        MoveDial _viewer;
        Dial Angle;

        public bool AngleEnabled {
            get => Angle.Enabled;
            set => Angle.Enabled = value;
        }

        public CopyOffset() => throw new InvalidOperationException();

        public CopyOffset(Offset offset, int angle, Copy copy) {
            InitializeComponent();

            _offset = offset;
            _copy = copy;

            _viewer.Update(_offset);
            _viewer.Changed += Offset_Changed;
            _viewer.AbsoluteChanged += Offset_AbsoluteChanged;
            _viewer.Switched += Offset_Switched;
            
            Angle.RawValue = angle;
            Angle.Enabled = copy.Data.CopyMode.SupportsAngle();
        }

        void Unloaded(object sender, VisualTreeAttachmentEventArgs e) {
            OffsetAdded = null;
            OffsetRemoved = null;

            _viewer.Changed -= Offset_Changed;
            _viewer.AbsoluteChanged -= Offset_AbsoluteChanged;
            _viewer.Switched -= Offset_Switched;

            _offset = null;
            _copy = null;
            _viewer = null;
        }

        void Offset_Add() => OffsetAdded?.Invoke(_copy.Offsets.IndexOf(_offset) + 1);

        void Offset_Remove() => OffsetRemoved?.Invoke(_copy.Offsets.IndexOf(_offset));

        void Offset_Changed(int x, int y, int? old_x, int? old_y) {
            if (old_x != null && old_y != null)
                Program.Project.Undo.AddAndExecute(new Copy.OffsetRelativeUndoEntry(
                    _copy,
                    _copy.Offsets.IndexOf(_offset),
                    old_x.Value,
                    old_y.Value,
                    x,
                    y
                ));
        }

        void Offset_AbsoluteChanged(int x, int y, int? old_x, int? old_y) {
            if (old_x != null && old_y != null)
                Program.Project.Undo.AddAndExecute(new Copy.OffsetAbsoluteUndoEntry(
                    _copy,
                    _copy.Offsets.IndexOf(_offset),
                    old_x.Value,
                    old_y.Value,
                    x,
                    y
                ));
        }

        void Offset_Switched() => Program.Project.Undo.AddAndExecute(new Copy.OffsetSwitchedUndoEntry(
            _copy,
            _copy.Offsets.IndexOf(_offset),
            _offset.Data.IsAbsolute,
            !_offset.Data.IsAbsolute
        ));

        public void SetOffset(Offset offset) => _viewer.Update(offset);
        
        public void Angle_Changed(Dial sender, double angle, double? old){
            if (old != null && old.Value != angle)
                Program.Project.Undo.AddAndExecute(new Copy.OffsetAngleUndoEntry(
                    _copy,
                    _copy.Offsets.IndexOf(_offset),
                    (int)old.Value,
                    (int)angle
                ));
        }
    
        public void SetAngle(double angle) => Angle.RawValue = angle;
    }
}
