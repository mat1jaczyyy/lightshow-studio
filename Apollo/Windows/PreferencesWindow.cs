using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;

using Humanizer;
using Humanizer.Localisation;

using Apollo.Components;
using Apollo.Core;
using Apollo.Devices;
using Apollo.Elements;
using Apollo.Enums;
using Apollo.Helpers;
using Apollo.RtMidi;
using Apollo.Structures;
using Apollo.Viewers;

namespace Apollo.Windows {
    public class PreferencesWindow: Window {
        void InitializeComponent() {
            AvaloniaXamlLoader.Load(this);

            AlwaysOnTop = this.Get<CheckBox>("AlwaysOnTop");
            CenterTrackContents = this.Get<CheckBox>("CenterTrackContents");

            ChainSignalIndicators = this.Get<CheckBox>("ChainSignalIndicators");
            DeviceSignalIndicators = this.Get<CheckBox>("DeviceSignalIndicators");
            
            ColorDisplayFormat = this.Get<ComboBox>("ColorDisplayFormat");

            LaunchpadStyle = this.Get<ComboBox>("LaunchpadStyle");
            LaunchpadGridRotation = this.Get<ComboBox>("LaunchpadGridRotation");
            LaunchpadModel = this.Get<ComboBox>("LaunchpadModel");

            AutoCreateKeyFilter = this.Get<CheckBox>("AutoCreateKeyFilter");
            AutoCreateMacroFilter = this.Get<CheckBox>("AutoCreateMacroFilter");
            AutoCreatePattern = this.Get<CheckBox>("AutoCreatePattern");

            FPSLimit = this.Get<HorizontalDial>("FPSLimit");
            FPSLimit.ErrorText = $"Use values above {FPSLimit.ErrorValue} FPS with caution, as they usually\n" +
                                 "bring little noticable improvement while severely limiting\n" +
                                 "performance (especially on non-CFW Launchpads).";

            CopyPreviousFrame = this.Get<CheckBox>("CopyPreviousFrame");
            CaptureLaunchpad = this.Get<CheckBox>("CaptureLaunchpad");
            EnableGestures = this.Get<CheckBox>("EnableGestures");
            RememberPatternPosition = this.Get<CheckBox>("RememberPatternPosition");

            Monochrome = this.Get<RadioButton>("Monochrome");
            NovationPalette = this.Get<RadioButton>("NovationPalette");
            CustomPalette = this.Get<RadioButton>("CustomPalette");

            ThemeHeader = this.Get<TextBlock>("ThemeHeader");
            Dark = this.Get<RadioButton>("Dark");
            Light = this.Get<RadioButton>("Light");

            Backup = this.Get<CheckBox>("Backup");
            Autosave = this.Get<CheckBox>("Autosave");

            UndoLimit = this.Get<CheckBox>("UndoLimit");

            DiscordPresence = this.Get<CheckBox>("DiscordPresence");
            DiscordFilename = this.Get<CheckBox>("DiscordFilename");

            CheckForUpdates = this.Get<CheckBox>("CheckForUpdates");

            Contents = this.Get<StackPanel>("Contents").Children;

            CurrentSession = this.Get<TextBlock>("CurrentSession");
            AllTime = this.Get<TextBlock>("AllTime");

            Preview = this.Get<LaunchpadGrid>("Preview");
        }

        CheckBox AlwaysOnTop, CenterTrackContents, ChainSignalIndicators, DeviceSignalIndicators, AutoCreateKeyFilter, AutoCreateMacroFilter, AutoCreatePattern, CopyPreviousFrame, CaptureLaunchpad, EnableGestures, RememberPatternPosition, Backup, Autosave, UndoLimit, DiscordPresence, DiscordFilename, CheckForUpdates;
        ComboBox ColorDisplayFormat, LaunchpadStyle, LaunchpadGridRotation, LaunchpadModel;
        TextBlock ThemeHeader, CurrentSession, AllTime;
        RadioButton Monochrome, NovationPalette, CustomPalette, Dark, Light;
        HorizontalDial FPSLimit;
        Controls Contents;
        DispatcherTimer Timer;
        LaunchpadGrid Preview;

        Fade fade = new Fade(
            new Time(false),
            colors: new List<Color>() {
                new Color(1, 0, 0),
                new Color(63, 0, 0),
                new Color(63, 63, 0),
                new Color(0, 63, 0),
                new Color(0, 63, 63),
                new Color(0, 0, 63),
                new Color(63, 0, 63),
                new Color(63, 0, 0),
                new Color(1, 0, 0)
            },
            positions: new List<double>() {
                0, 0.125, 0.25, 0.375, 0.5, 0.625, 0.75, 0.875, 1
            },
            types: new List<FadeType>{
                FadeType.Linear,
                FadeType.Linear,
                FadeType.Linear,
                FadeType.Linear,
                FadeType.Linear,
                FadeType.Linear,
                FadeType.Linear,
                FadeType.Linear,
                FadeType.Linear
            }
        );

        void UpdateTopmost(bool value) => AlwaysOnTop.IsChecked = Topmost = value;

        void UpdatePorts() {
            for (int i = Contents.Count - 2; i >= 0; i--) Contents.RemoveAt(i);

            foreach (Launchpad lp in MIDI.UsableDevices)
                Contents.Insert(Contents.Count - 1, new LaunchpadInfo(lp));
        }

        void HandlePorts() => Dispatcher.UIThread.InvokeAsync((Action)UpdatePorts);

        void UpdateTime(object sender, EventArgs e) {
            CurrentSession.Text = $"Current session: {Program.TimeSpent.Elapsed.Humanize(minUnit: TimeUnit.Second, maxUnit: TimeUnit.Hour)}";

            if (Preferences.Time >= (long)TimeSpan.MaxValue.TotalSeconds) Preferences.BaseTime = 0;

            AllTime.Text = $"All time: {Preferences.Time.Seconds().Humanize(minUnit: TimeUnit.Second, maxUnit: TimeUnit.Hour)}";
        }

        public PreferencesWindow() {
            InitializeComponent();
            #if DEBUG
                this.AttachDevTools();
            #endif
            
            UpdateTopmost(Preferences.AlwaysOnTop);
            Preferences.AlwaysOnTopChanged += UpdateTopmost;

            Preferences.Window = this;

            TextBlock Version = this.Get<TextBlock>("Version");
            Version.Text += Program.Version;

            if (Github.AvaloniaVersion() != "")
                ToolTip.SetTip(Version, $"Avalonia {Github.AvaloniaVersion()}");

            ToolTip.SetTip(this.Get<TextBlock>("LaunchpadHeader"), $"RtMidi APIs:\n{string.Join("- \n", MidiDeviceManager.Default.GetAvailableMidiApis())}");

            fade.MIDIExit = FadeExit;
            //fade.Initialize();

            AlwaysOnTop.IsChecked = Preferences.AlwaysOnTop;
            CenterTrackContents.IsChecked = Preferences.CenterTrackContents;

            ChainSignalIndicators.IsChecked = Preferences.ChainSignalIndicators;
            DeviceSignalIndicators.IsChecked = Preferences.DeviceSignalIndicators;
            
            ColorDisplayFormat.SelectedIndex = (int)Preferences.ColorDisplayFormat;

            LaunchpadStyle.SelectedIndex = (int)Preferences.LaunchpadStyle;
            LaunchpadGridRotation.SelectedIndex = Convert.ToInt32(Preferences.LaunchpadGridRotation);
            LaunchpadModel.SelectedIndex = (int)Preferences.LaunchpadModel;

            AutoCreateKeyFilter.IsChecked = Preferences.AutoCreateKeyFilter;
            AutoCreateMacroFilter.IsChecked = Preferences.AutoCreateMacroFilter;
            AutoCreatePattern.IsChecked = Preferences.AutoCreatePattern;

            FPSLimit.RawValue = Preferences.FPSLimit;

            CopyPreviousFrame.IsChecked = Preferences.CopyPreviousFrame;
            CaptureLaunchpad.IsChecked = Preferences.CaptureLaunchpad;
            EnableGestures.IsChecked = Preferences.EnableGestures;
            RememberPatternPosition.IsChecked = Preferences.RememberPatternPosition;

            Monochrome.IsChecked = Preferences.ImportPalette == Palettes.Monochrome;
            NovationPalette.IsChecked = Preferences.ImportPalette == Palettes.NovationPalette;
            CustomPalette.Content = $"Custom Retina Palette - {Preferences.PaletteName}";
            CustomPalette.IsChecked = Preferences.ImportPalette == Palettes.CustomPalette;

            Dark.IsChecked = Preferences.Theme == ThemeType.Dark;
            Light.IsChecked = Preferences.Theme == ThemeType.Light;

            Backup.IsChecked = Preferences.Backup;
            Autosave.IsChecked = Preferences.Autosave;

            UndoLimit.IsChecked = Preferences.UndoLimit;

            DiscordPresence.IsChecked = Preferences.DiscordPresence;
            DiscordFilename.IsChecked = Preferences.DiscordFilename;

            CheckForUpdates.IsChecked = Preferences.CheckForUpdates;

#if !PRERELEASE
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) {
                CheckForUpdates.IsChecked = false;
#endif
                CheckForUpdates.IsEnabled = false;
                
#if PRERELEASE
                ToolTip.SetTip((Control)CheckForUpdates.Parent, "Auto-updating is not supported on prerelease");
#else
                ToolTip.SetTip((Control)CheckForUpdates.Parent, "Auto-updating is not supported on Linux");
            }
#endif

            UpdateTime(null, EventArgs.Empty);
            Timer = new DispatcherTimer() {
                Interval = new TimeSpan(0, 0, 1)
            };
            Timer.Tick += UpdateTime;
            Timer.Start();

            UpdatePorts();
            MIDI.DevicesUpdated += HandlePorts;
        }

        void Loaded(object sender, EventArgs e) => Position = new PixelPoint(Position.X, Math.Max(0, Position.Y));

        void Unloaded(object sender, CancelEventArgs e) {
            Preferences.Window = null;

            Timer.Stop();
            Timer.Tick -= UpdateTime;

            MIDI.DevicesUpdated -= HandlePorts;

            Preferences.AlwaysOnTopChanged -= UpdateTopmost;

            this.Content = null;
            
            Preview = null;
            fade.Dispose();
        }

        void AlwaysOnTop_Changed(object sender, RoutedEventArgs e) {
            Preferences.AlwaysOnTop = AlwaysOnTop.IsChecked.Value;
            Activate();
        }

        void CenterTrackContents_Changed(object sender, RoutedEventArgs e) => Preferences.CenterTrackContents = CenterTrackContents.IsChecked.Value;

        void ChainSignalIndicators_Changed(object sender, RoutedEventArgs e) => Preferences.ChainSignalIndicators = ChainSignalIndicators.IsChecked.Value;

        void DeviceSignalIndicators_Changed(object sender, RoutedEventArgs e) => Preferences.DeviceSignalIndicators = DeviceSignalIndicators.IsChecked.Value;

        void ColorDisplayFormat_Changed(object sender, SelectionChangedEventArgs e) => Preferences.ColorDisplayFormat = (ColorDisplayType)ColorDisplayFormat.SelectedIndex;
        
        void LaunchpadStyle_Changed(object sender, SelectionChangedEventArgs e) => Preferences.LaunchpadStyle = (LaunchpadStyles)LaunchpadStyle.SelectedIndex;

        void LaunchpadGridRotation_Changed(object sender, SelectionChangedEventArgs e) => Preferences.LaunchpadGridRotation = LaunchpadGridRotation.SelectedIndex > 0;

        void LaunchpadModel_Changed(object sender, SelectionChangedEventArgs e) => Preferences.LaunchpadModel = (LaunchpadModels)LaunchpadModel.SelectedIndex;

        void AutoCreateKeyFilter_Changed(object sender, RoutedEventArgs e) => Preferences.AutoCreateKeyFilter = AutoCreateKeyFilter.IsChecked.Value;

        void AutoCreateMacroFilter_Changed(object sender, RoutedEventArgs e) => Preferences.AutoCreateMacroFilter = AutoCreateMacroFilter.IsChecked.Value;

        void AutoCreatePattern_Changed(object sender, RoutedEventArgs e) => Preferences.AutoCreatePattern = AutoCreatePattern.IsChecked.Value;

        void FPSLimit_Changed(Dial sender, double value, double? old) => Preferences.FPSLimit = (int)value;

        void CaptureLaunchpad_Changed(object sender, RoutedEventArgs e) => Preferences.CaptureLaunchpad = CaptureLaunchpad.IsChecked.Value;

        void CopyPreviousFrame_Changed(object sender, RoutedEventArgs e) => Preferences.CopyPreviousFrame = CopyPreviousFrame.IsChecked.Value;

        void EnableGestures_Changed(object sender, RoutedEventArgs e) => Preferences.EnableGestures = EnableGestures.IsChecked.Value;

        void RememberPatternPosition_Changed(object sender, RoutedEventArgs e) => Preferences.RememberPatternPosition = RememberPatternPosition.IsChecked.Value;

        void ClearColorHistory(object sender, RoutedEventArgs e) => ColorHistory.Clear();

        void Monochrome_Changed(object sender, RoutedEventArgs e) => Preferences.ImportPalette = Palettes.Monochrome;

        void NovationPalette_Changed(object sender, RoutedEventArgs e) => Preferences.ImportPalette = Palettes.NovationPalette;

        void CustomPalette_Changed(object sender, RoutedEventArgs e) => Preferences.ImportPalette = Palettes.CustomPalette;

        async void BrowseCustomPalette(object sender, RoutedEventArgs e) {
            OpenFileDialog ofd = new OpenFileDialog() {
                AllowMultiple = false,
                Filters = new List<FileDialogFilter>() {
                    new FileDialogFilter() {
                        Extensions = new List<string>() {
                            "*"
                        },
                        Name = "Retina Palette File"
                    }
                },
                Title = "Select Retina Palette"
            };

            string[] result = await ofd.ShowAsync(this);

            if (result.Length > 0) {
                Palette loaded;

                using (FileStream file = File.Open(result[0], FileMode.Open, FileAccess.Read))
                    loaded = Palette.Decode(file);
                
                if (loaded != null) {
                    Preferences.CustomPalette = loaded;
                    CustomPalette.Content = $"Custom Retina Palette - {Preferences.PaletteName = Path.GetFileNameWithoutExtension(result[0])}";
                    CustomPalette.IsChecked = true;
                    Preferences.ImportPalette = Palettes.CustomPalette;
                }
            }
        }

        void SetTheme(ThemeType theme) {
            if (Preferences.Theme != theme)
                ThemeHeader.Text = "You must restart\nApollo Studio to\napply this change.";

            Preferences.Theme = theme;
        }

        void Dark_Changed(object sender, RoutedEventArgs e) => SetTheme(ThemeType.Dark);

        void Light_Changed(object sender, RoutedEventArgs e) => SetTheme(ThemeType.Light);

        void Backup_Changed(object sender, RoutedEventArgs e) => Preferences.Backup = Backup.IsChecked.Value;

        void Autosave_Changed(object sender, RoutedEventArgs e) => Preferences.Autosave = Autosave.IsChecked.Value;

        void ClearRecentProjects(object sender, RoutedEventArgs e) => Preferences.RecentsClear();

        void UndoLimit_Changed(object sender, RoutedEventArgs e) => Preferences.UndoLimit = UndoLimit.IsChecked.Value;

        void DiscordPresence_Changed(object sender, RoutedEventArgs e) => Preferences.DiscordPresence = DiscordPresence.IsChecked.Value;

        void DiscordFilename_Changed(object sender, RoutedEventArgs e) => Preferences.DiscordFilename = DiscordFilename.IsChecked.Value;

        void CheckForUpdates_Changed(object sender, RoutedEventArgs e) => Preferences.CheckForUpdates = CheckForUpdates.IsChecked.Value;

        void OpenCrashesFolder(object sender, RoutedEventArgs e) {
            if (!Directory.Exists(Program.UserPath)) Directory.CreateDirectory(Program.UserPath);
            if (!Directory.Exists(Program.CrashDir)) Directory.CreateDirectory(Program.CrashDir);

            App.URL(Program.CrashDir);
        }

        void LocateApolloConnector(object sender, RoutedEventArgs e) {
            string m4l = Program.GetBaseFolder("M4L");

            if (!Directory.Exists(m4l)) Directory.CreateDirectory(m4l);

            App.URL(m4l);
        }

        void Launchpad_Add() {
            LaunchpadWindow.Create(MIDI.ConnectVirtual(), this);
            MIDI.Update();
        }

        void Preview_Pressed(int index) =>
            fade.MIDIEnter(new Signal(null, null, (byte)LaunchpadGrid.GridToSignal(index), new Color()));

        void FadeExit(List<Signal> n) => Dispatcher.UIThread.InvokeAsync(() => {
            foreach (Signal s in n)
                Preview?.SetColor(LaunchpadGrid.SignalToGrid(s.Index), s.Color.ToScreenBrush());
        });

        async void HandleKey(object sender, KeyEventArgs e) {
            if (App.Dragging) return;

            if (!App.WindowKey(this, e) && Program.Project != null && !await Program.Project.HandleKey(this, e))
                Program.Project?.Undo.HandleKey(e);
        }

        void Window_KeyDown(object sender, KeyEventArgs e) {
            List<Window> windows = App.Windows.ToList();
            HandleKey(sender, e);
            
            if (windows.SequenceEqual(App.Windows) && FocusManager.Instance.Current?.GetType() != typeof(TextBox))
                this.Focus();
        }

        void Window_LostFocus(object sender, RoutedEventArgs e) {
            if (FocusManager.Instance.Current?.GetType() == typeof(ComboBox))
                this.Focus();
        }

        void Window_Focus(object sender, PointerPressedEventArgs e) => this.Focus();

        void MoveWindow(object sender, PointerPressedEventArgs e) => BeginMoveDrag(e);
        
        void Minimize() => WindowState = WindowState.Minimized;

        public static void Create(Window owner) {
            if (Preferences.Window == null) {
                Preferences.Window = new PreferencesWindow();
                
                if (owner == null || owner.WindowState == WindowState.Minimized)
                    Preferences.Window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                else
                    Preferences.Window.Owner = owner;

                Preferences.Window.Show();
                Preferences.Window.Owner = null;

            } else {
                Preferences.Window.WindowState = WindowState.Normal;
                Preferences.Window.Activate();
            }

            Preferences.Window.Topmost = true;
            Preferences.Window.Topmost = Preferences.AlwaysOnTop;
        }
    }
}