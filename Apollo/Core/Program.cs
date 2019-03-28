﻿using System;
using System.Diagnostics;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Logging.Serilog;

using RtMidi.Core;

using Apollo.Elements;
using Apollo.Windows;

// Suppresses readonly suggestion
[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0044:Add readonly modifier")]

namespace Apollo.Core {
    class Program {
        public static AppBuilder BuildAvaloniaApp()
            => AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .LogToDebug();
        
        public static bool log = true;
        static Stopwatch logTimer = new Stopwatch();

        public static void Log(string text) {
            if (log)
                Console.WriteLine($"[{logTimer.Elapsed.ToString()}] {text}");
        }

        public static Project Project;

        public static void WindowClose(Window sender) {
            if (Project == null || Project.Window != null) return;
            
            for (int i = 0; i < Program.Project.Count; i++)
                if (Program.Project[i].Window != null) return;

            Type type = sender.GetType();

            if (type == typeof(ProjectWindow)) {
                Project.Dispose();
                Project = null;
                new SplashWindow().Show();

            } else if (type == typeof(TrackWindow)) {
                ProjectWindow.Create();
            }
        }

        static void Main(string[] args) {
            logTimer.Start();

            foreach (string arg in args) {
                /*if (arg.Equals("--log")) {
                    log = true;
                }*/
            }
            
            foreach (var api in MidiDeviceManager.Default.GetAvailableMidiApis())
                Log($"MIDI API: {api}");

            MIDI.Rescan();

            Log("ready");

            BuildAvaloniaApp().Start<SplashWindow>();
        }
    }
}
