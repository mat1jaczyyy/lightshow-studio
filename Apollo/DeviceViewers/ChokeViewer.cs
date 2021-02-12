using System;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

using Apollo.Components;
using Apollo.Core;
using Apollo.Devices;
using Apollo.Viewers;

namespace Apollo.DeviceViewers {
    public class ChokeViewer: UserControl {
        public static readonly string DeviceIdentifier = "choke";

        void InitializeComponent() {
            AvaloniaXamlLoader.Load(this);

            Target = this.Get<Dial>("Target");
        }
        
        Choke _choke;

        Dial Target;

        public ChokeViewer() => new InvalidOperationException();

        public ChokeViewer(Choke choke, DeviceViewer parent) {
            InitializeComponent();

            _choke = choke;

            Target.RawValue = _choke.Data.Target;

            parent.Border.CornerRadius = new CornerRadius(5, 0, 0, 5);
            parent.Header.CornerRadius = new CornerRadius(5, 0, 0, 0);

            parent.Root.Children.Insert(1, new ChainViewer(choke.Chain, true));
            parent.Root.Children.Insert(2, new DeviceTail(_choke, parent));
        }

        void Unloaded(object sender, VisualTreeAttachmentEventArgs e) => _choke = null;

        void Target_Changed(Dial sender, double value, double? old){
            if (old != null && old != value) 
                Program.Project.Undo.AddAndExecute(new Choke.TargetUndoEntry(
                    _choke, 
                    (int)old.Value, 
                    (int)value
                ));
        }

        public void SetTarget(int value) => Target.RawValue = value;
    }
}
