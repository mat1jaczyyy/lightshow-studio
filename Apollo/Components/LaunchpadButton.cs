using System;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Markup.Xaml;
using Avalonia.Media;

using Apollo.Core;
using Apollo.Enums;

namespace Apollo.Components {
    public class LaunchpadButton: UserControl {
        void InitializeComponent() {
            AvaloniaXamlLoader.Load(this);

            Canvas = this.Get<Canvas>("Canvas");
            Path = this.Get<Path>("Path");
        }

        Canvas Canvas;
        Path Path;
        int Index;

        void AddClass(string name) {
            Canvas.Classes.Add(name);
            Path.Classes.Add(name);

            if (name == "corner")
                Path.Data = App.GetResource<StreamGeometry>($"LPGrid_{Index}CornerGeometry");
        }

        public bool Empty => Canvas.Classes.Contains("empty");

        bool IsPhantom() {
            if (Preferences.LaunchpadModel.HasNovationLED() && Index == 9) return false;

            if (Preferences.LaunchpadStyle == LaunchpadStyles.Stock) {
                int x = Index % 10;
                int y = Index / 10;

                if (x == 0 || x == 9 || y == 0 || y == 9) return true;
            }

            return Preferences.LaunchpadStyle == LaunchpadStyles.Phantom;
        }
        
        public int UpdateModel() {
            int x = Index % 10;
            int y = Index / 10;

            int ret = 0;
            if (!Empty) ret--;

            Canvas.Classes.Clear();
            Path.Classes.Clear();

            switch (Preferences.LaunchpadModel) {
                case LaunchpadModels.MK2:
                    if (x == 0 || y == 9 || Index == 9) AddClass("empty");
                    else {
                        ret++;

                        if (x == 9 || y == 0) AddClass("circle");
                        else if (Index == 44 || Index == 45 || Index == 54 || Index == 55) AddClass("corner");
                        else AddClass("square");
                    }
                    break;

                case LaunchpadModels.Pro:
                    if (Index == 0 || Index == 9 || Index == 90 || Index == 99) AddClass("empty");
                    else {
                        ret++;

                        if (x == 0 || x == 9 || y == 0 || y == 9) AddClass("circle");
                        else if (Index == 44 || Index == 45 || Index == 54 || Index == 55) AddClass("corner");
                        else AddClass("square");
                    }
                    break;

                case LaunchpadModels.X:
                    if (x == 0 || y == 9) AddClass("empty");
                    else {
                        ret++;

                        if (Index == 9) AddClass("novation");
                        else if (Index == 44 || Index == 45 || Index == 54 || Index == 55) AddClass("corner");
                        else AddClass("square");
                    }
                    break;

                case LaunchpadModels.ProMK3:
                    if (Index == 90 || Index == 99) AddClass("empty");
                    else {
                        ret++;

                        if (Index == 0) AddClass("hidden");
                        else if (Index == 9) AddClass("novation");
                        else if (Index == 44 || Index == 45 || Index == 54 || Index == 55) AddClass("corner");
                        else if (y == 9) AddClass("split");
                        else AddClass("square");
                    }
                    break;

                case LaunchpadModels.All:
                    ret++;

                    if (Index == 0 || Index == 9 || Index == 90 || Index == 99) AddClass("hidden");
                    else if (Index == 44 || Index == 45 || Index == 54 || Index == 55) AddClass("corner");
                    else AddClass("square");
                    break;
            }

            return ret;
        }

        public void UpdateStyle()
            => Path.Fill = IsPhantom()? SolidColorBrush.Parse("Transparent") : Path.Stroke;

        public void SetColor(SolidColorBrush color)
            => Path.Stroke = IsPhantom()? color : Path.Fill = color;

        public LaunchpadButton() => throw new InvalidOperationException();

        public LaunchpadButton(int index) {
            InitializeComponent();

            Index = index;

            Grid.SetRow(this, Index / 10);
            Grid.SetColumn(this, Index % 10);

            AddClass("empty");
        }
    }
}
