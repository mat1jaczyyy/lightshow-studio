using System;
using System.Collections.Generic;
using System.Linq;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using Avalonia.Threading;

using Apollo.Core;
using Apollo.Elements;

namespace Apollo.Components {
    public class PortSelector: ComboBox, IStyleable {
        Type IStyleable.StyleKey => typeof(ComboBox);

        public delegate void PortChangedEventHandler(Launchpad lp);
        public event PortChangedEventHandler PortChanged;

        bool _noalp = false;
        public bool NoAbletonLaunchpads {
            get => _noalp;
            set {
                if (_noalp != value) {
                    _noalp = value;
                    Update();
                }
            }
        }

        public void Update(Launchpad selected) {
            if (!Dispatcher.UIThread.CheckAccess()) {
                Dispatcher.UIThread.InvokeAsync(() => Update(selected));
                return;
            }

            List<Launchpad> ports = MIDI.UsableDevices;

            if (NoAbletonLaunchpads)
                ports = ports.Where(i => i.GetType() != typeof(AbletonLaunchpad)).ToList();

            if (selected?.Usable == false) ports.Add(selected);

            ports.Add(MIDI.NoOutput);

            try {
                Items = ports;
                SelectedIndex = -1;
                SelectedItem = selected;
            } catch {}
        }

        void Update() => Update((Launchpad)SelectedItem);

        public PortSelector() {
            AvaloniaXamlLoader.Load(this);

            Update();
            MIDI.DevicesUpdated += Update;
        }

        void Unloaded(object sender, VisualTreeAttachmentEventArgs e) {
            MIDI.DevicesUpdated -= Update;

            PortChanged = null;
        }

        void Changed(object sender, SelectionChangedEventArgs e) => PortChanged?.Invoke((Launchpad)SelectedItem);
    }
}