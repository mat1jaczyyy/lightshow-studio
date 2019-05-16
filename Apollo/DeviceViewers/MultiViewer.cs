﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.VisualTree;

using Apollo.Binary;
using Apollo.Components;
using Apollo.Core;
using Apollo.Devices;
using Apollo.Elements;
using Apollo.Helpers;
using Apollo.Viewers;

namespace Apollo.DeviceViewers {
    public class MultiViewer: UserControl, IMultipleChainParentViewer, ISelectParentViewer {
        public static readonly string DeviceIdentifier = "multi";

        public int? IExpanded {
            get => _multi.Expanded;
        }

        private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
        
        Multi _multi;
        DeviceViewer _parent;
        Controls _root;

        ContextMenu ChainContextMenu;

        Controls Contents;
        ComboBox MultiMode;
        VerticalAdd ChainAdd;

        private void SetAlwaysShowing() {
            ChainAdd.AlwaysShowing = (Contents.Count == 1);

            for (int i = 1; i < Contents.Count; i++)
                ((ChainInfo)Contents[i]).ChainAdd.AlwaysShowing = false;

            if (Contents.Count > 1) ((ChainInfo)Contents.Last()).ChainAdd.AlwaysShowing = true;
        }

        public void Contents_Insert(int index, Chain chain) {
            ChainInfo viewer = new ChainInfo(chain);
            viewer.ChainAdded += Chain_Insert;
            viewer.ChainRemoved += Chain_Remove;
            viewer.ChainExpanded += Expand;
            chain.Info = viewer;

            Contents.Insert(index + 1, viewer);
            SetAlwaysShowing();

            if (IsArrangeValid && _multi.Expanded != null && index <= _multi.Expanded) _multi.Expanded++;
        }

        public void Contents_Remove(int index) {
            if (IsArrangeValid && _multi.Expanded != null) {
                if (index < _multi.Expanded) _multi.Expanded--;
                else if (index == _multi.Expanded) Expand(null);
            }

            _multi[index].Info = null;
            Contents.RemoveAt(index + 1);
            SetAlwaysShowing();
        }

        public MultiViewer(Multi multi, DeviceViewer parent) {
            InitializeComponent();

            _multi = multi;
            _multi.Preprocess.ClearParentIndexChanged();

            _parent = parent;
            _parent.Border.CornerRadius = new CornerRadius(0, 5, 5, 0);
            _parent.Header.CornerRadius = new CornerRadius(0, 5, 0, 0);

            _root = _parent.Root.Children;
            _root.Insert(0, new DeviceHead(parent));
            _root.Insert(1, new ChainViewer(_multi.Preprocess, true));

            MultiMode = this.Get<ComboBox>("MultiMode");
            MultiMode.SelectedItem = _multi.Mode;

            ChainContextMenu = (ContextMenu)this.Resources["ChainContextMenu"];
            ChainContextMenu.AddHandler(MenuItem.ClickEvent, new EventHandler(ChainContextMenu_Click));

            this.AddHandler(DragDrop.DropEvent, Drop);
            this.AddHandler(DragDrop.DragOverEvent, DragOver);

            Contents = this.Get<StackPanel>("Contents").Children;
            
            ChainAdd = this.Get<VerticalAdd>("ChainAdd");
            
            for (int i = 0; i < _multi.Count; i++) {
                _multi[i].ClearParentIndexChanged();
                Contents_Insert(i, _multi[i]);
            }

            if (_multi.Expanded != null) Expand_Insert(multi.Expanded.Value);
        }

        private void Expand_Insert(int index) {
            _root.Insert(3, new ChainViewer(_multi[index], true));
            _root.Insert(4, new DeviceTail(_parent));

            _parent.Border.CornerRadius = new CornerRadius(0);
            _parent.Header.CornerRadius = new CornerRadius(0);
            ((ChainInfo)Contents[index + 1]).Get<TextBlock>("Name").FontWeight = FontWeight.Bold;
        }

        private void Expand_Remove() {
            _root.RemoveAt(4);
            _root.RemoveAt(3);

            _parent.Border.CornerRadius = new CornerRadius(0, 5, 5, 0);
            _parent.Header.CornerRadius = new CornerRadius(0, 5, 0, 0);
            ((ChainInfo)Contents[_multi.Expanded.Value + 1]).Get<TextBlock>("Name").FontWeight = FontWeight.Normal;
        }

        public void Expand(int? index) {
            if (_multi.Expanded != null) {
                Expand_Remove();

                if (index == _multi.Expanded) {
                    _multi.Expanded = null;
                    return;
                }
            }

            if (index != null) Expand_Insert(index.Value);
            
            _multi.Expanded = index;
        }

        private void Chain_Insert(int index) => Chain_Insert(index, new Chain());
        private void Chain_InsertStart() => Chain_Insert(0);

        private void Chain_Insert(int index, Chain chain) {
            Chain r = chain.Clone();
            List<int> path = Track.GetPath(_multi);

            Program.Project.Undo.Add($"Chain Added", () => {
                ((Multi)Track.TraversePath(path)).Remove(index);
            }, () => {
                ((Multi)Track.TraversePath(path)).Insert(index, r.Clone());
            });

            _multi.Insert(index, chain);
        }

        private void Chain_Remove(int index) {
            Chain u = _multi[index].Clone();
            List<int> path = Track.GetPath(_multi);

            Program.Project.Undo.Add($"Chain Deleted", () => {
                ((Multi)Track.TraversePath(path)).Insert(index, u.Clone());
            }, () => {
                ((Multi)Track.TraversePath(path)).Remove(index);
            });

            _multi.Remove(index);
        }

        private void Chain_Action(string action) => Chain_Action(action, false);
        private void Chain_Action(string action, bool right) => Track.Get(_multi).Window?.Selection.Action(action, _multi, (right? _multi.Count : 0) - 1);

        private void ChainContextMenu_Click(object sender, EventArgs e) {
            ((Window)this.GetVisualRoot()).Focus();
            IInteractive item = ((RoutedEventArgs)e).Source;

            if (item.GetType() == typeof(MenuItem))
                Chain_Action((string)((MenuItem)item).Header, true);
        }

        private void Click(object sender, PointerReleasedEventArgs e) {
            if (e.MouseButton == MouseButton.Right)
                ChainContextMenu.Open((Control)sender);

            e.Handled = true;
        }

        private void DragOver(object sender, DragEventArgs e) {
            e.Handled = true;
            if (!e.Data.Contains("chain") && !e.Data.Contains("device")) e.DragEffects = DragDropEffects.None;
        }

        private void Drop(object sender, DragEventArgs e) {
            e.Handled = true;
            
            IControl source = (IControl)e.Source;
            while (source.Name != "DropZoneAfter" && source.Name != "ChainAdd")
                source = source.Parent;

            bool copy = e.Modifiers.HasFlag(InputModifiers.Control);
            bool result;

            if (e.Data.Contains("chain")) {
                List<Chain> moving = ((List<ISelect>)e.Data.Get("chain")).Select(i => (Chain)i).ToList();

                if (source.Name != "DropZoneAfter" || _multi.Chains.Count == 0) result = Chain.Move(moving, _multi, copy);
                else result = Chain.Move(moving, _multi.Chains.Last(), copy);
            
            } else if (e.Data.Contains("device")) {
                List<Device> moving = ((List<ISelect>)e.Data.Get("device")).Select(i => (Device)i).ToList();

                if (source.Name != "DropZoneAfter") {
                    Chain_Insert(0);
                    result = Device.Move(moving, _multi[0], copy);
                } else {
                    Chain_Insert(_multi.Count);
                    result = Device.Move(moving, _multi.Chains.Last(), copy);
                } 

            } else return;

            if (!result) e.DragEffects = DragDropEffects.None;
        }

        private void Mode_Changed(object sender, SelectionChangedEventArgs e) => _multi.Mode = (string)MultiMode.SelectedItem;

        public async void Copy(int left, int right, bool cut = false) {
            Copyable copy = new Copyable();
            
            for (int i = left; i <= right; i++)
                copy.Contents.Add(_multi[i]);

            string b64 = Convert.ToBase64String(Encoder.Encode(copy).ToArray());

            if (cut) Delete(left, right);
            
            await Application.Current.Clipboard.SetTextAsync(b64);
        }

        public async void Paste(int right) {
            string b64 = await Application.Current.Clipboard.GetTextAsync();
            
            Copyable paste = Decoder.Decode(new MemoryStream(Convert.FromBase64String(b64)), typeof(Copyable));
            
            for (int i = 0; i < paste.Contents.Count; i++)
                Chain_Insert(right + i + 1, (Chain)paste.Contents[i]);
        }

        public void Duplicate(int left, int right) {
            for (int i = 0; i <= right - left; i++)
                Chain_Insert(right + i + 1, _multi[left + i].Clone());
        }

        public void Delete(int left, int right) {
            for (int i = right; i >= left; i--)
                Chain_Remove(i);
        }

        public void Group(int left, int right) => throw new InvalidOperationException("A Chain cannot be grouped.");

        public void Ungroup(int index) => throw new InvalidOperationException("A Chain cannot be ungrouped.");

        public void Rename(int left, int right) => ((ChainInfo)Contents[left + 1]).StartInput(left, right);
    }
}
