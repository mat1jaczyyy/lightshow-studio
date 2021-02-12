using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;

using Avalonia;

using Apollo.Binary;
using Apollo.Elements;

// Suppresses readonly suggestion
[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0044:Add readonly modifier")]

namespace Apollo.Core {
    class Program {
        public static readonly string Version = "Version 1.9.0";

        public static AppBuilder BuildAvaloniaApp()
            => AppBuilder.Configure<App>()
                .UsePlatformDetect();

        public static string GetBaseFolder(string folder) => Path.Combine(
            Directory.GetParent(
                Path.GetDirectoryName(AppDomain.CurrentDomain.BaseDirectory)
            ).FullName,
            folder
        );

        public static readonly string UserPath = Path.Combine(Environment.GetEnvironmentVariable(
            RuntimeInformation.IsOSPlatform(OSPlatform.Windows)? "USERPROFILE" : "HOME"
        ), ".apollostudio"
            #if PRERELEASE
                + "-prerelease"
            #endif
        );

        public static readonly string CrashDir = Path.Combine(UserPath, "Crashes");
        public static readonly string CrashProject = Path.Combine(CrashDir, "crash.approj");

        public static bool HadCrashed = false;

        public static bool LaunchAdmin = false;
        public static bool LaunchUpdater = false;
        
        public static Stopwatch TimeSpent = new Stopwatch();
        public static void Log(string text) => Console.WriteLine($"[{TimeSpent.Elapsed.ToString()}] {text}");

        static bool DebugLogging = false;
        public static void DebugLog(string text) {
            if (DebugLogging) Log(text);
        }

        public delegate void ProjectLoadedEventHandler();
        public static event ProjectLoadedEventHandler ProjectLoaded;

        static Project _project;
        public static Project Project {
            get => _project;
            set {
                MIDI.ClearState(force: true);

                _project?.Dispose();

                if ((_project = value) == null) {
                    if (Directory.Exists(Program.CrashDir))
                        File.Delete(Program.CrashProject);
                
                } else {
                    _project.WriteCrashBackup();

                    ProjectLoaded?.Invoke();
                }
                
                ProjectLoaded = null;
            }
        }

        [STAThread]
        static void Main(string[] args) {
            if (args.Contains("--debug")) DebugLogging = true;

            AppDomain.CurrentDomain.UnhandledException += (object sender, UnhandledExceptionEventArgs e) => {
                if (!Directory.Exists(CrashDir)) Directory.CreateDirectory(CrashDir);
                
                using (MemoryStream memoryStream = new MemoryStream()) {
                    string crashName = Path.Combine(CrashDir, $"Crash-{DateTimeOffset.Now.ToUnixTimeSeconds()}");

                    using (ZipArchive archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, true)) {
                        string additional = "";
                        
                        if (Project != null) {
                            try {
                                byte[] project = Encoder.Encode(Project);

                                File.WriteAllBytes(CrashProject, project);

                                if (project != null)
                                    using (Stream writer = archive.CreateEntry("project.approj").Open())
                                        writer.Write(project);

                            } catch (Exception ex) {
                                additional = "\r\n\r\n" + 
                                    "There was an additional exception while attempting to store the project for the crash log:\r\n\r\n" +
                                    ex.ToString();
                            }
                        }

                        using (Stream log = archive.CreateEntry("exception.log").Open())
                            using (StreamWriter writer = new StreamWriter(log)) {
                                writer.Write(
                                    $"Apollo Version: {Version}\r\n" +
                                    $"Operating System: {RuntimeInformation.OSDescription}\r\n\r\n" +
                                    e.ExceptionObject.ToString() +
                                    additional
                                );
                            }
                    }

                    File.WriteAllBytes(crashName + ".zip", memoryStream.ToArray());
                }

                if (TimeSpent.IsRunning) TimeSpent.Stop();

                if (e.IsTerminating && Project != null) {
                    Preferences.Crashed = true;
                    Preferences.CrashPath = Project.FilePath;
                }

                Preferences.Save();
            };

            TimeSpent.Start();
            
            App.Args = args;
            
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(null);

            TimeSpent.Stop();

            if (LaunchAdmin && RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                Process.Start(
                    $"{AppDomain.CurrentDomain.BaseDirectory}elevate.exe",
                    $"\"{Path.Combine(Program.GetBaseFolder("Apollo"), "Apollo.exe")}\" --update"
                );

            else if (LaunchUpdater) {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    Process.Start(
                        $"{AppDomain.CurrentDomain.BaseDirectory}elevate.exe",
                        $"\"{Path.Combine(Program.GetBaseFolder("Update"), "ApolloUpdate.exe")}\""
                    );
                
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                    Process.Start(Path.Combine(Program.GetBaseFolder("Update"), "ApolloUpdate"));
            }
        }
    }
}
