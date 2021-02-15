using System;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

using Apollo.Components;
using Apollo.Core;
using Apollo.Devices;
using Apollo.Structures;

namespace Apollo.DeviceViewers {
    public class PaintViewer: UserControl {
        public static readonly string DeviceIdentifier = "paint";

        void InitializeComponent() {
            AvaloniaXamlLoader.Load(this);
            
            Picker = this.Get<ColorPicker>("Picker");
        }
        
        Paint _paint;
        ColorPicker Picker;

        public PaintViewer() => new InvalidOperationException();

        public PaintViewer(Paint paint) {
            InitializeComponent();

            _paint = paint;

            Picker.SetColor(_paint.Data.Color);
        }

        void Unloaded(object sender, VisualTreeAttachmentEventArgs e) => _paint = null;
        
        void Color_Changed(Color color, Color old) {
            if (old != null)
                Program.Project.Undo.AddAndExecute(new Paint.ColorUndoEntry(
                    _paint, 
                    old.Clone(), 
                    color.Clone()
                ));
        }

        public void Set(Color color) => Picker.SetColor(color);
    }
}
