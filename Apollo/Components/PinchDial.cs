using System;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;

using Apollo.Core;

namespace Apollo.Components {
    public class PinchDial: Dial {
        public override bool UsingSteps {
            get => IsBilateral;
            set => IsBilateral = value;
        }
        
        bool _isBilateral;
        public bool IsBilateral {
            get => _isBilateral;
            set {
                _isBilateral = value;
                
                DrawArcAuto();
            }
        }

        public PinchDial() {
            Title = "Pinch";

            Minimum = -2;
            Maximum = 2;
            Round = 2;
            Default = 0;
            
            IsBilateral = false;
        }

        Geometry CreateGeometry(string geometry, double value) {
            double realHeight = height * Scale;
            double margin = (width - height) * Scale / 2;

            return Geometry.Parse(String.Format("M {0} {1} " + geometry + " {6} 0",
                margin.ToString(),
                (realHeight).ToString(),
                (realHeight * (1 - value) + margin).ToString(),
                (realHeight * (1 - value)).ToString(),
                (realHeight * value + margin).ToString(),
                (realHeight * value).ToString(),
                (realHeight + margin).ToString()
            ));
        }
        
        protected override void DrawArc(Path Arc, double value, bool overrideBase, string color = "ThemeAccentBrush") {
            if (overrideBase) return;
            
            Display.Text = (Enabled || !DisplayDisabledText)? ValueString : DisabledText;

            ArcBase.StrokeThickness = stroke * Scale;
            ArcBase.IsVisible = Enabled;

            Arc.Stroke = App.GetResource<IBrush>(Enabled? color : "ThemeForegroundLowBrush");
            Arc.StrokeThickness = stroke * Scale / 2;
            
            ArcBase.Data = Arc.Data = CreateGeometry(IsBilateral? "C {2} {3} {4} {5}" : "Q {2} {3}", value);
        }
        
        void DrawArcBilateral() {
            if (IsBilateral) DrawArc(Arc, Value, false, "ThemeExtraBrush");
        }

        protected override void DrawArcAuto() {
            if (IsBilateral) DrawArcBilateral();
            else DrawArcValue();
        }
    }
}