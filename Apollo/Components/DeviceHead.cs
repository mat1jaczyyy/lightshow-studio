using System;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Media;

using Apollo.Core;
using Apollo.Elements;
using Apollo.Viewers;

namespace Apollo.Components {
    public class DeviceHead: UserControl {
        void InitializeComponent() {
            AvaloniaXamlLoader.Load(this);

            Border = this.Get<Border>("Border");
            Header = this.Get<Border>("Header");
        }

        DeviceViewer Owner;
        public Border Border, Header;

        public DeviceHead() => throw new InvalidOperationException();

        public DeviceHead(Device owner, DeviceViewer ownerviewer) {
            InitializeComponent();

            Owner = ownerviewer;

            this.Resources["TitleBrush"] = Owner.Header.Background?? Owner.Resources["TitleBrush"];

            Owner.DragDrop.Subscribe(this);
            
            SetEnabled(owner.Enabled);
        }

        void Unloaded(object sender, VisualTreeAttachmentEventArgs e) => Owner = null;

        public void SetEnabled(bool value) {
            Border.Background = App.GetResource<IBrush>(value? "ThemeControlHighBrush" : "ThemeControlMidBrush");
            Border.BorderBrush = App.GetResource<IBrush>(value? "ThemeBorderMidBrush" : "ThemeBorderLowBrush");
        }

        void Drag(object sender, PointerPressedEventArgs e) => Owner.Drag(sender, e);
    }
}
