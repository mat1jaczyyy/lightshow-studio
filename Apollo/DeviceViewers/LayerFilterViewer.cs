using System;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

using Apollo.Components;
using Apollo.Core;
using Apollo.Devices;

namespace Apollo.DeviceViewers {
    public class LayerFilterViewer: UserControl {
        public static readonly string DeviceIdentifier = "layerfilter";

        void InitializeComponent() {
            AvaloniaXamlLoader.Load(this);

            Target = this.Get<Dial>("Target");
            Range = this.Get<Dial>("Range");
        }
        
        LayerFilter _filter;
        
        Dial Target, Range;

        public LayerFilterViewer() => new InvalidOperationException();

        public LayerFilterViewer(LayerFilter filter) {
            InitializeComponent();

            _filter = filter;
            
            Target.RawValue = _filter.Data.Target;
            Range.RawValue = _filter.Data.Range;
        }

        void Unloaded(object sender, VisualTreeAttachmentEventArgs e) => _filter = null;

        void Target_Changed(Dial sender, double value, double? old) {
            if (old != null && old != value) 
                Program.Project.Undo.AddAndExecute(new LayerFilter.TargetUndoEntry(
                    _filter,
                    (int)old.Value,
                    (int)value
                ));
        }

        public void SetTarget(int value) => Target.RawValue = value;

        void Range_Changed(Dial sender, double value, double? old) {
            if (old != null && old != value)
                Program.Project.Undo.AddAndExecute(new LayerFilter.RangeUndoEntry(
                    _filter,
                    (int)old.Value, 
                    (int)value
                ));
        }

        public void SetRange(int value) => Range.RawValue = value;
    }
}
