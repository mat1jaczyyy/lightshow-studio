using System;
using System.Collections.Generic;
using System.Linq;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Input;
using Avalonia.Threading;

using Apollo.Core;
using Apollo.Enums;
using Apollo.Rendering;
using Apollo.Structures;

namespace Apollo.Components {
    public class LaunchpadGrid: UserControl {
        void InitializeComponent() {
            AvaloniaXamlLoader.Load(this);

            Root = this.Get<LayoutTransformControl>("Root");
            View = this.Get<Border>("View");
            Back = this.Get<Border>("Back");

            ModeLight = this.Get<Rectangle>("ModeLight");
        }
        
        LayoutTransformControl Root;
        Border View, Back;
        Grid Grid;
        LaunchpadButton[] Buttons;
        Rectangle ModeLight;

        public delegate void PadChangedEventHandler(int index);
        public event PadChangedEventHandler PadStarted;
        public event PadChangedEventHandler PadFinished;
        public event PadChangedEventHandler PadPressed;
        public event PadChangedEventHandler PadReleased;

        public delegate void PadModsChangedEventHandler(int index, KeyModifiers mods);
        public event PadModsChangedEventHandler PadModsPressed;

        public static int GridToSignal(int index) => (index == -1)? 100 : ((9 - (index / 10)) * 10 + index % 10);
        public static int SignalToGrid(int index) => (index == 100)? -1 : ((9 - (index / 10)) * 10 + index % 10);

        public void SetColor(int index, SolidColorBrush color) {
            if (index == -1) {
                if (IsArrangeValid) ModeLight.Fill = color;
                else this.Resources["ModeBrush"] = color;

            } else Buttons[index].SetColor(color);
        }

        public void RawUpdate(RawUpdate n) => Dispatcher.UIThread.InvokeAsync(() => {
            SetColor(LaunchpadGrid.SignalToGrid(n.Index), n.Color.ToScreenBrush());
        });

        public void Clear() {
            SolidColorBrush color = App.GetResource<SolidColorBrush>("ThemeForegroundLowBrush");
            for (int i = -1; i < 100; i++) SetColor(i, color);
        }

        void Update_LaunchpadStyle() {
            for (int i = 0; i < 100; i++)
                Buttons[i].UpdateStyle();
        }

        void Update_LaunchpadModel() {
            Grid?.Children.Clear();

            int buttons = Preferences.LaunchpadModel.GridSize();

            IEnumerable<string> ones = Enumerable.Range(0, buttons).Select(i => "*");
            IEnumerable<string> zeros = Enumerable.Range(0, 10 - buttons).Select(i => "0");

            View.Child = Grid = new Grid() {
                RowDefinitions = RowDefinitions.Parse(
                    String.Join(
                        ",",
                        ones.Concat(zeros).ToArray()
                    )
                ),
                ColumnDefinitions = ColumnDefinitions.Parse(
                    String.Join(
                        ",",
                        zeros.Concat(ones).ToArray()
                    )
                )
            };

            for (int i = 0; i < 100; i++) {
                int mouse = Buttons[i].UpdateModel();

                if (mouse < 0) Buttons[i].PointerPressed -= MouseDown;
                else if (mouse > 0) Buttons[i].PointerPressed += MouseDown;

                Grid.Children.Add(Buttons[i]);
            }

            ModeLight.IsVisible = Preferences.LaunchpadModel.HasModeLight();

            Update_LaunchpadStyle();
        }

        void Update_LaunchpadRotation()
            => Root.LayoutTransform = new RotateTransform(Preferences.LaunchpadGridRotation? -45.0 : 0.0);

        public LaunchpadGrid() {
            InitializeComponent();

            Buttons = new LaunchpadButton[100];

            for (int i = 0; i < 100; i++)
                Buttons[i] = new LaunchpadButton(i);

            Preferences.LaunchpadModelChanged += Update_LaunchpadModel;
            Preferences.LaunchpadStyleChanged += Update_LaunchpadStyle;
            Preferences.LaunchpadGridRotationChanged += Update_LaunchpadRotation;

            Update_LaunchpadModel();
            Update_LaunchpadRotation();
            
            Clear();
        }

        void Unloaded(object sender, VisualTreeAttachmentEventArgs e) {
            PadStarted = null;
            PadFinished = null;
            PadPressed = null;
            PadReleased = null;
            PadModsPressed = null;

            for (int i = 0; i < 100; i++)
                if (!Buttons[i].Empty)
                    Buttons[i].PointerPressed -= MouseDown;

            Buttons = null;
            Grid.Children.Clear();

            Preferences.LaunchpadModelChanged -= Update_LaunchpadModel;
            Preferences.LaunchpadStyleChanged -= Update_LaunchpadStyle;
            Preferences.LaunchpadGridRotationChanged -= Update_LaunchpadRotation;
        }

        public void RenderFrame(Frame frame) {
            for (int i = 0; i < 101; i++)
                SetColor(SignalToGrid(i), frame.Screen[i].ToScreenBrush());
        }

        bool mouseHeld = false;
        IControl mouseOver = null;

        void MouseDown(object sender, PointerPressedEventArgs e) {
            PointerUpdateKind MouseButton = e.GetCurrentPoint(this).Properties.PointerUpdateKind;

            if (MouseButton == PointerUpdateKind.LeftButtonPressed) {
                mouseHeld = true;

                e.Pointer.Capture(Root);
                Root.Cursor = new Cursor(StandardCursorType.Hand);

                PadStarted?.Invoke(Array.IndexOf(Buttons, (IControl)sender));
                MouseMove(sender, e);
            }
        }

        void MouseUp(object sender, PointerReleasedEventArgs e) {
            PointerUpdateKind MouseButton = e.GetCurrentPoint(this).Properties.PointerUpdateKind;

            if (MouseButton == PointerUpdateKind.LeftButtonReleased) {
                MouseMove(sender, e);
                PadFinished?.Invoke(Array.IndexOf(Buttons, (IControl)sender));

                mouseHeld = false;
                if (mouseOver != null) MouseLeave(mouseOver);
                mouseOver = null;

                e.Pointer.Capture(null);
                Root.Cursor = new Cursor(StandardCursorType.Arrow);
            }
        }

        void MouseEnter(IControl control, KeyModifiers mods) {
            int index = Array.IndexOf(Buttons, control);
            PadPressed?.Invoke(index);
            PadModsPressed?.Invoke(index, mods);
        }

        void MouseLeave(IControl control) => PadReleased?.Invoke(Array.IndexOf(Buttons, control));

        void MouseMove(object sender, PointerEventArgs e) {
            if (mouseHeld) {
                IInputElement _over = Root.InputHitTest(e.GetPosition(Root));

                if (_over is Shape overPath && !(_over is Rectangle))
                    _over = overPath.Parent;

                if (_over is Canvas overCanvas)
                    _over = overCanvas.Parent;
                
                if (_over is LaunchpadButton || _over is Rectangle) {
                    IControl over = (IControl)_over;

                    if (mouseOver == null) MouseEnter(over, e.KeyModifiers);
                    else if (mouseOver != over) {
                        MouseLeave(mouseOver);
                        MouseEnter(over, e.KeyModifiers);
                    }

                    mouseOver = over;

                } else if (mouseOver != null) {
                    MouseLeave(mouseOver);
                    mouseOver = null;
                }
            }
        }
    }
}
