using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Media;

using Apollo.Components;
using Apollo.Core;
using Apollo.Devices;
using Apollo.Elements;
using Apollo.Selection;

namespace Apollo.Viewers {
    public class DeviceViewer: UserControl, ISelectViewer, IDraggable {
        protected virtual void InitializeComponent() {
            AvaloniaXamlLoader.Load(this);

            TitleText = this.Get<TextBlock>("Title");
            Root = this.Get<StackPanel>("Root");
            Border = this.Get<Border>("Border");
            Header = this.Get<Border>("Header");
            
            DeviceAdd = this.Get<DeviceAdd>("DropZoneAfter");
            Draggable = this.Get<Grid>("Draggable");

            Indicator = this.Get<Indicator>("Indicator");

            DeviceMute = this.Get<MenuItem>("DeviceMute");
            GroupMute = this.Get<MenuItem>("GroupMute");
            ChokeMute = this.Get<MenuItem>("ChokeMute");
        }

        static IControl GetSpecificViewer(DeviceViewer sender, Device device) {
            foreach (Type deviceViewer in Assembly.GetExecutingAssembly().GetTypes().Where(i => i.ReflectedType == null && i.Namespace.StartsWith("Apollo.DeviceViewers")))
                if ((string)deviceViewer.GetField("DeviceIdentifier").GetValue(null) == device.DeviceIdentifier) {
                    if (device.DeviceIdentifier == "group" || device.DeviceIdentifier == "multi" || device.DeviceIdentifier == "choke")
                        return (IControl)Activator.CreateInstance(deviceViewer, new object[] {device, sender});
                    
                    return (IControl)Activator.CreateInstance(deviceViewer, new object[] {device});
                }

            return null;
        }

        public delegate void AddedEventHandler(int index, Type device);
        public event AddedEventHandler Added;

        public delegate void DeviceCollapsedEventHandler(int index);
        public event DeviceCollapsedEventHandler DeviceCollapsed;

        protected void ResetEvents() {
            Added = null;
            DeviceCollapsed = null;
        }
        
        protected Device _device;
        public bool Selected { get; protected set; } = false;

        public IControl SpecificViewer { get; protected set; }

        public StackPanel Root;
        public Border Border, Header;
        public DeviceAdd DeviceAdd { get; protected set; }
        public Indicator Indicator { get; protected set; }

        protected TextBlock TitleText;
        protected Grid Draggable;
        protected ApolloContextMenu DeviceContextMenu, GroupContextMenu, ChokeContextMenu;
        protected MenuItem DeviceMute, GroupMute, ChokeMute;

        protected virtual void ApplyHeaderBrush(string resource) {
            IBrush brush = App.GetResource<IBrush>(resource);

            if (Root.Children[0] is DeviceHead targetHead) {
                if (IsArrangeValid) targetHead.Header.Background = brush;
                else targetHead.Resources["TitleBrush"] = brush;
            }

            if (Root.Children[Root.Children.Count - 2] is DeviceTail targetTail) {
                if (IsArrangeValid) targetTail.Header.Background = brush;
                else targetTail.Resources["TitleBrush"] = brush;
            }

            if (IsArrangeValid) Header.Background = brush;
            else this.Resources["TitleBrush"] = brush;
        }

        public void Select() {
            ApplyHeaderBrush("ThemeAccentBrush2");
            Selected = true;
        }

        public void Deselect() {
            ApplyHeaderBrush("ThemeControlLowBrush");
            Selected = false;
        }

        public DeviceViewer() => InitializeComponent();

        public DeviceViewer(Device device) {
            InitializeComponent();

            TitleText.Text = device.Name;

            _device = device;
            _device.Viewer = this;
            Deselect();

            DeviceContextMenu = (ApolloContextMenu)this.Resources["DeviceContextMenu"];
            GroupContextMenu = (ApolloContextMenu)this.Resources["GroupContextMenu"];
            ChokeContextMenu = (ApolloContextMenu)this.Resources["ChokeContextMenu"];
            
            DragDrop = new DragDropManager(this);

            SpecificViewer = GetSpecificViewer(this, _device);

            if (SpecificViewer != null)
                this.Get<Grid>("Contents").Children.Add(SpecificViewer);
            
            SetEnabled();
        }

        protected virtual void Unloaded(object sender, VisualTreeAttachmentEventArgs e) {
            ResetEvents();

            SpecificViewer = null;
            _device.Viewer = null;
            _device = null;

            DeviceContextMenu = GroupContextMenu = ChokeContextMenu = null;

            DragDrop.Dispose();
            DragDrop = null;
        }

        public virtual void SetEnabled() {
            Border.Background = App.GetResource<IBrush>(_device.Enabled? "ThemeControlHighBrush" : "ThemeControlMidBrush");
            Border.BorderBrush = App.GetResource<IBrush>(_device.Enabled? "ThemeBorderMidBrush" : "ThemeBorderLowBrush");
            TitleText.Foreground = App.GetResource<IBrush>(_device.Enabled? "ThemeForegroundBrush" : "ThemeForegroundLowBrush");

            if (Root.Children[0] is DeviceHead targetHead)
                targetHead.SetEnabled(_device.Enabled);

            if (Root.Children[Root.Children.Count - 2] is DeviceTail targetTail)
                targetTail.SetEnabled(_device.Enabled);
        }

        protected void Device_Add(Type device) => Added?.Invoke(_device.ParentIndex.Value + 1, device);

        protected void Device_Action(string action) => Track.Get(_device)?.Window?.Selection.Action(action, _device.Parent, _device.ParentIndex.Value);

        protected void ContextMenu_Action(string action) => Track.Get(_device)?.Window?.Selection.Action(action);

        public void Select(PointerPressedEventArgs e) {
            PointerUpdateKind MouseButton = e.GetCurrentPoint(this).Properties.PointerUpdateKind;

            if (MouseButton == PointerUpdateKind.LeftButtonPressed || (MouseButton == PointerUpdateKind.RightButtonPressed && !Selected))
                Track.Get(_device)?.Window?.Selection.Select(_device, e.KeyModifiers.HasFlag(KeyModifiers.Shift));
        }

        public DragDropManager DragDrop { get; protected set; }

        public string DragFormat => "Device";
        public List<string> DropAreas => new List<string>() {"DropZoneHead", "Contents", "DropZoneTail", "DropZoneAfter"};

        public Dictionary<string, DragDropManager.DropHandler> DropHandlers => new Dictionary<string, DragDropManager.DropHandler>() {
            {DataFormats.FileNames, null},
            {DragFormat, null}
        };

        public ISelect Item => _device;
        public ISelectParent ItemParent => Item.IParent;

        public void DragFailed(PointerPressedEventArgs e) {
            PointerUpdateKind MouseButton = e.GetCurrentPoint(this).Properties.PointerUpdateKind;
            
            if (MouseButton == PointerUpdateKind.RightButtonPressed) {
                ApolloContextMenu menu = DeviceContextMenu;
                List<ISelect> selection = Track.Get(_device)?.Window?.Selection.Selection;

                if (selection.Count == 1) {
                    if (selection[0] is Group group && group.Count == 1)
                        menu = GroupContextMenu;

                    else if (selection[0] is Choke)
                        menu = ChokeContextMenu;
                }
                
                DeviceMute.Header = GroupMute.Header = ChokeMute.Header = ((Device)selection.First()).Enabled? "Mute" : "Unmute";

                menu.Open(Draggable);
            
            } else if (MouseButton == PointerUpdateKind.LeftButtonPressed && e.ClickCount == 2) {
                _device.Collapsed = !_device.Collapsed;
                DeviceCollapsed?.Invoke(_device.ParentIndex.Value);
            }
        }

        public void Drag(object sender, PointerPressedEventArgs e) => DragDrop.Drag(Track.Get(_device)?.Window?.Selection, e);

        public bool DropLeft(IControl source, DragEventArgs e) 
            => source.Name == "DropZoneHead" || (source.Name == "Contents" && e.GetPosition(source).X < source.Bounds.Width / 2);
    }
}
