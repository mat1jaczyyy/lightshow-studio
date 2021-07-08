using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Threading;

using Apollo.Core;
using Apollo.Undo;
using Apollo.Viewers;

namespace Apollo.Windows {
    public class UndoWindow: Window {
        void InitializeComponent() {
            AvaloniaXamlLoader.Load(this);
            
            ScrollViewer = this.Get<ScrollViewer>("ScrollViewer");
            Contents = this.Get<StackPanel>("Contents");
        }

        void UpdateTopmost(bool value) => Topmost = value;

        ScrollViewer ScrollViewer;
        StackPanel Contents;

        int? saved = null;
        int? current = null;

        public void Contents_Insert(int index, UndoEntry entry) {
            UndoEntryInfo viewer = new UndoEntryInfo(entry);
            viewer.Selected += UndoEntry_Select;

            Contents.Children.Insert(index, viewer);

            if (index <= current) current++;
            if (index <= saved) saved++;
            
            Dispatcher.UIThread.Post(() => ScrollViewer.Offset = ScrollViewer.Offset.WithY(Contents.Bounds.Height), DispatcherPriority.Background);
        }

        public void Contents_Remove(int index) {
            Contents.Children.RemoveAt(index);

            if (index < current) current--;
            else if (index == current) current = null;

            if (index < saved) saved--;
            else if (index == saved) saved = null;
        }

        public UndoWindow() {
            InitializeComponent();
            #if DEBUG
                this.AttachDevTools();
            #endif
            
            UpdateTopmost(Preferences.AlwaysOnTop);
            Preferences.AlwaysOnTopChanged += UpdateTopmost;

            for (int i = 0; i < Program.Project.Undo.History.Count; i++)
                Contents_Insert(i, Program.Project.Undo.History[i]);
            
            Program.Project.Undo.SavedPositionChanged += HighlightSaved;

            HighlightPosition(Program.Project.Undo.Position);
        }

        void Loaded(object sender, EventArgs e) => Position = new PixelPoint(Position.X, Math.Max(0, Position.Y));

        void Unloaded(object sender, CancelEventArgs e) {
            Program.Project.Undo.Window = null;

            Preferences.AlwaysOnTopChanged -= UpdateTopmost;

            Program.Project.Undo.SavedPositionChanged -= HighlightSaved;

            this.Content = null;
        }

        void UndoEntry_Select(int index) => Program.Project.Undo.Select(index);

        public void HighlightSaved(int? index) {
            if (saved.HasValue && saved != current)
                ((UndoEntryInfo)(Contents.Children[saved.Value])).Background = SolidColorBrush.Parse("Transparent");

            if ((saved = index).HasValue && index != current)
                ((UndoEntryInfo)(Contents.Children[saved.Value])).Background = App.GetResource<SolidColorBrush>("ThemeControlVeryHighBrush");
        }

        public void HighlightPosition(int index) {
            if (current.HasValue)
                ((UndoEntryInfo)(Contents.Children[current.Value])).Background = SolidColorBrush.Parse("Transparent");
            
            ((UndoEntryInfo)(Contents.Children[(current = index).Value])).Background = App.GetResource<SolidColorBrush>("ThemeAccentBrush2");
        
            if (Program.Project.Undo.SavedPosition.HasValue)
                HighlightSaved(Program.Project.Undo.SavedPosition.Value);
        }

        async void HandleKey(object sender, KeyEventArgs e) {
            if (App.Dragging) return;

            if (App.WindowKey(this, e) || await Program.Project.HandleKey(this, e) || Program.Project.Undo.HandleKey(e)) {
                this.Focus();
                return;
            }

            if (e.Key == Key.Up) Program.Project.Undo.Undo();
            else if (e.Key == Key.Down) Program.Project.Undo.Redo();

            this.Focus();
        }

        void Window_KeyDown(object sender, KeyEventArgs e) {
            List<Window> windows = App.Windows.ToList();
            HandleKey(sender, e);
            
            if (windows.SequenceEqual(App.Windows) && FocusManager.Instance.Current?.GetType() != typeof(TextBox))
                this.Focus();
        }

        void Window_Focus(object sender, PointerPressedEventArgs e) => this.Focus();

        void MoveWindow(object sender, PointerPressedEventArgs e) => BeginMoveDrag(e);
        
        void Minimize() => WindowState = WindowState.Minimized;

        void ResizeNorth(object sender, PointerPressedEventArgs e) => BeginResizeDrag(WindowEdge.North, e);

        void ResizeSouth(object sender, PointerPressedEventArgs e) => BeginResizeDrag(WindowEdge.South, e);

        public static void Create(Window owner) {
            if (Program.Project.Undo.Window == null) {
                Program.Project.Undo.Window = new UndoWindow();
                
                if (owner == null || owner.WindowState == WindowState.Minimized) 
                    Program.Project.Undo.Window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                else
                    Program.Project.Undo.Window.Owner = owner;

                Program.Project.Undo.Window.Show();
                Program.Project.Undo.Window.Owner = null;

            } else {
                Program.Project.Undo.Window.WindowState = WindowState.Normal;
                Program.Project.Undo.Window.Activate();
            }
            
            Program.Project.Undo.Window.Topmost = true;
            Program.Project.Undo.Window.Topmost = Preferences.AlwaysOnTop;
        }
    }
}