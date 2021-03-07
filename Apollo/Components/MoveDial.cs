using System;
using System.Collections.Generic;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Threading;

using Apollo.Core;
using Apollo.Structures;

namespace Apollo.Components {
    public class MoveDial: UserControl {
        void InitializeComponent() {
            AvaloniaXamlLoader.Load(this);

            PlaneCanvas = this.Get<Canvas>("PlaneCanvas");
            AbsoluteCanvas = this.Get<Canvas>("AbsoluteCanvas");

            XRect = this.Get<Rectangle>("XRect");
            YRect = this.Get<Rectangle>("YRect");
            Point = this.Get<Rectangle>("Point");
            AbsolutePoint = this.Get<Rectangle>("AbsolutePoint");

            Display = this.Get<TextBlock>("Display");

            InputX = this.Get<TextBox>("InputX");
            InputY = this.Get<TextBox>("InputY");
        }

        HashSet<IDisposable> observables = new();

        public delegate void ChangedEventHandler(int x, int y, int? old_x, int? old_y);
        public event ChangedEventHandler Changed, AbsoluteChanged;
        
        public delegate void SwitchedEventHandler();
        public event SwitchedEventHandler Switched;

        Canvas PlaneCanvas, AbsoluteCanvas;
        Rectangle XRect, YRect, Point, AbsolutePoint;
        TextBlock Display;
        TextBox InputX, InputY;

        int _x = 0;
        public int X {
            get => _x;
            set {
                value = Math.Clamp(value, -9, 9);
                if (value != _x) {
                    _x = value;
                    DrawX();
                    DrawPoint();
                    Changed?.Invoke(_x, _y, null, null);
                }
            }
        }

        int _y = 0;
        public int Y {
            get => _y;
            set {
                value = Math.Clamp(value, -9, 9);
                if (value != _y) {
                    _y = value;
                    DrawY();
                    DrawPoint();
                    Changed?.Invoke(_x, _y, null, null);
                }
            }
        }

        int _ax = 0;
        public int AbsoluteX {
            get => _ax;
            set {
                value = Math.Clamp(value, 0, 9);
                if (value != _ax) {
                    _ax = value;
                    DrawPoint();
                    AbsoluteChanged?.Invoke(_ax, _ay, null, null);
                }
            }
        }

        int _ay = 0;
        public int AbsoluteY {
            get => _ay;
            set {
                value = Math.Clamp(value, 0, 9);
                if (value != _ay) {
                    _ay = value;
                    DrawPoint();
                    AbsoluteChanged?.Invoke(_ax, _ay, null, null);
                }
            }
        }

        Canvas CurrentCanvas => AbsoluteCanvas.IsVisible? AbsoluteCanvas : PlaneCanvas;
        int CurrentX => AbsoluteCanvas.IsVisible? AbsoluteX : X;
        int CurrentY => AbsoluteCanvas.IsVisible? AbsoluteY : Y;

        string ValueString => $"({(AbsoluteCanvas.IsVisible? _ax : _x)}, {(AbsoluteCanvas.IsVisible? _ay : _y)})";

        void DrawPoint() {
            Canvas.SetLeft(Point, 18 + 2 * _x);
            Canvas.SetTop(Point, 18 - 2 * _y);

            Canvas.SetLeft(AbsolutePoint, 4 * _ax);
            Canvas.SetTop(AbsolutePoint, 36 - 4 * _ay);

            Display.Text = ValueString;
        }

        void DrawX() {
            XRect.Width = Math.Abs(2 * _x) + 2;
            Canvas.SetLeft(XRect, (_x > 0)? 18 : 20 - XRect.Width);
        }

        void DrawY() {
            YRect.Height = Math.Abs(2 * _y) + 2;
            Canvas.SetTop(YRect, (_y < 0)? 18 : 20 - YRect.Height);
        }

        public MoveDial() {
            InitializeComponent();

            observables.Add(InputX.GetObservable(TextBox.TextProperty).Subscribe(InputX_Changed));
            InputX.AddHandler(InputElement.PointerPressedEvent, Input_MouseDown, RoutingStrategies.Tunnel);

            observables.Add(InputY.GetObservable(TextBox.TextProperty).Subscribe(InputY_Changed));
            InputY.AddHandler(InputElement.PointerPressedEvent, Input_MouseDown, RoutingStrategies.Tunnel);

            DrawX();
            DrawY();
            DrawPoint();
        }

        void Unloaded(object sender, VisualTreeAttachmentEventArgs e) {
            Changed = null;
            AbsoluteChanged = null;
            Switched = null;

            InputX.RemoveHandler(InputElement.PointerPressedEvent, Input_MouseDown);
            InputY.RemoveHandler(InputElement.PointerPressedEvent, Input_MouseDown);

            foreach (IDisposable observable in observables)
                observable.Dispose();
        }

        public void Update(Offset offset) {
            X = offset.X;
            Y = offset.Y;

            AbsoluteX = offset.AbsoluteX;
            AbsoluteY = offset.AbsoluteY;

            PlaneCanvas.IsVisible = !(AbsoluteCanvas.IsVisible = offset.IsAbsolute);
            DrawPoint();
        }

        bool mouseHeld = false;
        int old_x, old_y;
        double lastX, lastY;

        void MouseDown(object sender, PointerPressedEventArgs e) {
            PointerUpdateKind MouseButton = e.GetCurrentPoint(this).Properties.PointerUpdateKind;

            if (MouseButton == PointerUpdateKind.LeftButtonPressed) {
                if (e.ClickCount == 2) {
                    DisplayPressed(sender, e);
                    return;
                }

                mouseHeld = true;
                e.Pointer.Capture(CurrentCanvas);

                lastX = e.GetPosition(CurrentCanvas).X;
                lastY = e.GetPosition(CurrentCanvas).Y;
                old_x = CurrentX;
                old_y = CurrentY;

                CurrentCanvas.Cursor = new Cursor(StandardCursorType.SizeAll);
            }
        }

        void MouseUp(object sender, PointerReleasedEventArgs e) {
            PointerUpdateKind MouseButton = e.GetCurrentPoint(this).Properties.PointerUpdateKind;
            
            if (MouseButton == PointerUpdateKind.LeftButtonReleased) {
                mouseHeld = false;
                e.Pointer.Capture(null);

                if (AbsoluteCanvas.IsVisible) {
                    if (old_x != AbsoluteX || old_y != AbsoluteY)
                        AbsoluteChanged?.Invoke(AbsoluteX, AbsoluteY, old_x, old_y);

                } else {
                    if (old_x != X || old_y != Y)
                        Changed?.Invoke(X, Y, old_x, old_y);
                }

                CurrentCanvas.Cursor = new Cursor(StandardCursorType.Hand);

            } else if (!mouseHeld && MouseButton == PointerUpdateKind.RightButtonReleased)
                Switched?.Invoke();
        }

        void MouseMove(object sender, PointerEventArgs e) {
            if (mouseHeld) {
                double x = e.GetPosition(CurrentCanvas).X;
                double y = e.GetPosition(CurrentCanvas).Y;

                if (Math.Abs(x - lastX) >= 4) {
                    int change = (int)((x - lastX) / 4);

                    if (AbsoluteCanvas.IsVisible) AbsoluteX += change;
                    else X += change;
                    
                    lastX = x;
                }

                if (Math.Abs(y - lastY) >= 4) {
                    int change = -(int)((y - lastY) / 4);

                    if (AbsoluteCanvas.IsVisible) AbsoluteY += change;
                    else Y += change;

                    lastY = y;
                }
            }
        }

        Action InputX_Update = null, InputY_Update = null;

        void InputX_Changed(string text) {
            int x = Input_Changed(InputX, InputX_Update, CurrentX, text);

            if (AbsoluteCanvas.IsVisible) AbsoluteX = x;
            else X = x;
        }

        void InputY_Changed(string text) {
            int y = Input_Changed(InputY, InputY_Update, CurrentY, text);
            
            if (AbsoluteCanvas.IsVisible) AbsoluteY = y;
            else Y = y;
        }

        int Input_Changed(TextBox Input, Action Update, int RawValue, string text) {
            if (text == null) return RawValue;
            if (text == "") return RawValue;

            Update = () => { Input.Text = RawValue.ToString(); };

            if (int.TryParse(text, out int value)) {
                if ((AbsoluteCanvas.IsVisible? 0 : -9) <= value && value <= 9) {
                    RawValue = value;
                    Update = () => { Input.Foreground = (IBrush)Application.Current.Styles.FindResource("ThemeForegroundBrush"); };
                } else {
                    Update = () => { Input.Foreground = (IBrush)Application.Current.Styles.FindResource("ErrorBrush"); };
                }

                Update += () => {
                    if (value < 0) text = $"-{text.Substring(1).TrimStart('0')}";
                    else if (value > 0) text = text.TrimStart('0');
                    else text = "0";

                    if (!AbsoluteCanvas.IsVisible && value < -9) text = "-9";
                    if (value > 9) text = "9";
                    
                    Input.Text = text;
                };
            }

            if (!AbsoluteCanvas.IsVisible && text == "-") Update = null;

            Dispatcher.UIThread.InvokeAsync(() => {
                Update?.Invoke();
                Update = null;
            });

            return RawValue;
        }

        void DisplayPressed(object sender, PointerPressedEventArgs e) {
            PointerUpdateKind MouseButton = e.GetCurrentPoint(this).Properties.PointerUpdateKind;

            if (MouseButton == PointerUpdateKind.LeftButtonPressed && e.ClickCount == 2) {
                InputX.Text = CurrentX.ToString();
                InputY.Text = CurrentY.ToString();

                InputX.SelectionStart = 0;
                InputX.SelectionEnd = InputX.Text.Length;
                InputX.CaretIndex = InputX.Text.Length;

                Display.Opacity = 0;
                Display.IsHitTestVisible = false;

                InputX.Opacity = InputY.Opacity = 1;
                InputX.IsHitTestVisible = InputY.IsHitTestVisible = true;
                InputX.Focus();

                e.Handled = true;
            }
        }

        bool SkipLostFocus = false;
        
        void Input_LostFocus(object sender, RoutedEventArgs e) {
            if (SkipLostFocus) {
                SkipLostFocus = false;
                return;
            }

            InputX.Text = CurrentX.ToString();
            InputY.Text = CurrentY.ToString();

            Display.Opacity = 1;
            Display.IsHitTestVisible = true;

            InputX.Opacity = InputY.Opacity = 0;
            InputX.IsHitTestVisible = InputY.IsHitTestVisible = false;

            if (AbsoluteCanvas.IsVisible) {
                if (old_x != AbsoluteX || old_y != AbsoluteY)
                    AbsoluteChanged?.Invoke(AbsoluteX, AbsoluteY, old_x, old_y);

            } else {
                if (old_x != X || old_y != Y)
                    Changed?.Invoke(X, Y, old_x, old_y);
            }
        }

        void Input_KeyDown(object sender, KeyEventArgs e) {
            if (App.Dragging) return;

            if (e.Key == Key.Return)
                this.Focus();
            
            if (e.Key == Key.Tab && sender is TextBox textBox) {
                SkipLostFocus = true;

                TextBox i = (textBox == InputX)? InputY : InputX;
                
                i.SelectionStart = 0;
                i.SelectionEnd = i.Text.Length;
                i.CaretIndex = i.Text.Length;

                i.Focus();
            }

            e.Key = Key.None;
        }

        void Input_KeyUp(object sender, KeyEventArgs e) {
            if (App.Dragging) return;

            e.Key = Key.None;
        }

        void Input_MouseDown(object sender, PointerPressedEventArgs e) {
            TextBox Input = (TextBox)sender;

            if (!Input.IsFocused) {
                SkipLostFocus = true;
                Input.Focus();
            }
        }

        void Input_MouseUp(object sender, PointerReleasedEventArgs e) => e.Handled = true;
    }
}
