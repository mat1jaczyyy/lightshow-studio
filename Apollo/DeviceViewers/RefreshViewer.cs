using System;
using System.Linq;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Interactivity;

using Apollo.Core;
using Apollo.Devices;

namespace Apollo.DeviceViewers {
    public class RefreshViewer: UserControl {
        public static readonly string DeviceIdentifier = "refresh";

        void InitializeComponent() {
            AvaloniaXamlLoader.Load(this);
            Macros = ((StackPanel)Content).Children.OfType<CheckBox>().ToArray();
        }
        
        Refresh _refresh;
        CheckBox[] Macros = new CheckBox[4];

        public RefreshViewer() => new InvalidOperationException();

        public RefreshViewer(Refresh refresh) {
            InitializeComponent();

            _refresh = refresh;
            
            for (int i = 0; i < 4; i++)
                Macros[i].IsChecked = _refresh.Data.GetMacro(i);
        }

        void Unloaded(object sender, VisualTreeAttachmentEventArgs e) => _refresh = null;

        void Macro_Changed(object sender, RoutedEventArgs e) {
            CheckBox source = (CheckBox)sender;
            int index = Array.IndexOf(Macros, source);
            bool value = source.IsChecked.Value;

            if (_refresh.Data.GetMacro(index) != value) 
                Program.Project.Undo.AddAndExecute(new Refresh.MacroUndoEntry(
                    _refresh, 
                    index, 
                    _refresh.Data.GetMacro(index), 
                    value
                ));
        }

        public void SetMacro(int index, bool value) => Macros[index].IsChecked = value;
    }
}
