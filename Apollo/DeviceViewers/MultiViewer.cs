using System;
using System.Linq;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;

using Apollo.Components;
using Apollo.Core;
using Apollo.Devices;
using Apollo.Elements;
using Apollo.Enums;
using Apollo.Viewers;

namespace Apollo.DeviceViewers {
    public class MultiViewer: GroupViewer {
        public static readonly new string DeviceIdentifier = "multi";

        protected override void InitializeComponent() {
            AvaloniaXamlLoader.Load(this);

            MultiMode = this.Get<ComboBox>("MultiMode");
            
            Contents = this.Get<StackPanel>("Contents").Children;
            ChainAdd = this.Get<VerticalAdd>("ChainAdd");

            Grid = this.Get<LaunchpadGrid>("Grid");
            GridContainer = this.Get<Border>("GridContainer");
        }
        
        Multi _multi => (Multi)_group;

        ComboBox MultiMode;

        LaunchpadGrid Grid;
        Border GridContainer;

        SolidColorBrush GetColor(bool value) => App.GetResource<SolidColorBrush>(value? "ThemeAccentBrush" : "ThemeForegroundLowBrush");

        public MultiViewer() => new InvalidOperationException();

        public MultiViewer(Multi multi, DeviceViewer parent): base(multi, parent) {
            _multi.Preprocess.ClearParentIndexChanged();

            if (_multi.Expanded == null) {
                _parent.Border.CornerRadius = new CornerRadius(0, 5, 5, 0);
                _parent.Header.CornerRadius = new CornerRadius(0, 5, 0, 0);
            }

            _root.Insert(0, new DeviceHead(_multi, parent));
            _root.Insert(1, new ChainViewer(_multi.Preprocess, true));

            ExpandBase = 3;

            SetMode(_multi.Mode);
        }

        protected override void Expand_Insert(int index) {
            base.Expand_Insert(index);

            _parent.Border.CornerRadius = new CornerRadius(0);
            _parent.Header.CornerRadius = new CornerRadius(0);

            GridContainer.MaxWidth = double.MaxValue;
            Set(null, _multi[index].SecretMultiFilter);
        }

        protected override void Expand_Remove() {
            base.Expand_Remove();

            _parent.Border.CornerRadius = new CornerRadius(0, 5, 5, 0);
            _parent.Header.CornerRadius = new CornerRadius(0, 5, 0, 0);

            GridContainer.MaxWidth = 0;
        }

        void Mode_Changed(object sender, SelectionChangedEventArgs e) {
            MultiType selected = (MultiType)MultiMode.SelectedIndex;

            if (_multi.Mode != selected)
                Program.Project.Undo.AddAndExecute(new Multi.ModeUndoEntry(
                    _multi,
                    _multi.Mode,
                    selected,
                    MultiMode.Items
                ));
        }

        public void SetMode(MultiType mode) {
            MultiMode.SelectedIndex = (int)mode;

            GridContainer.IsVisible = mode.UsesKeySelector();
        }
    
        bool drawingState;
        bool[] old;

        void PadStarted(int index) {
            bool[] filter = _multi[(int)_multi.Expanded].SecretMultiFilter;
            drawingState = !filter[LaunchpadGrid.GridToSignal(index)];
            old = filter.ToArray();
        }

        void PadPressed(int index) => Grid.SetColor(
            index,
            GetColor(_multi[(int)_multi.Expanded].SecretMultiFilter[LaunchpadGrid.GridToSignal(index)] = drawingState)
        );

        void PadFinished(int index) {
            if (old == null) return;

            Program.Project.Undo.Add(new Multi.FilterChangedUndoEntry(
                _multi,
                (int)_multi.Expanded,
                old
            ));

            old = null;
        }

        public void Set(Chain chain, bool[] filter) {
            int index = _multi.Chains.IndexOf(chain);

            if (index != -1 && _multi.Expanded != index) return;

            for (int i = 0; i < 100; i++)
                Grid.SetColor(LaunchpadGrid.SignalToGrid(i), GetColor(filter[i]));
        }
    }
}