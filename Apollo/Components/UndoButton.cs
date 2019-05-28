﻿using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.VisualTree;

using Apollo.Core;
using Apollo.Windows;

namespace Apollo.Components {
    public class UndoButton: UserControl {
        private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

        private void Update_Position(int position) =>
            this.Resources["CanvasBrush"] = (SolidColorBrush)Application.Current.Styles.FindResource((position == 0)
                ? "ThemeControlHighBrush"
                : "ThemeForegroundLowBrush"
            );

        public UndoButton() {
            InitializeComponent();

            Program.Project.Undo.PositionChanged += Update_Position;
            Update_Position(Program.Project.Undo.Position);
        }

        private void Click(object sender, PointerReleasedEventArgs e) {
            if (e.MouseButton == MouseButton.Left) Program.Project.Undo.Undo();
            else if (e.MouseButton == MouseButton.Right) UndoWindow.Create((Window)this.GetVisualRoot());
        }
    }
}
