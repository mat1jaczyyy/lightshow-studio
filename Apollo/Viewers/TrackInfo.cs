using System;
using System.Collections.Generic;
using System.Linq;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.VisualTree;

using Apollo.Components;
using Apollo.Core;
using Apollo.Elements;
using Apollo.Selection;
using Apollo.Windows;

namespace Apollo.Viewers {
    public class TrackInfo: UserControl, ISelectViewer, IDraggable, IRenamable {
        void InitializeComponent() {
            AvaloniaXamlLoader.Load(this);

            NameText = this.Get<TextBlock>("Name");
            Draggable = this.Get<Grid>("Draggable");

            PortSelector = this.Get<PortSelector>("PortSelector");
            DropZone = this.Get<Border>("DropZone");
            TrackAdd = this.Get<TrackAdd>("DropZoneAfter");
            MuteItem = this.Get<MenuItem>("MuteItem");
            Input = this.Get<TextBox>("Input");
        }

        public delegate void AddedEventHandler(int index);
        public event AddedEventHandler Added;
        
        Track _track;
        public bool Selected { get; private set; } = false;

        public TextBlock NameText { get; private set; }
        public PortSelector PortSelector { get; private set; }
        public TrackAdd TrackAdd;

        Grid Draggable;
        Border DropZone;
        MenuItem MuteItem;
        public TextBox Input { get; private set; }
        
        void UpdateText(int index) => Rename.UpdateText();
        
        void ApplyHeaderBrush(string resource) {
            IBrush brush = App.GetResource<IBrush>(resource);

            if (IsArrangeValid) DropZone.Background = brush;
            else this.Resources["BackgroundBrush"] = brush;
        }

        public void Select() {
            ApplyHeaderBrush("ThemeAccentBrush2");
            Selected = true;
        }

        public void Deselect() {
            ApplyHeaderBrush("ThemeControlHighBrush");
            Selected = false;
        }

        public TrackInfo() => new InvalidOperationException();

        public TrackInfo(Track track) {
            InitializeComponent();
            
            _track = track;
            
            Deselect();

            Rename = new RenameManager(this);

            Rename.UpdateText();
            _track.ParentIndexChanged += UpdateText;

            PortSelector.Update(_track.Launchpad);

            DragDrop = new DragDropManager(this);

            SetEnabled();
        }

        void Unloaded(object sender, VisualTreeAttachmentEventArgs e) {
            Added = null;

            _track.ParentIndexChanged -= UpdateText;
            _track.Info = null;
            _track = null;

            Rename.Dispose();
            Rename = null;

            DragDrop.Dispose();
            DragDrop = null;
        }

        public virtual void SetEnabled() => NameText.Foreground = PortSelector.Foreground = App.GetResource<IBrush>(_track.Enabled? "ThemeForegroundBrush" : "ThemeForegroundLowBrush");
        
        void Track_Action(string action) => Program.Project.Window?.Selection.Action(action, Program.Project, _track.ParentIndex.Value);

        void ContextMenu_Action(string action) => Program.Project.Window?.Selection.Action(action);

        public void Select(PointerPressedEventArgs e) {
            PointerUpdateKind MouseButton = e.GetCurrentPoint(this).Properties.PointerUpdateKind;

            if (MouseButton == PointerUpdateKind.LeftButtonPressed || (MouseButton == PointerUpdateKind.RightButtonPressed && !Selected))
                Program.Project.Window?.Selection.Select(_track, e.KeyModifiers.HasFlag(KeyModifiers.Shift));
        }

        DragDropManager DragDrop;

        public string DragFormat => "Track";
        public List<string> DropAreas => new List<string>() {"DropZone", "DropZoneAfter"};

        public Dictionary<string, DragDropManager.DropHandler> DropHandlers => new Dictionary<string, DragDropManager.DropHandler>() {
            {DataFormats.FileNames, null},
            {DragFormat, null},
        };

        public ISelect Item => _track;
        public ISelectParent ItemParent => Program.Project;

        public void DragFailed(PointerPressedEventArgs e) {
            PointerUpdateKind MouseButton = e.GetCurrentPoint(this).Properties.PointerUpdateKind;
                
            if (MouseButton == PointerUpdateKind.LeftButtonPressed && e.ClickCount == 2) 
                TrackWindow.Create(_track, (Window)this.GetVisualRoot());
            
            if (MouseButton == PointerUpdateKind.RightButtonPressed) {
                MuteItem.Header = ((Track)Program.Project.Window?.Selection.Selection.First()).Enabled? "Mute" : "Unmute";
                ((ApolloContextMenu)this.Resources["TrackContextMenu"]).Open(Draggable);
            }
        }

        public void Drag(object sender, PointerPressedEventArgs e) => DragDrop.Drag(Program.Project.Window?.Selection, e);

        void Track_Add() => Added?.Invoke(_track.ParentIndex.Value + 1);

        void Port_Changed(Launchpad selected) {
            if (selected != null && _track.Launchpad != selected)
                _track.Launchpad = selected;
        }

        public RenameManager Rename { get; private set; }
    }
}
