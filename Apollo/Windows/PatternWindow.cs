using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using FontWeight = Avalonia.Media.FontWeight;
using SolidColorBrush = Avalonia.Media.SolidColorBrush;
using Avalonia.Threading;

using Apollo.Components;
using Apollo.Core;
using Apollo.Devices;
using Apollo.Elements;
using Apollo.Enums;
using Apollo.Helpers;
using Apollo.Selection;
using Apollo.Structures;
using Apollo.Rendering;

namespace Apollo.Windows {
    public class PatternWindow: Window, ISelectParentViewer, IDroppable {
        static int? _x = null;
        static int _y;
        static double _w, _h;

        public int? IExpanded {
            get => _pattern.Expanded;
        }

        void InitializeComponent() {
            AvaloniaXamlLoader.Load(this);

            TitleText = this.Get<TextBlock>("Title");
            TitleCenter = this.Get<TextBlock>("TitleCenter");
            
            CenteringLeft = this.Get<StackPanel>("CenteringLeft");
            CenteringRight = this.Get<StackPanel>("CenteringRight");

            UndoButton = this.Get<UndoButton>("UndoButton");
            RedoButton = this.Get<RedoButton>("RedoButton");

            FrameList = this.Get<ScrollViewer>("FrameList");
            Editor = this.Get<LaunchpadGrid>("Editor");
            RootKey = this.Get<LaunchpadGrid>("RootKey");
            Wrap = this.Get<CheckBox>("Wrap");

            Duration = this.Get<Dial>("Duration");
            Gate = this.Get<Dial>("Gate");
            Repeats = this.Get<Dial>("Repeats");

            Pinch = this.Get<PinchDial>("Pinch");

            PlaybackMode = this.Get<ComboBox>("PlaybackMode");
            Infinite = this.Get<CheckBox>("Infinite");

            ImportButton = this.Get<Button>("Import");
            Play = this.Get<Button>("Play");
            Fire = this.Get<Button>("Fire");

            Reverse = this.Get<Button>("Reverse");
            Invert = this.Get<Button>("Invert");

            Contents = this.Get<StackPanel>("Frames").Children;
            PortSelector = this.Get<PortSelector>("PortSelector");

            ColorPicker = this.Get<ColorPicker>("ColorPicker");
            ColorHistory = this.Get<ColorHistory>("ColorHistory");

            BottomLeftPane = this.Get<StackPanel>("BottomLeftPane");
            CollapseButton = this.Get<CollapseButton>("CollapseButton");
        }

        HashSet<IDisposable> observables = new();

        Pattern _pattern;
        Track _track;

        public Pattern Device { get => _pattern; }
        
        Launchpad _launchpad;
        Launchpad Launchpad {
            get => _launchpad;
            set {
                if (_launchpad != null) {
                    _launchpad.PatternWindow = null;
                    _launchpad.Clear();
                }

                _launchpad = value;

                if (_launchpad != null && _launchpad != MIDI.NoOutput) {
                    _launchpad.PatternWindow?.Close();
                    _launchpad.PatternWindow = this;
                    _launchpad.Clear();
                    PlayExit = Heaven.MIDIEnter;

                } else PlayExit = null;

                origin = gesturePoint = -1;

                if (historyShowing) RenderHistory();
                else if (IsArrangeValid)
                    if (Launchpad != null) 
                        PlayExit?.Invoke(_pattern[_pattern.Expanded].Screen.Select((c, i) => 
                            new Signal(this, Launchpad, (byte)i, _pattern[_pattern.Expanded].Screen[i], layer: -1000)
                        ).ToList());
            }
        }

        TextBlock TitleText, TitleCenter;
        StackPanel CenteringLeft, CenteringRight, BottomLeftPane;
        UndoButton UndoButton;
        RedoButton RedoButton;
        ScrollViewer FrameList;
        PortSelector PortSelector;
        ComboBox PlaybackMode;
        LaunchpadGrid Editor, RootKey;
        Controls Contents;
        ColorPicker ColorPicker;
        ColorHistory ColorHistory;
        Dial Duration, Gate, Repeats;
        PinchDial Pinch;
        Button ImportButton, Play, Fire, Reverse, Invert;
        CheckBox Wrap, Infinite;
        CollapseButton CollapseButton;

        int origin = -1;
        int gesturePoint = -1;
        bool gestureUsed = false;
        bool historyShowing = false;

        bool _locked = false;
        public bool Locked {
            get => _locked;
            private set {
                _locked = value;

                if (Repeats.Enabled) Repeats.DisplayDisabledText = false;
                if (Duration.Enabled) Duration.DisplayDisabledText = false;

                UndoButton.IsEnabled =
                RedoButton.IsEnabled =
                ImportButton.IsEnabled =
                Repeats.Enabled =
                Gate.Enabled =
                Pinch.Enabled =
                PlaybackMode.IsEnabled =
                Wrap.IsEnabled =
                Infinite.IsEnabled =
                Play.IsEnabled =
                Fire.IsEnabled =
                PortSelector.IsEnabled =
                ColorPicker.IsEnabled =
                ColorHistory.IsEnabled =
                Duration.Enabled =
                Duration.IsEnabled =
                Reverse.IsEnabled =
                Invert.IsEnabled = !_locked;

                if (!_locked) {
                    Duration.Enabled = !(_pattern.Infinite && _pattern.Expanded == _pattern.Count - 1);
                    SetPlaybackMode(_pattern.Mode);

                    Repeats.DisplayDisabledText = Duration.DisplayDisabledText = true;
                }
            }
        }

        Action<List<Signal>> PlayExit;
        
        void UpdateTitle() => UpdateTitle(_track.ParentIndex.Value, _track.ProcessedName);
        void UpdateTitle(int index) => UpdateTitle(index, _track.ProcessedName);
        void UpdateTitle(string name) => UpdateTitle(_track.ParentIndex.Value, name);
        void UpdateTitle(int index, string name)
            => Title = TitleText.Text = TitleCenter.Text = $"Editing Pattern - {name}";

        void UpdateTopmost(bool value) => Topmost = value;
        
        void SetAlwaysShowing() {
            for (int i = 1; i < Contents.Count; i++)
                ((FrameDisplay)Contents[i]).FrameAdd.AlwaysShowing = false;

            if (Contents.Count > 1) ((FrameDisplay)Contents.Last()).FrameAdd.AlwaysShowing = true;
        }

        public void Contents_Insert(int index, Frame frame, bool ignoreExpanded = false) {
            if (Contents.Count > 1) {
                ((FrameDisplay)Contents[1]).Remove.Opacity = 1;
                ((FrameDisplay)Contents[Contents.Count - 1]).Viewer.Time.Text = _pattern.Frames[Contents.Count - 2].Time.ToString();
            }

            FrameDisplay viewer = new FrameDisplay(frame, _pattern);
            viewer.FrameAdded += Frame_Insert;
            viewer.FrameRemoved += i => Selection.Action("Delete", _pattern, i, i);
            viewer.FrameSelected += Frame_Select;
            frame.Info = viewer;

            Contents.Insert(index + 1, viewer);
            SetAlwaysShowing();

            ((FrameDisplay)Contents[Contents.Count - 1]).Viewer.Time.Text = _pattern.Frames[Contents.Count - 2].ToString();

            if (!ignoreExpanded && index <= _pattern.Expanded) _pattern.Expanded++;
        }

        public void Contents_Remove(int index) {
            if (index < _pattern.Expanded) _pattern.Expanded--;
            else if (index == _pattern.Expanded) Frame_Select(Math.Max(0, _pattern.Expanded - 1));

            _pattern[index].Info = null;
            Contents.RemoveAt(index + 1);

            SetAlwaysShowing();

            if (Contents.Count == 2) ((FrameDisplay)Contents[1]).Remove.Opacity = 0;
        }

        public SelectionManager Selection;

        public PatternWindow() => new InvalidOperationException();

        public PatternWindow(Pattern pattern) {
            InitializeComponent();
            #if DEBUG
                this.AttachDevTools();
            #endif

            UpdateTopmost(Preferences.AlwaysOnTop);
            Preferences.AlwaysOnTopChanged += UpdateTopmost;

            _pattern = pattern;
            _pattern.MIDIEnter(StopSignal.Instance);

            _track = Track.Get(_pattern);

            SetRootKey(_pattern.RootKey);
            Wrap.IsChecked = _pattern.Wrap;

            Repeats.RawValue = _pattern.Repeats;
            Gate.RawValue = _pattern.Gate * 100;

            Pinch.RawValue = _pattern.Pinch;
            Pinch.IsBilateral = _pattern.Bilateral;

            PlaybackMode.SelectedIndex = (int)_pattern.Mode;

            Infinite.IsChecked = _pattern.Infinite;

            CollapseButton.Showing = true;

            DragDrop = new DragDropManager(this);

            for (int i = 0; i < _pattern.Count; i++)
                Contents_Insert(i, _pattern[i], true);
            
            if (_pattern.Count == 1) ((FrameDisplay)Contents[1]).Remove.Opacity = 0;

            Selection = new SelectionManager(() => _pattern.Frames.FirstOrDefault());

            Launchpad = Preferences.CaptureLaunchpad? _track.Launchpad : MIDI.NoOutput;

            PortSelector.Update(Launchpad);

            ColorPicker.SetColor(ColorHistory.GetColor(0)?? new Color());
            ColorHistory.Select(ColorPicker.Color.Clone(), true);
            
            Frame_Select(_pattern.Expanded);
            Selection.Select(_pattern[_pattern.Expanded]);

            observables.Add(this.GetObservable(Visual.BoundsProperty).Subscribe(Bounds_Updated));
            observables.Add(TitleText.GetObservable(Visual.BoundsProperty).Subscribe(Bounds_Updated));
            observables.Add(TitleCenter.GetObservable(Visual.BoundsProperty).Subscribe(Bounds_Updated));
            observables.Add(CenteringLeft.GetObservable(Visual.BoundsProperty).Subscribe(Bounds_Updated));
            observables.Add(CenteringRight.GetObservable(Visual.BoundsProperty).Subscribe(Bounds_Updated));
        }

        void Loaded(object sender, EventArgs e) {
            if (Preferences.RememberPatternPosition && _x.HasValue) {
                Position = new PixelPoint(_x.Value, _y);
                Width = _w;
                Height = _h;
            }

            Position = new PixelPoint(Position.X, Math.Max(0, Position.Y));

            _track.ParentIndexChanged += UpdateTitle;
            _track.NameChanged += UpdateTitle;
            UpdateTitle();

            ColorHistory.HistoryChanged += RenderHistory;
        }

        void Unloaded(object sender, CancelEventArgs e) {
            if (historyShowing) MIDI.ClearState(force: true);

            Locked = false;

            foreach (Frame frame in _pattern.Frames)
                frame.Info = null;

            _pattern.Window = null;
            _pattern = null;

            if (Launchpad != null) {
                Launchpad.PatternWindow = null;
                Launchpad.Clear();
            }

            _launchpad = null;

            Selection.Dispose();
            
            _track.ParentIndexChanged -= UpdateTitle;
            _track.NameChanged -= UpdateTitle;
            _track = null;

            DragDrop.Dispose();
            DragDrop = null;

            Preferences.AlwaysOnTopChanged -= UpdateTopmost;
            ColorHistory.HistoryChanged -= RenderHistory;

            foreach (IDisposable observable in observables)
                observable.Dispose();

            Content = null;

            _x = Position.X;
            _y = Position.Y;
            _w = Width;
            _h = Height;

            App.WindowClosed(this);
        }

        public void Bounds_Updated(Rect bounds) {
            if (Bounds.IsEmpty || TitleText.Bounds.IsEmpty || TitleCenter.Bounds.IsEmpty || CenteringLeft.Bounds.IsEmpty || CenteringRight.Bounds.IsEmpty) return;

            int result = Convert.ToInt32((Bounds.Width - TitleText.Bounds.Width) / 2 <= Math.Max(CenteringLeft.Bounds.Width, CenteringRight.Bounds.Width) + 10);

            TitleText.Opacity = result;
            TitleCenter.Opacity = 1 - result;
        }

        void Port_Changed(Launchpad selected) {
            if (selected != null && Launchpad != selected)
                PortSelector.Update(Launchpad = selected);
        }

        public void Expand(int? index) {
            if (index != null) Frame_Select(index.Value);
        }

        void Frame_Insert(int index) {
            if (Locked) return;

            Frame reference = _pattern[Math.Max(0, index - 1)];

            Frame_Insert(index, new Frame(reference.Time.Clone()));
        }

        void Frame_Insert(int index, Frame frame) {
            if (Locked) return;

            Frame reference = _pattern[Math.Max(0, index - 1)];

            if (Preferences.CopyPreviousFrame)
                for (int i = 0; i < reference.Screen.Length; i++)
                    frame.Screen[i] = reference.Screen[i].Clone();
            
            Program.Project.Undo.AddAndExecute(new Pattern.FrameInsertedUndoEntry(
                _pattern,
                index,
                frame
            ));
        }

        void Frame_InsertStart() => Frame_Insert(0);

        public bool Draw = true;

        public void Frame_Select(int index) {
            if (Locked) return;

            ((FrameDisplay)Contents[_pattern.Expanded + 1]).Viewer.Time.FontWeight = FontWeight.Normal;

            _pattern.Expanded = index;

            ((FrameDisplay)Contents[_pattern.Expanded + 1]).Viewer.Time.FontWeight = FontWeight.Bold;
            
            Duration.UsingSteps = _pattern[_pattern.Expanded].Time.Mode;
            Duration.Length = _pattern[_pattern.Expanded].Time.Length;
            Duration.RawValue = _pattern[_pattern.Expanded].Time.Free;

            Locked = false; // Redraws stuff

            if (Draw) {
                Editor.RenderFrame(_pattern[_pattern.Expanded]);

                if (Launchpad != null)
                    PlayExit?.Invoke(_pattern[_pattern.Expanded].Screen.Select((c, i) => 
                        new Signal(this, Launchpad, (byte)i, _pattern[_pattern.Expanded].Screen[i], layer: -1000)
                    ).ToList());
            }
        }

        void Frame_Action(string action) => Frame_Action(action, false);
        void Frame_Action(string action, bool right) => Selection.Action(action, _pattern, (right? _pattern.Count : 0) - 1);

        void ContextMenu_Action(string action) => Frame_Action(action, true);
        
        void Frame_AfterClick(object sender, PointerReleasedEventArgs e) {
            PointerUpdateKind MouseButton = e.GetCurrentPoint(this).Properties.PointerUpdateKind;

            if (MouseButton == PointerUpdateKind.RightButtonReleased)
                ((ApolloContextMenu)this.Resources["FrameContextMenu"]).Open((Control)sender);

            e.Handled = true;
        }

        async void HandleKey(object sender, KeyEventArgs e) {
            if (App.Dragging) return;

            if (e.Key == Key.Enter || e.Key == Key.Space) {
                KeyModifiers mods = e.KeyModifiers & ~App.ControlKey;
                int start = ((e.KeyModifiers & ~KeyModifiers.Shift) == App.ControlKey)? _pattern.Expanded : 0;

                if (mods == KeyModifiers.Shift) PatternFire(start);
                else if (mods == KeyModifiers.None) PatternPlay(Play, start);
                return;
            }

            if (Locked) return;
            
            if (e.Key == Key.Insert || e.Key == Key.Add || e.Key == Key.OemPlus) Frame_Insert(_pattern.Expanded + 1);
            else if (e.Key == Key.Delete || e.Key == Key.Back || e.Key == Key.Subtract || e.Key == Key.OemMinus) Selection.Action("Delete");

            else {
                if (App.WindowKey(this, e) || await Program.Project.HandleKey(this, e) || Program.Project.Undo.HandleKey(e) || Selection.HandleKey(e)) {
                    this.Focus();
                    return;
                }

                if (e.KeyModifiers != KeyModifiers.None && e.KeyModifiers != KeyModifiers.Shift) return;

                bool shift = e.KeyModifiers == KeyModifiers.Shift;

                if (e.Key == Key.Up || e.Key == Key.Left) {
                    if (Selection.Move(false, shift) || shift) Frame_Select(Selection.Start.IParentIndex.Value);
                    else Frame_Insert(0);

                } else if (e.Key == Key.Down || e.Key == Key.Right) {
                    if (Selection.Move(true, shift) || shift) Frame_Select(Selection.Start.IParentIndex.Value);
                    else Frame_Insert(_pattern.Count);

                } else if (e.Key == Key.Home) {
                    Selection.Select(_pattern[0], shift);
                    Frame_Select(0);

                } else if (e.Key == Key.End) {
                    Selection.Select(_pattern[_pattern.Count - 1], shift);
                    Frame_Select(_pattern.Count - 1);

                } else if (e.Key == Key.PageUp) {
                    int target = Math.Max(0, Selection.Start.IParentIndex.Value - (int)(FrameList.Bounds.Height / Contents[1].Bounds.Height));
                    Selection.Select(_pattern[target], shift);
                    Frame_Select(target);
                    
                } else if (e.Key == Key.PageDown) {
                    int target = Math.Min(_pattern.Count - 1, Selection.Start.IParentIndex.Value + (int)(FrameList.Bounds.Height / Contents[1].Bounds.Height));
                    Selection.Select(_pattern[target], shift);
                    Frame_Select(target);
                }
            }
        }

        void Window_KeyDown(object sender, KeyEventArgs e) {
            List<Window> windows = App.Windows.ToList();
            HandleKey(sender, e);
            
            if (windows.SequenceEqual(App.Windows) && FocusManager.Instance.Current?.GetType() != typeof(TextBox))
                this.Focus();
        }

        void ColorPicker_Changed(Color color, Color old) => ColorHistory.Select(color.Clone());

        void ColorHistory_Changed(Color color) {
            ColorPicker.SetColor(color.Clone());
            RenderHistory();
        }

        void RenderHistory() {
            if (!historyShowing) return;
            ColorHistory.Render(Launchpad);
        }

        Color drawingState;
        Color[] oldScreen;
        
        void PadStarted(int index) {
            drawingState = (_pattern[_pattern.Expanded].Screen[LaunchpadGrid.GridToSignal(index)] == ColorPicker.Color)
                ? new Color(0)
                : ColorPicker.Color;
            
            oldScreen = _pattern[_pattern.Expanded].Screen.Select(i => i.Clone()).ToArray();
        }
    
        void PadPressed(int index, KeyModifiers mods = KeyModifiers.None) {
            if (Locked) return;

            int signalIndex = LaunchpadGrid.GridToSignal(index);

            if (mods.HasFlag(App.ControlKey)) {
                Color color = _pattern[_pattern.Expanded].Screen[signalIndex];
                ColorPicker.SetColor(color.Clone());
                ColorHistory.Select(color.Clone(), true);
                return;
            }

            if (_pattern[_pattern.Expanded].Screen[signalIndex] != ColorPicker.Color) Dispatcher.UIThread.InvokeAsync(() => {
                ColorHistory.Use();
            });

            Editor.SetColor(index, (SolidColorBrush)(_pattern[_pattern.Expanded].Screen[signalIndex] = drawingState.Clone()).ToScreenBrush());
            
            ((FrameDisplay)Contents[_pattern.Expanded + 1]).Viewer.Draw();

            if (Launchpad != null) 
                PlayExit?.Invoke(new List<Signal>() {new Signal(this, Launchpad, (byte)signalIndex, _pattern[_pattern.Expanded].Screen[signalIndex], layer: -1000)});
        }

        void PadFinished(int _) {
            if (oldScreen == null) return;

            if (!oldScreen.SequenceEqual(_pattern[_pattern.Expanded].Screen))
                Program.Project.Undo.Add(new Pattern.FrameChangedUndoEntry(
                    _pattern,
                    _pattern.Expanded,
                    oldScreen
                ));
            
            oldScreen = null;
        }

        public void SetGrid(int index, Frame frame) {
            if (_pattern.Expanded == index) {
                Editor.RenderFrame(frame);

                if (Launchpad != null)
                    PlayExit?.Invoke(frame.Screen.Select((c, i) => 
                        new Signal(this, Launchpad, (byte)i, frame.Screen[i], layer: -1000)
                    ).ToList());
            }
        }

        void HandleGesture(int x, int y) {
            if (x == -1 && y == 0) { // Left
                if (_pattern.Expanded == 0) Frame_Insert(0);
                else Frame_Select(_pattern.Expanded - 1);

            } else if (x == 1 && y == 0) { // Right
                if (_pattern.Expanded == _pattern.Count - 1) Frame_Insert(_pattern.Count);
                else Frame_Select(_pattern.Expanded + 1);

            } else if (x == 0 && y == 1) { // Up
                if (Locked) return;
                origin = -1;
                historyShowing = Locked = true;
                RenderHistory();
                
            } else if (x == 0 && y == -1) { // Down
                if (Locked) return;
                
                int index = Launchpad.Type.IsGenerationX()? 9 : -1;
                PadStarted(index);
                PadPressed(index);
                PadFinished(index);
                
            } else if (x == -1 && y == 1) // Up-Left
                PatternPlay(Play);
                
            else if (x == 1 && y == -1) // Down-Right
                PatternFire();
                
            else if (x == 1 && y == 1) // Up-Right
                Frame_Insert(_pattern.Expanded + 1);
                
            else if (x == -1 && y == -1) // Down-Left
                Selection.Action("Delete", _pattern, _pattern.Expanded, _pattern.Expanded);
        }

        public void MIDIEnter(Signal n) {
            if (n.Source != Launchpad || n.Index == 100) return;

            if (!Preferences.EnableGestures) {
                if (n.Color.Lit) Dispatcher.UIThread.InvokeAsync(() => {
                    int index = LaunchpadGrid.SignalToGrid(n.Index);
                    PadStarted(index);
                    PadPressed(index);
                    PadFinished(index);
                });

                return;
            }

            if (n.Color.Lit) {
                if (historyShowing) {
                    int x = n.Index % 10;
                    int y = n.Index / 10;

                    if (x < 1 || 8 < x || y < 1 || 8 < y) return;
                    
                    int i = 64 - y * 8 + x - 1;

                    if (i < ColorHistory.AdjustedCount) Dispatcher.UIThread.InvokeAsync(() => {
                        ColorHistory.Input(i);

                        historyShowing = Locked = false;
                        MIDI.ClearState(force: true);
                        Frame_Select(_pattern.Expanded);
                    });

                } else if (origin == -1) {
                    origin = n.Index;
                    gestureUsed = false;

                } else if (gesturePoint == -1)
                    gesturePoint = n.Index;

            } else {
                if (historyShowing) return;

                if (n.Index == origin) {
                    if (!gestureUsed && !Locked) Dispatcher.UIThread.InvokeAsync(() => {
                        int index = LaunchpadGrid.SignalToGrid(n.Index);
                        PadStarted(index);
                        PadPressed(index);
                        PadFinished(index);
                    });

                    origin = gesturePoint;
                    gesturePoint = -1;

                } else if (n.Index == gesturePoint) {
                    int x = gesturePoint % 10 - origin % 10;
                    int y = gesturePoint / 10 - origin / 10;

                    Dispatcher.UIThread.InvokeAsync(() => { HandleGesture(x, y); });

                    gestureUsed = true;
                    gesturePoint = -1;
                }
            }
        }

        void Infinite_Changed(object sender, RoutedEventArgs e) {
            bool value = Infinite.IsChecked.Value;

            if (_pattern.Infinite != value)
                Program.Project.Undo.AddAndExecute(new Pattern.InfiniteUndoEntry(
                    _pattern,
                    _pattern.Infinite,
                    value
                ));
        }

        public void SetInfinite(bool value) {
            Infinite.IsChecked = value;

            ((FrameDisplay)Contents.Last()).Viewer.Time.Text = _pattern.Frames.Last().ToString();

            Duration.Enabled = !(value && _pattern.Expanded == _pattern.Count - 1);
            Repeats.Enabled = !(_pattern.Infinite || _pattern.Mode == PlaybackType.Loop);
        }

        List<Time> oldTime;

        void Duration_Started() {
            oldTime = new List<Time>();

            foreach (Frame frame in Selection.Selection)
                oldTime.Add(frame.Time.Clone());
        }

        void Duration_Changed(Dial sender, double value, double? old) {
            if (oldTime == null) return;

            if (old != null && !oldTime.SequenceEqual(Selection.Selection.Select(i => ((Frame)i).Time.With(false, free: (int)value)))) {
                Program.Project.Undo.AddAndExecute(new Pattern.DurationValueUndoEntry(
                    _pattern,
                    Selection.Selection[0].IParentIndex.Value,
                    oldTime,
                    (int)value
                ));

                oldTime = null;
            }
        }

        public void SetDurationValue(int index, int value) {
            if (_pattern.Expanded == index)
                Duration.RawValue = value;
        }

        void Duration_StepChanged(int value, int? old) {
            if (oldTime == null) return;

            if (old != null && !oldTime.SequenceEqual((Selection.Selection.Select(i => ((Frame)i).Time)))) {
                Program.Project.Undo.AddAndExecute(new Pattern.DurationStepUndoEntry(
                    _pattern,
                    Selection.Selection[0].IParentIndex.Value,
                    oldTime,
                    (int)value
                ));

                oldTime = null;
            }
        }

        public void SetDurationStep(int index, Length value) {
            if (_pattern.Expanded == index)
                Duration.Length = value;
        }

        void Duration_ModeChanged(bool value, bool? old) {
            if (oldTime == null) return;

            if (old != null && !oldTime.SequenceEqual(Selection.Selection.Select(i => ((Frame)i).Time.With(value)))) {
                Program.Project.Undo.AddAndExecute(new Pattern.DurationModeUndoEntry(
                    _pattern,
                    Selection.Selection[0].IParentIndex.Value,
                    oldTime,
                    value
                ));

                oldTime = null;
            }
        }

        public void SetDurationMode(int index, bool value) {
            if (_pattern.Expanded == index)
                Duration.UsingSteps = value;
        }

        void Frame_Reverse(object sender, RoutedEventArgs e) {
            List<ISelect> selection = Selection.Selection;

            int left = selection.First().IParentIndex.Value;
            int right = selection.Last().IParentIndex.Value;

            if (left == right) return;

            Program.Project.Undo.AddAndExecute(new Pattern.FrameReversedUndoEntry(
                _pattern,
                _pattern.Expanded,
                left,
                right
            ));
        }

        void Frame_Invert(object sender, RoutedEventArgs e) {
            List<ISelect> selection = Selection.Selection;
            
            Program.Project.Undo.AddAndExecute(new Pattern.FrameInvertedUndoEntry(
                _pattern,
                selection.First().IParentIndex.Value,
                selection.Last().IParentIndex.Value
            ));
        }

        void PlaybackMode_Changed(object sender, SelectionChangedEventArgs e) {
            PlaybackType selected = (PlaybackType)PlaybackMode.SelectedIndex;

            if (_pattern.Mode != selected)
                Program.Project.Undo.AddAndExecute(new Pattern.PlaybackModeUndoEntry(
                    _pattern,
                    _pattern.Mode,
                    selected
                ));
        }

        public void SetPlaybackMode(PlaybackType mode) {
            PlaybackMode.SelectedIndex = (int)mode;

            Repeats.Enabled = !(_pattern.Infinite || _pattern.Mode == PlaybackType.Loop);
            Repeats.DisabledText = (_pattern.Mode == PlaybackType.Loop)? "Infinite" : "1";
        }

        void Gate_Changed(Dial sender, double value, double? old) {
            if (old != null && old != value)
                Program.Project.Undo.AddAndExecute(new Pattern.GateUndoEntry(
                    _pattern,
                    old.Value,
                    value
                ));
        }

        public void SetGate(double gate) => Gate.RawValue = gate * 100;

        void Repeats_Changed(Dial sender, double value, double? old) {
            if (old != null && old != value)
                Program.Project.Undo.AddAndExecute(new Pattern.RepeatsUndoEntry(
                    _pattern,
                    (int)old.Value,
                    (int)value
                ));
        }

        public void SetRepeats(int repeats) => Repeats.RawValue = repeats;

        void Pinch_Changed(Dial sender, double value, double? old) {
            if (old != null && old != value)
                Program.Project.Undo.AddAndExecute(new Pattern.PinchUndoEntry(
                    _pattern,
                    old.Value,
                    value
                ));

            _pattern.Pinch = value;
        }

        public void SetPinch(double pinch) => Pinch.RawValue = pinch;

        void Bilateral_Changed(bool value, bool? old) {
            if (old != null && old != value)
                Program.Project.Undo.AddAndExecute(new Pattern.BilateralUndoEntry(
                    _pattern,
                    _pattern.Bilateral,
                    value
                ));
        }

        public void SetBilateral(bool value) => Pinch.IsBilateral = value;

        int? oldRootKey = -1;

        void RootKeyStarted(int index) {
            if (Locked) return;

            oldRootKey = _pattern.RootKey;
        }

        void RootKeyPressed(int index) {
            if (Locked) return;

            int signalIndex = LaunchpadGrid.GridToSignal(index);

            _pattern.RootKey = (signalIndex == _pattern.RootKey)? null : (int?)signalIndex;
        }

        void RootKeyFinished(int _) {
            if (Locked) return;

            if (oldRootKey == -1) return;

            if (oldRootKey != _pattern.RootKey) 
                Program.Project.Undo.Add(new Pattern.RootKeyUndoEntry(
                    _pattern,
                    oldRootKey,
                    _pattern.RootKey
                ));

            oldRootKey = -1;
        }

        public void SetRootKey(int? index) {
            RootKey.Clear();

            if (index != null) RootKey.SetColor(
                LaunchpadGrid.SignalToGrid(index.Value),
                App.GetResource<SolidColorBrush>("ThemeAccentBrush")
            );
        }

        void Wrap_Changed(object sender, RoutedEventArgs e) {
            bool value = Wrap.IsChecked.Value;

            if (_pattern.Wrap != value) 
                Program.Project.Undo.AddAndExecute(new Pattern.WrapUndoEntry(
                    _pattern,
                    _pattern.Wrap,
                    value
                ));
        }

        public void SetWrap(bool value) => Wrap.IsChecked = value;

        void PlayColor(int index, Color color) {
            Dispatcher.UIThread.InvokeAsync(() => {
                Editor.SetColor(LaunchpadGrid.SignalToGrid(index), color.ToScreenBrush());
            });

            PlayExit?.Invoke(new List<Signal>() {new Signal(this, Launchpad, (byte)index, color.Clone(), layer: -1000)});
        }

        int PlayIndex = 0;
        object PlayLocker;

        void PatternStop(bool send = true, int start = 0) {
            if (send && PlayIndex < _pattern.Count * _pattern.AdjustedRepeats)
                for (int i = 0; i < _pattern[PlayIndex % _pattern.Count].Screen.Length; i++)
                    if (_pattern[PlayIndex % _pattern.Count].Screen[i].Lit)
                        PlayColor(i, new Color(0));

            PlayLocker = null;
            PlayIndex = start;
        }

        void PatternPlay(Button sender, int start = 0) {
            if (Locked) {
                PatternStop();
                _pattern.MIDIEnter(StopSignal.Instance);

                PatternFinish();
                return;
            }

            Locked = true;

            Button button = (Button)sender;

            Dispatcher.UIThread.InvokeAsync(() => {
                button.IsEnabled = true;
                button.Content = "Stop";
            });
            
            PatternStop(false, start);
            _pattern.MIDIEnter(StopSignal.Instance);

            MIDI.ClearState(false);

            Editor.RenderFrame(_pattern[start]);
            
            PlayExit?.Invoke(_pattern[start].Screen.Select((c, i) => new Signal(this, Launchpad, (byte)i, c.Clone(), layer: -1000)).ToList());

            double time = Enumerable.Sum(_pattern.Frames.Take(start).Select(i => (double)i.Time)) * _pattern.Gate;
            double starttime = _pattern.ApplyPinch(time);

            object locker = PlayLocker = new object();
            
            for (int i = start; i < _pattern.Count * _pattern.AdjustedRepeats; i++) {
                time += _pattern[i % _pattern.Count].Time * _pattern.Gate;
                double pinched = _pattern.ApplyPinch(time);

                Heaven.Schedule(() => {
                    if (ReferenceEquals(locker, PlayLocker)) 
                        RenderFrame();
                }, Heaven.Time + pinched - starttime);
            }
        }
        
        void RenderFrame() {
            if (_pattern?.Disposed != false) return;
            
            if (++PlayIndex < _pattern.Count * _pattern.AdjustedRepeats) {
                for (int i = 0; i < _pattern[PlayIndex % _pattern.Count].Screen.Length; i++)
                    if (_pattern[PlayIndex % _pattern.Count].Screen[i] != _pattern[(PlayIndex - 1) % _pattern.Count].Screen[i])
                        PlayColor(i, _pattern[PlayIndex % _pattern.Count].Screen[i]);

            } else {
                for (int i = 0; i < _pattern.Frames.Last().Screen.Length; i++)
                    if (_pattern.Frames.Last().Screen[i].Lit)
                        PlayColor(i, new Color(0));

                PatternFinish();
            }
        }

        void PatternFire(int start = 0) {
            PlayExit = n => {
                n.ForEach(s => {
                    s.Layer = 0;
                    s.Source = _track.Launchpad;
                });
                _pattern.MIDIExit(n);
            };
            PatternPlay(Fire, start);
        }

        void PatternFinish() {
            Dispatcher.UIThread.InvokeAsync(() => {
                Locked = false;

                Editor.RenderFrame(_pattern[_pattern.Expanded]);

                Play.Content = "Play";
                Fire.Content = "Fire";
            });

            if (_launchpad != null) {
                PlayExit = Heaven.MIDIEnter;
                
                PlayExit(_pattern[_pattern.Expanded].Screen.Select((c, i) => 
                    new Signal(this, Launchpad, (byte)i, c, layer: -1000)
                ).ToList());
            }
            else PlayExit = null;
        }

        void PlayButton(object sender, RoutedEventArgs e) => PatternPlay(Play);

        void FireButton(object sender, RoutedEventArgs e) => PatternFire();

        public void PlayFrom(FrameDisplay sender, bool fire = false) {
            int start = Contents.IndexOf(sender) - 1;

            if (fire) PatternFire(start);
            else PatternPlay(Play, start);
        }

        public void RecreateFrames() {
            while (Contents.Count > 1) Contents.RemoveAt(1);
            _pattern.Expanded = 0;

            for (int i = 0; i < _pattern.Count; i++)
                Contents_Insert(i, _pattern[i], true);

            if (_pattern.Count == 1) ((FrameDisplay)_pattern.Window?.Contents[1]).Remove.Opacity = 0;

            Frame_Select(0);
            Selection.Select(_pattern[0]);
        }

        async void ImportFile(string filepath) {
            if (!Importer.FramesFromMIDI(filepath, out List<Frame> frames) && !Importer.FramesFromImage(filepath, out frames)) {
                await MessageWindow.CreateReadError(this);
                return;
            }

            Program.Project.Undo.AddAndExecute(new Pattern.ImportUndoEntry(
                _pattern,
                Path.GetFileNameWithoutExtension(filepath),
                new Pattern(frames: frames.Select(i => i.Clone()).ToList())
            ));
        }

        async void ImportDialog(object sender, RoutedEventArgs e) {
            if (Locked) return;

            OpenFileDialog ofd = new OpenFileDialog() {
                AllowMultiple = false,
                Filters = new List<FileDialogFilter>() {
                    new FileDialogFilter() {
                        Extensions = new List<string>() {
                            "mid",
                            "gif",
                            "jpg",
                            "jpeg",
                            "png",
                            "bmp",
                        },
                        Name = "All Supported Files"
                    },
                    new FileDialogFilter() {
                        Extensions = new List<string>() {
                            "mid"
                        },
                        Name = "MIDI Files"
                    },
                    new FileDialogFilter() {
                        Extensions = new List<string>() {
                            "gif"
                        },
                        Name = "Animated GIF Images"
                    },
                    new FileDialogFilter() {
                        Extensions = new List<string>() {
                            "jpg",
                            "jpeg",
                            "png",
                            "bmp"
                        },
                        Name = "Static JPG, PNG or BMP images"
                    }
                },
                Title = "Import Pattern"
            };

            string[] result = await ofd.ShowAsync(this);
            if (result.Length > 0) ImportFile(result[0]);
        }

        DragDropManager DragDrop;

        public List<string> DropAreas => new List<string>() {"DropZoneAfter", "FrameAdd"};

        public Dictionary<string, DragDropManager.DropHandler> DropHandlers => new Dictionary<string, DragDropManager.DropHandler>() {
            {"Frame", null}
        };

        public ISelect Item => null;
        public ISelectParent ItemParent => _pattern;

        void BottomCollapse() => BottomLeftPane.Opacity = Convert.ToInt32(BottomLeftPane.IsVisible = CollapseButton.Showing = (BottomLeftPane.MaxHeight = (BottomLeftPane.MaxHeight == 0)? 1000 : 0) != 0);

        void Window_Focus(object sender, PointerPressedEventArgs e) => this.Focus();

        void MoveWindow(object sender, PointerPressedEventArgs e) {
            if (e.ClickCount == 2) Maximize(null);
            else BeginMoveDrag(e);

            Topmost = false;
            Topmost = Preferences.AlwaysOnTop;
            Activate();
        }
        
        void Minimize() => WindowState = WindowState.Minimized;

        void Maximize(PointerEventArgs e) {
            WindowState = (WindowState == WindowState.Maximized)? WindowState.Normal : WindowState.Maximized;

            Topmost = false;
            Topmost = Preferences.AlwaysOnTop;
            Activate();
        }

        void ResizeNorthWest(object sender, PointerPressedEventArgs e) => BeginResizeDrag(WindowEdge.NorthWest, e);
        void ResizeNorth(object sender, PointerPressedEventArgs e) => BeginResizeDrag(WindowEdge.North, e);
        void ResizeNorthEast(object sender, PointerPressedEventArgs e) => BeginResizeDrag(WindowEdge.NorthEast, e);
        void ResizeWest(object sender, PointerPressedEventArgs e) => BeginResizeDrag(WindowEdge.West, e);
        void ResizeEast(object sender, PointerPressedEventArgs e) => BeginResizeDrag(WindowEdge.East, e);
        void ResizeSouthWest(object sender, PointerPressedEventArgs e) => BeginResizeDrag(WindowEdge.SouthWest, e);
        void ResizeSouth(object sender, PointerPressedEventArgs e) => BeginResizeDrag(WindowEdge.South, e);
        void ResizeSouthEast(object sender, PointerPressedEventArgs e) => BeginResizeDrag(WindowEdge.SouthEast, e);

        public static void Create(Pattern pattern, Window owner) {
            if (pattern.Window == null) {
                pattern.Window = new PatternWindow(pattern);
                
                if (owner == null || owner.WindowState == WindowState.Minimized) 
                    pattern.Window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                else
                    pattern.Window.Owner = owner;

                pattern.Window.Show();
                pattern.Window.Owner = null;
                
            } else {
                pattern.Window.WindowState = WindowState.Normal;
                pattern.Window.Activate();
            }

            pattern.Window.Topmost = true;
            pattern.Window.Topmost = Preferences.AlwaysOnTop;
        }
    }
}