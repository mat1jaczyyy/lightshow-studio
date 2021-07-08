using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;

using Humanizer;

using Apollo.Core;
using Apollo.Helpers;

namespace Apollo.Windows {
    public class UpdateWindow: Window {
        static Image UpdateImage = App.GetResource<Image>("UpdateImage");

        void InitializeComponent() {
            AvaloniaXamlLoader.Load(this);
            
            Root = this.Get<Grid>("Root");
            State = this.Get<TextBlock>("State");
            DownloadProgress = this.Get<ProgressBar>("DownloadProgress");
        }

        Grid Root;
        TextBlock State;
        ProgressBar DownloadProgress;

        Stopwatch time = new Stopwatch();
        bool exiting = false;

        ZipArchiveEntry GetZipFolder(ZipArchive zip, string folder) => zip.Entries.First(i => {
            string[] path = i.FullName.Split('/');
            return path.Length == 3 && path[1] == folder && path[2] == "";
        });

        void ExtractWin(ZipArchiveEntry directory, string path) {
            if (Directory.Exists(path))
                Directory.Delete(path, true);
            
            Directory.CreateDirectory(path);

            foreach (ZipArchiveEntry i in directory.Archive.Entries) {
                if (i.FullName.StartsWith(directory.FullName)) {
                    string name = i.FullName.Replace(directory.FullName, "");

                    if (name.EndsWith("/")) Directory.CreateDirectory(Path.Combine(path, name));
                    else if (name != "") i.ExtractToFile(Path.Combine(path, name));
                }
            }
        }

        void ExtractMac(string directory, string path) {
            if (Directory.Exists(path))
                Directory.Delete(path, true);
            
            Directory.Move(directory, path);
        }

        public UpdateWindow() {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                throw new InvalidOperationException("Auto-updating is not supported on Linux");

            InitializeComponent();
            #if DEBUG
                this.AttachDevTools();
            #endif
            
            Root.Children.Add(UpdateImage);
        }

        async void Loaded(object sender, EventArgs e) {
            Position = new PixelPoint(Position.X, Math.Max(0, Position.Y));

            State.Text = "Downloading...";

            WebClient downloader = new WebClient();
            downloader.DownloadProgressChanged += Progress;
            downloader.DownloadDataCompleted += Downloaded;
            downloader.DownloadDataAsync(new Uri((await Github.LatestDownload()).BrowserDownloadUrl));
        }
        
        void Unloaded(object sender, CancelEventArgs e) {
            if (!exiting) {
                e.Cancel = true;
                return;
            }

            Root.Children.Remove(UpdateImage);

            this.Content = null;
        }

        void Progress(object sender, DownloadProgressChangedEventArgs e) {
            if (!time.IsRunning) time.Start();

            DownloadProgress.Value = e.ProgressPercentage;
            State.Text = $"Downloading... ({(e.BytesReceived * 1000.0 / (time.ElapsedMilliseconds + 50)).Bytes().Humanize("#.#")}/s)";
        }

        void Downloaded(object sender, AsyncCompletedEventArgs e) {
            byte[] result = ((DownloadDataCompletedEventArgs)e).Result;
                
            string updatepath = Program.GetBaseFolder("Update");
            string temppath = Program.GetBaseFolder("Temp");
            string tempm4lpath = Program.GetBaseFolder("TempM4L");

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
                ZipArchive zip = new ZipArchive(new MemoryStream(result));

                ExtractWin(GetZipFolder(zip, "Update"), updatepath);
                ExtractWin(GetZipFolder(zip, "Apollo"), temppath);
                ExtractWin(GetZipFolder(zip, "M4L"), tempm4lpath);

            } else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) {
                string zippath = Program.GetBaseFolder("Zip");

                if (Directory.Exists(zippath))
                    Directory.Delete(zippath, true);
                
                Directory.CreateDirectory(zippath);

                string zipfile = Path.Combine(zippath, "update.zip");
            
                File.WriteAllBytes(zipfile, result);

                Process.Start(new ProcessStartInfo(
                    "ditto", $"-x -k --sequesterRsrc --rsrc \"{zipfile}\" \"{zippath}\""
                )).WaitForExit();

                string foldername = Directory.GetDirectories(zippath)[0];

                ExtractMac(Path.Combine(zippath, foldername, "Update"), updatepath);
                ExtractMac(Path.Combine(zippath, foldername, "Apollo"), temppath);
                ExtractMac(Path.Combine(zippath, foldername, "M4L"), tempm4lpath);

                Directory.Delete(zippath, true);
            }

            Program.LaunchUpdater = true;

            exiting = true;
            App.Shutdown();
        }
        
        void Minimize() => WindowState = WindowState.Minimized;

        void MoveWindow(object sender, PointerPressedEventArgs e) => BeginMoveDrag(e);
        
        public static void Create(Window owner) {
            UpdateWindow window = new UpdateWindow();
                
            if (owner == null || owner.WindowState == WindowState.Minimized) 
                window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            else
                window.Owner = owner;

            window.Show();
            window.Owner = null;

            window.Topmost = true;
            window.Topmost = Preferences.AlwaysOnTop;
        }
    }
}
