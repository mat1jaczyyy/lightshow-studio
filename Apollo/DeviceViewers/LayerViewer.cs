using System;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

using Apollo.Components;
using Apollo.Core;
using Apollo.Devices;
using Apollo.Enums;

namespace Apollo.DeviceViewers {
    public class LayerViewer: UserControl {
        public static readonly string DeviceIdentifier = "layer";

        void InitializeComponent() {
            AvaloniaXamlLoader.Load(this);

            Target = this.Get<Dial>("Target");
            BlendingMode = this.Get<ComboBox>("BlendingMode");
            Range = this.Get<Dial>("Range");
        }
        
        Layer _layer;
        
        Dial Target, Range;
        ComboBox BlendingMode;

        public LayerViewer() => new InvalidOperationException();

        public LayerViewer(Layer layer) {
            InitializeComponent();

            _layer = layer;
            
            Target.RawValue = _layer.Data.Target;
            SetMode(_layer.Data.BlendingMode);
            Range.RawValue = _layer.Data.Range;
        }

        void Unloaded(object sender, VisualTreeAttachmentEventArgs e) => _layer = null;

        void Target_Changed(Dial sender, double value, double? old) {
            if (old != null && old != value)
                Program.Project.Undo.AddAndExecute(new Layer.TargetUndoEntry(
                    _layer,
                    (int)old.Value, 
                    (int)value
                ));
        }

        public void SetTarget(int value) => Target.RawValue = value;

        void Mode_Changed(object sender, SelectionChangedEventArgs e) {
            BlendingType selected = (BlendingType)BlendingMode.SelectedIndex;

            if (_layer.Data.BlendingMode != selected)
                Program.Project.Undo.AddAndExecute(new Layer.ModeUndoEntry(
                    _layer, 
                    _layer.Data.BlendingMode, 
                    selected
                ));
        }

        public void SetMode(BlendingType mode) {
            BlendingMode.SelectedIndex = (int)mode;
            Range.Enabled = mode.SupportsRange();
        }

        void Range_Changed(Dial sender, double value, double? old) {
            if (old != null && old != value)
                Program.Project.Undo.AddAndExecute(new Layer.RangeUndoEntry(
                    _layer,
                    (int)old.Value, 
                    (int)value
                ));
        }

        public void SetRange(int value) => Range.RawValue = value;
    }
}
