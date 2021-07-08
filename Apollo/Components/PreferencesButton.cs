using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.VisualTree;

using Apollo.Core;
using Apollo.Windows;

namespace Apollo.Components {
    public class PreferencesButton: IconButton {
        void InitializeComponent() {
            AvaloniaXamlLoader.Load(this);
   
            Hole = this.Get<Ellipse>("Hole");
        }

        Ellipse Hole;

        public IBrush HoleFill {
            get => Hole.Fill;
            set => Hole.Fill = value;
        }

        protected override IBrush Fill {
            get => (IBrush)this.Resources["Brush"];
            set => this.Resources["Brush"] = value;
        }

        public PreferencesButton() {
            InitializeComponent();

            base.MouseLeave(this, null);

            Hole.Fill = App.GetResource<SolidColorBrush>("ThemeBorderMidBrush");
        }

        protected override void Click(PointerReleasedEventArgs e) => PreferencesWindow.Create((Window)this.GetVisualRoot());
    }
}
