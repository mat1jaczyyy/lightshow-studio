using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

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
    public class ChainInfo: UserControl, ISelectViewer, IDraggable, IRenamable {
        void InitializeComponent() {
            AvaloniaXamlLoader.Load(this);

            Root = this.Get<Grid>("DropZone");
            ChainAdd = this.Get<VerticalAdd>("DropZoneAfter");
            NameText = this.Get<TextBlock>("Name");
            Draggable = this.Get<Grid>("Draggable");
            MuteItem = this.Get<MenuItem>("MuteItem");
            Input = this.Get<TextBox>("Input");
            Indicator = this.Get<Indicator>("Indicator");
        }

        public delegate void ChainAddedEventHandler(int index);
        public event ChainAddedEventHandler ChainAdded;

        public delegate void ChainExpandedEventHandler(int? index);
        public event ChainExpandedEventHandler ChainExpanded;

        Chain _chain;
        public bool Selected { get; private set; } = false;

        Grid Root;
        public TextBlock NameText { get; private set; }
        public VerticalAdd ChainAdd;
        public Indicator Indicator { get; private set; }

        Grid Draggable;
        MenuItem MuteItem;
        public TextBox Input { get; private set; }
        
        void ApplyHeaderBrush(IBrush brush) {
            if (IsArrangeValid) Root.Background = brush;
            else this.Resources["BackgroundBrush"] = brush;
        }

        public void Select() {
            ApplyHeaderBrush(App.GetResource<IBrush>("ThemeAccentBrush2"));
            Selected = true;
        }

        public void Deselect() {
            ApplyHeaderBrush(new SolidColorBrush(Color.Parse("Transparent")));
            Selected = false;
        }

        public ChainInfo() => new InvalidOperationException();

        public ChainInfo(Chain chain) {
            InitializeComponent();
            
            _chain = chain;

            Deselect();

            Rename = new RenameManager(this);

            Rename.UpdateText();
            _chain.ParentIndexChanged += Rename.UpdateText;

            DragDrop = new DragDropManager(this);

            SetEnabled();
        }

        void Unloaded(object sender, VisualTreeAttachmentEventArgs e) {
            ChainAdded = null;
            ChainExpanded = null;

            _chain.ParentIndexChanged -= Rename.UpdateText;
            _chain.Info = null;
            _chain = null;

            Rename.Dispose();
            Rename = null;

            DragDrop.Dispose();
            DragDrop = null;
        }

        public void SetEnabled() => NameText.Foreground = App.GetResource<IBrush>(_chain.Enabled? "ThemeForegroundBrush" : "ThemeForegroundLowBrush");

        void Chain_Action(string action) => Track.Get(_chain)?.Window?.Selection.Action(action, (ISelectParent)_chain.Parent, _chain.ParentIndex.Value);
    
        void ContextMenu_Action(string action) => Track.Get(_chain)?.Window?.Selection.Action(action);

        public void Select(PointerPressedEventArgs e) {
            PointerUpdateKind MouseButton = e.GetCurrentPoint(this).Properties.PointerUpdateKind;

            if (MouseButton == PointerUpdateKind.LeftButtonPressed || (MouseButton == PointerUpdateKind.RightButtonPressed && !Selected))
                Track.Get(_chain)?.Window?.Selection.Select(_chain, e.KeyModifiers.HasFlag(KeyModifiers.Shift));
        }

        public static bool DeviceAsChainDrop(IControl source, ISelectParent parent, ISelect child, int after, string format, DragEventArgs e) {
            List<Device> moving = ((List<ISelect>)e.Data.Get(format)).Cast<Device>().ToList();

            Chain source_chain = moving[0].Parent;
            Chain target_chain = (Chain)child;

            int before = moving[0].IParentIndex.Value - 1;
            bool copy = e.KeyModifiers.HasFlag(App.ControlKey);

            int? remove = null;

            Group parent_group = (Group)parent;

            if (target_chain == null) {
                parent_group.Insert((remove = (source.Name != "DropZoneAfter")? 0 : parent_group.Count).Value, target_chain = new Chain());

            } else {
                if (source.Name != "DropZoneAfter") {
                    if (parent_group.Expanded != target_chain.IParentIndex)
                        parent_group.SpecificViewer.Expand(target_chain.IParentIndex);
                
                } else {
                    parent_group.Insert((remove = target_chain.IParentIndex + 1).Value);
                    target_chain = parent_group[target_chain.IParentIndex.Value + 1];
                }
            }

            bool result;

            if (result = DragDropManager.Move(moving.Cast<ISelect>().ToList(), (ISelectParent)target_chain, after = ((ISelectParent)target_chain).Count - 1, copy, out Path<ISelectParent> premove)) {
                int before_pos = before;
                int after_pos = moving[0].IParentIndex.Value - 1;
                int count = moving.Count;

                if (source_chain == target_chain && after < before)
                    before_pos += count;
                
                Program.Project.Undo.Add(new DeviceAsChainUndoEntry(source_chain, premove, target_chain, remove, copy, count, before, after, before_pos, after_pos, format));

            } else if (remove != null) parent_group.Remove(remove.Value);

            return result;
        }

        public class DeviceAsChainUndoEntry: DragDropManager.DragDropUndoEntry {
            int? remove;

            protected override ISelectParent ResolvePremove() {
                ISelectParent ret;

                if (remove != null) premove.Resolve(1).IInsert(remove.Value, (Chain)(ret = new Chain()));
                else ret = premove.Resolve();

                return ret;
            }

            protected override void UndoPath(params ISelectParent[] items) {
                base.UndoPath(items);
                
                if (remove != null)
                    ((ISelect)items[1]).IParent.Remove(remove.Value);
            }

            public DeviceAsChainUndoEntry(ISelectParent sourceparent, Path<ISelectParent> premove, ISelectParent targetparent, int? remove, bool copy, int count, int before, int after, int before_pos, int after_pos, string format)
            : base(sourceparent, premove, targetparent, copy, count, before, after, before_pos, after_pos, format) => this.remove = remove;
        
            DeviceAsChainUndoEntry(BinaryReader reader, int version)
            : base(reader, version) => remove = reader.ReadBoolean()? (int?)reader.ReadInt32() : null;
            
            public override void Encode(BinaryWriter writer) {
                base.Encode(writer);
                
                writer.Write(remove.HasValue);
                if (remove.HasValue)
                    writer.Write(remove.Value);
            }
        }

        DragDropManager DragDrop;

        public string DragFormat => "Chain";
        public List<string> DropAreas => new List<string>() {"DropZone", "DropZoneAfter"};

        public Dictionary<string, DragDropManager.DropHandler> DropHandlers => new Dictionary<string, DragDropManager.DropHandler>() {
            {DataFormats.FileNames, null},
            {DragFormat, null},
            {"Device", DeviceAsChainDrop}
        };

        public ISelect Item => _chain;
        public ISelectParent ItemParent => Item.IParent;

        public void DragFailed(PointerPressedEventArgs e) {
            PointerUpdateKind MouseButton = e.GetCurrentPoint(this).Properties.PointerUpdateKind;
                
            if (MouseButton == PointerUpdateKind.LeftButtonPressed)
                ChainExpanded?.Invoke(_chain.ParentIndex.Value);
            
            if (MouseButton == PointerUpdateKind.RightButtonPressed) {
                MuteItem.Header = ((Chain)Track.Get(_chain)?.Window?.Selection.Selection.First()).Enabled? "Mute" : "Unmute";
                ((ApolloContextMenu)this.Resources["ChainContextMenu"]).Open(Draggable);
            }
        }

        public void Drag(object sender, PointerPressedEventArgs e) => DragDrop.Drag(Track.Get(_chain)?.Window?.Selection, e);
        
        void Chain_Add() => ChainAdded?.Invoke(_chain.ParentIndex.Value + 1);

        public RenameManager Rename { get; private set; }
    }
}
