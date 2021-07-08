using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Markup.Xaml;

using Apollo.Elements;
using Apollo.Enums;
using Apollo.Helpers;
using Apollo.Structures;
using Apollo.Themes;
using Apollo.Windows;

namespace Apollo.Core {
    public class App: Application {
        static App instance;

        public static Window MainWindow => ((ClassicDesktopStyleApplicationLifetime)instance.ApplicationLifetime).MainWindow;
        public static IReadOnlyList<Window> Windows => ((ClassicDesktopStyleApplicationLifetime)instance.ApplicationLifetime).Windows;
        public static void Shutdown() => ((ClassicDesktopStyleApplicationLifetime)instance.ApplicationLifetime).Shutdown();
        
        public static readonly KeyModifiers ControlKey = RuntimeInformation.IsOSPlatform(OSPlatform.OSX)? KeyModifiers.Meta : KeyModifiers.Control;

        public static bool Dragging = false;

        public static bool WindowKey(Window sender, KeyEventArgs e) {
            if (e.KeyModifiers == ControlKey) {
                if (e.Key == Key.W) sender.Close();
                else if (e.Key == Key.OemComma) PreferencesWindow.Create(sender);
                else if (e.Key == Key.M) sender.WindowState = WindowState.Minimized;
                else return false;

            } else return false;

            return true;
        }

        public static void WindowClosed(Window sender) {
            if (Program.Project != null) {
                if (Program.Project.Window != null) return;

                foreach (Track track in Program.Project.Tracks)
                    if (track.Window != null) return;
            }
            
            if (sender is PatternWindow) return;

            if (sender is ProjectWindow) {
                Program.Project = null;

                SplashWindow.Create(sender);

            } else if (sender is TrackWindow) {
                ProjectWindow.Create(sender);
            
            } else if (sender is SplashWindow) {
                Preferences.Window?.Close();
                
                foreach (Launchpad lp in MIDI.Devices)
                    lp.Window?.Close();
            }
        }

        public static string[] Args;

        public static void URL(string url) => Process.Start(new ProcessStartInfo() {
            FileName = url,
            UseShellExecute = true
        });

        public static T GetResource<T>(string res)
            => Application.Current.Styles.TryGetResource(res, out object ret) && ret is T final
                ? final
                : default;

        public override void Initialize() {
            AvaloniaXamlLoader.Load(this);

            instance = this;

            if (Preferences.Theme == ThemeType.Dark) Styles.Add(new Dark());
            else if (Preferences.Theme == ThemeType.Light) Styles.Add(new Light());
        }

        public override void OnFrameworkInitializationCompleted() {
            if (!(ApplicationLifetime is IClassicDesktopStyleApplicationLifetime lifetime)) throw new ApplicationException("Invalid ApplicationLifetime");

            if (Args.Length > 0 && Args[0] == "--update") lifetime.MainWindow = new UpdateWindow();
            else {
                if (!DriverChecker.Run(out MessageWindow driverError)) {
                    lifetime.MainWindow = driverError;

                    base.OnFrameworkInitializationCompleted();
                    return;
                }

                if (!AbletonConnector.Connected) {
                    if (Args.Length > 0) {
                        AbletonConnector.NewInstanceFile(App.Args[0]);
                        Environment.Exit(0);

                    } else lifetime.MainWindow = new MessageWindow(
                        $"Another instance of Apollo Studio is currently running.\n\n" +
                        "Please close other instances of Apollo Studio before launching Apollo Studio."
                    );

                    base.OnFrameworkInitializationCompleted();
                    return;
                }

                Program.HadCrashed = Preferences.Crashed;
                Preferences.Crashed = true;

                if (Preferences.DiscordPresence) Discord.Set(true);

                MIDI.Start();

                foreach (int i in Preferences.VirtualLaunchpads) {
                    LaunchpadWindow.Create(MIDI.ConnectVirtual(i), null);
                    MIDI.Update();
                }
                
                Courier autosave = new Courier(180000, async _ => {
                    if (Preferences.Autosave && Program.Project != null && File.Exists(Program.Project.FilePath) && !Program.Project.Undo.Saved) {
                        try {
                            string dir = Path.Combine(Path.GetDirectoryName(Program.Project.FilePath), $"{Program.Project.FileName} Backups");
                            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

                            await Program.Project.WriteFile(
                                null,
                                Path.Join(dir, $"{Program.Project.FileName} Autosave {DateTime.Now.ToString("yyyy-MM-dd HH-mm-ss")}.approj"),
                                false
                            );
                        } catch {}
                    }

                    Preferences.Save();
                });

                lifetime.Exit += (_, __) => {
                    autosave.Dispose();
                    MIDI.Stop();
                    Discord.Set(false);
                    AbletonConnector.Dispose();
                    Preferences.Crashed = Program.HadCrashed;

                    Preferences.Save();
                };

                lifetime.MainWindow = new SplashWindow();
                base.OnFrameworkInitializationCompleted();
            }
        }
    }
}
