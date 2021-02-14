using System;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

using Apollo.Components;
using Apollo.Core;
using Apollo.Devices;

namespace Apollo.DeviceViewers {
    public class OutputViewer: UserControl {
        public static readonly string DeviceIdentifier = "output";

        void InitializeComponent() {
            AvaloniaXamlLoader.Load(this);
            
            Target = this.Get<Dial>("Target");
        }
        
        Output _output;
        Dial Target;

        void Update_Maximum(int value) {
            Target.Enabled = (value != 1);
            if (Target.Enabled) Target.Maximum = value;
        }

        public OutputViewer() => new InvalidOperationException();

        public OutputViewer(Output output) {
            InitializeComponent();
            
            _output = output;
            Program.Project.TrackCountChanged += Update_Maximum;

            Update_Maximum(Program.Project.Tracks.Count);
            SetTarget(_output.Data.Target);
        }

        void Unloaded(object sender, VisualTreeAttachmentEventArgs e) {
            Program.Project.TrackCountChanged -= Update_Maximum;

            _output = null;
        }

        void Target_Changed(Dial sender, double value, double? old) {
            if (old != null && old != value)
                Program.Project.Undo.AddAndExecute(new Output.TargetUndoEntry(
                    _output,
                    (int)old.Value,
                    (int)value
                ));
        }

        public void SetTarget(int value) => Target.RawValue = value + 1;
    }
}
