using System;
using System.Collections.Generic;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using AvaloniaColor = Avalonia.Media.Color;
using IBrush = Avalonia.Media.IBrush;
using SolidColorBrush = Avalonia.Media.SolidColorBrush;

using Apollo.Core;
using Apollo.Devices;
using Apollo.Selection;
using Apollo.Structures;

namespace Apollo.Components {
    public class FrameDisplay: UserControl, ISelectViewer, IDraggable {
        void InitializeComponent() {
            AvaloniaXamlLoader.Load(this);

            Root = this.Get<StackPanel>("DropZone");

            Viewer = this.Get<FrameThumbnail>("Draggable");
            
            Remove = this.Get<Remove>("Remove");
            FrameAdd = this.Get<VerticalAdd>("DropZoneAfter");
        }

        public delegate void FrameEventHandler(int index);
        public event FrameEventHandler FrameAdded;
        public event FrameEventHandler FrameRemoved;
        public event FrameEventHandler FrameSelected;
        
        Pattern _pattern;
        public bool Selected { get; private set; } = false;
        public FrameThumbnail Viewer;

        StackPanel Root;
        public Remove Remove;
        public VerticalAdd FrameAdd;

        void ApplyHeaderBrush(IBrush brush) {
            if (IsArrangeValid) Root.Background = brush;
            else this.Resources["BackgroundBrush"] = brush;
        }

        public void Select() {
            ApplyHeaderBrush(App.GetResource<IBrush>("ThemeAccentBrush2"));
            Selected = true;
        }

        public void Deselect() {
            ApplyHeaderBrush(new SolidColorBrush(AvaloniaColor.Parse("Transparent")));
            Selected = false;
        }

        public FrameDisplay() => throw new InvalidOperationException();

        public FrameDisplay(Frame frame, Pattern pattern) {
            InitializeComponent();

            _pattern = pattern;

            Viewer.Frame = frame;

            DragDrop = new DragDropManager(this);
        }

        void Unloaded(object sender, VisualTreeAttachmentEventArgs e) {
            FrameAdded = null;
            FrameRemoved = null;
            FrameSelected = null;
            Viewer = null;

            DragDrop.Dispose();
            DragDrop = null;
        }

        void Frame_Action(string action) => _pattern.Window?.Selection.Action(action, _pattern, Viewer.Frame.ParentIndex.Value);

        void ContextMenu_Action(string action) {
            if (action == "Play Here") _pattern.Window?.PlayFrom(this);
            else if (action == "Fire Here") _pattern.Window?.PlayFrom(this, true);
            else _pattern.Window?.Selection.Action(action);
        }

        public void Select(PointerPressedEventArgs e) {
            PointerUpdateKind MouseButton = e.GetCurrentPoint(this).Properties.PointerUpdateKind;

            if (MouseButton == PointerUpdateKind.LeftButtonPressed || (MouseButton == PointerUpdateKind.RightButtonPressed && !Selected))
                _pattern.Window?.Selection.Select(Viewer.Frame, e.KeyModifiers.HasFlag(KeyModifiers.Shift));
        }
        
        DragDropManager DragDrop;

        public string DragFormat => "Frame";
        public List<string> DropAreas => new List<string>() {"DropZone", "DropZoneAfter"};

        public Dictionary<string, DragDropManager.DropHandler> DropHandlers => new Dictionary<string, DragDropManager.DropHandler>() {
            {DragFormat, null},
        };

        public ISelect Item => Viewer.Frame;
        public ISelectParent ItemParent => _pattern;

        public void DragFailed(PointerPressedEventArgs e) {
            PointerUpdateKind MouseButton = e.GetCurrentPoint(this).Properties.PointerUpdateKind;

            if (MouseButton == PointerUpdateKind.LeftButtonPressed)
                FrameSelected?.Invoke(Viewer.Frame.ParentIndex.Value);
    
            if (MouseButton == PointerUpdateKind.RightButtonPressed)
                ((ApolloContextMenu)this.Resources["FrameContextMenu"]).Open(Viewer);

            if (MouseButton == PointerUpdateKind.MiddleButtonPressed)
                _pattern.Window?.PlayFrom(this, e.KeyModifiers == KeyModifiers.Shift);
        }

        public void Drag(object sender, PointerPressedEventArgs e) {
            PointerUpdateKind MouseButton = e.GetCurrentPoint(this).Properties.PointerUpdateKind;

            if ((e.KeyModifiers & ~KeyModifiers.Shift) == KeyModifiers.Alt) {
                _pattern.Window?.PlayFrom(this, (e.KeyModifiers & ~KeyModifiers.Alt) == KeyModifiers.Shift);
                return;
            }
            
            DragDrop.Drag(_pattern.Window?.Selection, e);
        }
        
        void Frame_Add() => FrameAdded?.Invoke(Viewer.Frame.ParentIndex.Value + 1);
        void Frame_Remove() => FrameRemoved?.Invoke(Viewer.Frame.ParentIndex.Value);
    }
}
