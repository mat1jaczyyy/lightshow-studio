using System;
using System.Collections.Generic;
using System.Linq;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Media;

using Apollo.Components;
using Apollo.Core;
using Apollo.Elements;
using Apollo.Selection;

namespace Apollo.Viewers {
    public class ChainViewer: UserControl, ISelectParentViewer, IDroppable {
        public int? IExpanded {
            get => null;
        }

        void InitializeComponent() {
            AvaloniaXamlLoader.Load(this);

            DropZoneBefore = this.Get<Grid>("DropZoneBefore");
            DropZoneAfter = this.Get<Grid>("DropZoneAfter");

            Contents = this.Get<StackPanel>("Contents").Children;
            DeviceAdd = this.Get<DeviceAdd>("DeviceAdd");

            Indicator = this.Get<Indicator>("Indicator");
        }
        
        Chain _chain;

        Grid DropZoneBefore, DropZoneAfter;

        Controls Contents;
        DeviceAdd DeviceAdd;
        public Indicator Indicator { get; private set; }

        void SetAlwaysShowing() {
            bool RootChain = _chain.Parent is Track;

            DeviceAdd.AlwaysShowing = Contents.Count == 1 || RootChain;

            for (int i = 1; i < Contents.Count; i++)
                ((DeviceViewer)Contents[i]).DeviceAdd.AlwaysShowing = false;

            if (Contents.Count > 1 && RootChain) ((DeviceViewer)Contents.Last()).DeviceAdd.AlwaysShowing = true;
        }

        public void Contents_Insert(int index, Device device) {
            DeviceViewer viewer = device.Collapsed? (DeviceViewer)new CollapsedDeviceViewer(device) : new DeviceViewer(device);
            viewer.Added += Device_Insert;
            viewer.DeviceCollapsed += Device_Collapsed;

            Contents.Insert(index + 1, viewer);
            SetAlwaysShowing();
        }

        public void Contents_Remove(int index) {
            Contents.RemoveAt(index + 1);
            SetAlwaysShowing();
        }

        public ChainViewer() => new InvalidOperationException();

        public ChainViewer(Chain chain, bool backgroundBorder = false) {
            InitializeComponent();

            _chain = chain;
            _chain.Viewer = this;

            DragDrop = new DragDropManager(this);

            for (int i = 0; i < _chain.Count; i++)
                Contents_Insert(i, _chain[i]);
            
            if (backgroundBorder) {
                this.Get<Grid>("Root").Children.Insert(0, new DeviceBackground());
                Background = App.GetResource<IBrush>("ThemeControlDarkenBrush");
            }
        }

        void Unloaded(object sender, VisualTreeAttachmentEventArgs e) {
            _chain.Viewer = null;
            _chain = null;
            
            DragDrop.Dispose();
            DragDrop = null;
        }

        public void Expand(int? index) {}

        void Device_Insert(int index, Type device) => Device_Insert(index, Device.Create(device, _chain));
        void Device_InsertStart(Type device) => Device_Insert(0, device);

        void Device_Insert(int index, Device device) {
            Program.Project.Undo.AddAndExecute(new Chain.DeviceInsertedUndoEntry(
                _chain,
                index,
                device
            ));
        }

        void Device_Collapsed(int index) {
            Contents_Remove(index);
            Contents_Insert(index, _chain[index]);
            
            Track.Get(_chain[index]).Window?.Selection.Select(_chain[index]);
        }

        void Device_Action(string action) => Device_Action(action, false);
        void Device_Action(string action, bool right) => Track.Get(_chain)?.Window?.Selection.Action(action, _chain, (right? _chain.Count : 0) - 1);

        void ContextMenu_Action(ApolloContextMenu sender, string action) =>
            Device_Action(action, sender == (ApolloContextMenu)this.Resources["DeviceContextMenuAfter"]);

        void Click(object sender, PointerReleasedEventArgs e) {
            PointerUpdateKind MouseButton = e.GetCurrentPoint(this).Properties.PointerUpdateKind;

            if (MouseButton == PointerUpdateKind.RightButtonReleased)
                if (sender == DropZoneBefore) ((ApolloContextMenu)this.Resources["DeviceContextMenuBefore"]).Open((Control)sender);
                else if (sender == DropZoneAfter) ((ApolloContextMenu)this.Resources["DeviceContextMenuAfter"]).Open((Control)sender);

            e.Handled = true;
        }

        DragDropManager DragDrop;

        public List<string> DropAreas => new List<string>() {"DropZoneBefore", "DropZoneAfter", "DeviceAdd"};

        public Dictionary<string, DragDropManager.DropHandler> DropHandlers => new Dictionary<string, DragDropManager.DropHandler>() {
            {DataFormats.FileNames, null},
            {"Device", null}
        };

        public ISelect Item => null;
        public ISelectParent ItemParent => _chain;
    }
}
