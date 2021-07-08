using System;

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
    public class Dial: UserControl {
        protected virtual void InitializeComponent() {
            AvaloniaXamlLoader.Load(this);

            ArcCanvas = this.Get<Canvas>("ArcCanvas");
            ArcBase = this.Get<Path>("ArcBase");
            Arc = this.Get<Path>("Arc");

            Display = this.Get<TextBlock>("Display");
            TitleText = this.Get<TextBlock>("Title");

            Input = this.Get<TextBox>("Input");
        }
        
        IDisposable observable;

        public delegate void DialStartedEventHandler();
        public event DialStartedEventHandler Started;

        public delegate void DialValueChangedEventHandler(Dial sender, double NewValue, double? OldValue);
        public event DialValueChangedEventHandler ValueChanged;

        public delegate void DialStepChangedEventHandler(int NewValue, int? OldValue);
        public event DialStepChangedEventHandler StepChanged;

        public delegate void DialModeChangedEventHandler(bool NewValue, bool? OldValue);
        public event DialModeChangedEventHandler ModeChanged;

        protected Canvas ArcCanvas;
        protected Path ArcBase, Arc;
        protected TextBlock TitleText, Display;
        protected TextBox Input;

        protected const double width = 43, height = 39;
        protected const double radius = 18, stroke = 7;
        protected const double strokeHalf = stroke / 2;

        protected const double angle_start = 4 * Math.PI / 3;
        protected const double angle_end = -1 * Math.PI / 3;
        protected const double angle_center = Math.PI / 2;

        protected double ToValue(double rawValue) => Math.Pow((rawValue - _min) / (_max - _min), 1 / _exp);
        protected double ToRawValue(double value) => _min + (_max - _min) * Math.Pow(value, _exp);

        double _min = 0;
        public double Minimum {
            get => _min;
            set {
                if (_min != value) {
                    _min = value;
                    Value = ToValue(_raw);
                }
            }
        }

        protected double _max = 100;
        public double Maximum {
            get => _max;
            set {
                if (_max != value) {
                    _max = value;
                    Value = ToValue(_raw);
                }
            }
        }

        protected double? _errorvalue = null;
        public double? ErrorValue {
            get => _errorvalue;
            set {
                if (_errorvalue != value) {
                    _errorvalue = value;
                    DrawArcAuto();
                }
            }
        }

        string _errortext = "";
        public string ErrorText {
            get => _errortext;
            set {
                _errortext = value;
                DrawArcAuto();
            }
        }

        int _round = 0;
        public int Round {
            get => _round;
            set {
                if (_round != value) {
                    _round = value;
                    Value = ToValue(_raw);
                }
            }
        }

        double _exp = 1;
        public double Exponent {
            get => _exp;
            set {
                if (_exp != value) {
                    _exp = value;
                    Value = ToValue(_raw);
                }
            }
        }

        bool _valuechanging = false;
        double _value = 0.5;
        public double Value {
            get => _value;
            set {
                value = Math.Max(0, Math.Min(1, value));
                if (!_valuechanging && value != _value) {
                    _valuechanging = true;

                    _value = value;
                    RawValue = ToRawValue(_value);
                    DrawArcAuto();

                    _valuechanging = false;
                }
            }
        }

        bool _rawchanging = false;
        double _raw = 50;
        public double RawValue {
            get => _raw;
            set {
                value = Math.Round(Math.Max(_min, Math.Min(_max, value)) * Math.Pow(10, _round), 0) / Math.Pow(10, _round);
                if (!_rawchanging && _raw != value) {
                    _rawchanging = true;

                    _raw = value;
                    Value = ToValue(_raw);
                    Display.Text = ValueString;

                    ValueChanged?.Invoke(this, _raw, null);

                    _rawchanging = false;
                }
            }
        }
        
        double _default = 50;
        public double Default {
            get => _default;
            set => _default = Math.Round(Math.Max(_min, Math.Min(_max, value)) * Math.Pow(10, _round), 0) / Math.Pow(10, _round);
        }

        string _title = "Dial";
        public string Title {
            get => _title;
            set => TitleText.Text = _title = value;
        }

        string _unit = "";
        public string Unit {
            get => _unit;
            set {
                _unit = value;
                DrawArcAuto();
            }
        }

        string _disabledtext = "Disabled";
        public string DisabledText {
            get => _disabledtext;
            set {
                _disabledtext = value;
                
                if (!Enabled) Display.Text = value;
            }
        }
        
        bool _displaydisabledtext = true;
        public bool DisplayDisabledText {
            get => _displaydisabledtext;
            set {
                _displaydisabledtext = value;

                this.Focus();
                DrawArcAuto();
            }
        }

        bool _centered = false;
        public bool Centered {
            get => _centered;
            set {
                _centered = value;
                DrawArcAuto();
            }
        }

        bool _fillstart = true;
        public bool FillStart {
            get => _fillstart;
            set {
                if (value != _fillstart) {
                    _fillstart = value;

                    DrawArcAuto();
                }
            }
        }

        bool _enabled = true;
        public bool Enabled {
            get => _enabled;
            set {
                _enabled = value;

                this.Focus();
                DrawArcAuto();
            }
        }

        double _scale = 1;
        public double Scale {
            get => _scale;
            set {
                value = Math.Max(0, Math.Min(1, value));
                if (value != _scale) {
                    _scale = value;

                    ArcCanvas.Width = width * _scale;
                    ArcCanvas.Height = height * _scale;

                    DrawArcBase();
                    DrawArcAuto();
                }
            }
        }

        bool _allowSteps = false;
        public bool AllowSteps {
            get => _allowSteps;
            set {
                _allowSteps = value;
                if (!_allowSteps) UsingSteps = false;
            }
        }

        bool _usingSteps = false;
        public virtual bool UsingSteps {
            get => _usingSteps;
            set {
                if (AllowSteps && Enabled && _usingSteps != value) {
                    _usingSteps = value;
                    DrawArcAuto();
                }
            }
        }

        Length _length = new Length();
        public Length Length {
            get => _length;
            set {
                _length = value;
                DrawArcSteps();
            }
        }

        protected string ValueString => _usingSteps? _length.ToString() : $"{((_centered && RawValue > 0)? "+" : "")}{RawValue}{Unit}";

        protected virtual void DrawArc(Path Arc, double value, bool overrideBase, string color = "ThemeAccentBrush") {
            if (Parent is Control control) ToolTip.SetIsOpen(control, false);

            double angle_starting = FillStart
                ? (_centered? angle_center: angle_start)
                : angle_start - Math.Abs(angle_end - angle_start) * value * 0.94;

            if (overrideBase) angle_starting = angle_start;
            
            double angle_point = angle_start - Math.Abs(angle_end - angle_start) * (_centered && !overrideBase? value : (1 - (1 - value) * 0.94));

            double angle = (angle_starting - angle_point) / Math.PI * 180;
            double angleLen = Math.Abs(angle);

            if (angleLen < 17.9) {
                double change = (.1 * Math.PI - angle_starting + angle_point) / 2;

                angle_starting += change;
                angle_point -= change;

                angle = 18;
            }

            double x_start = (radius * (Math.Cos(angle_starting) + 1) + strokeHalf) * _scale;
            double y_start = (radius * (-Math.Sin(angle_starting) + 1) + strokeHalf) * _scale;

            double x_end = (radius * (Math.Cos(angle_point) + 1) + strokeHalf) * _scale;
            double y_end = (radius * (-Math.Sin(angle_point) + 1) + strokeHalf) * _scale;

            int large = Convert.ToInt32(angle > 180);
            int direction = Convert.ToInt32(angle > 0);

            Arc.StrokeThickness = stroke * _scale;
            if (!overrideBase) {
                Arc.Stroke = App.GetResource<IBrush>(Enabled? color : "ThemeForegroundLowBrush");
                Display.Text = (Enabled || !DisplayDisabledText)? ValueString : DisabledText;
            }
            
            Arc.Data = Geometry.Parse(String.Format("M {0},{1} A {2},{2} {3} {4} {5} {6},{7}",
                x_start.ToString(),
                y_start.ToString(),
                (radius * _scale).ToString(),
                angle.ToString(),
                large,
                direction,
                x_end.ToString(),
                y_end.ToString()
            ));
        }

        protected void DrawArcBase() => DrawArc(ArcBase, 1, true);

        protected void DrawArcValue() {
            if (!_usingSteps) {
                bool erroring = ErrorValue != null? ErrorValue < RawValue : false;

                DrawArc(Arc, _value, false, erroring? "ErrorBrush" : "ThemeAccentBrush");
                if (Parent is Control control) ToolTip.SetTip(control, erroring && ErrorText != ""? ErrorText : null);
            }
        }

        protected void DrawArcSteps() {
            if (_usingSteps) {
                DrawArc(Arc, _length.Step / 9.0, false, "ThemeExtraBrush");
                if (Parent is Control control) ToolTip.SetTip(control, null);
            }
        }

        protected virtual void DrawArcAuto() {
            if (_usingSteps) DrawArcSteps();
            else DrawArcValue();
        }

        public Dial() {
            InitializeComponent();

            observable = Input.GetObservable(TextBox.TextProperty).Subscribe(Input_Changed);

            DrawArcBase();
        }

        protected void Unloaded(object sender, VisualTreeAttachmentEventArgs e) {
            Started = null;
            ValueChanged = null;
            StepChanged = null;
            ModeChanged = null;

            _length = null;

            observable.Dispose();
        }

        protected void LayoutChanged(object sender, EventArgs e) {
            DrawArcAuto();
            ((Grid)Content).LayoutUpdated -= LayoutChanged;
        }

        bool mouseHeld = false;
        double oldValue;
        int oldStep;
        double lastY;

        protected void MouseDown(object sender, PointerPressedEventArgs e) {
            PointerUpdateKind MouseButton = e.GetCurrentPoint(this).Properties.PointerUpdateKind;

            if (MouseButton == PointerUpdateKind.LeftButtonPressed && Enabled) {
                if (e.KeyModifiers.HasFlag(App.ControlKey)) {
                    Started?.Invoke();

                    if (_usingSteps) Length.Step = 5;
                    else RawValue = Default;
                    return;
                }
                
                if (e.ClickCount == 2) {
                    DisplayPressed(sender, e);
                    return;
                }

                mouseHeld = true;
                e.Pointer.Capture(ArcCanvas);

                lastY = e.GetPosition(ArcCanvas).Y;
                if (_usingSteps) oldStep = Length.Step;
                else oldValue = RawValue;

                Started?.Invoke();

                ArcCanvas.Cursor = new Cursor(StandardCursorType.SizeNorthSouth);
            }
        }

        protected void MouseUp(object sender, PointerReleasedEventArgs e) {
            PointerUpdateKind MouseButton = e.GetCurrentPoint(this).Properties.PointerUpdateKind;

            if (!Enabled) return;
            
            if (MouseButton == PointerUpdateKind.LeftButtonReleased) {
                mouseHeld = false;
                e.Pointer.Capture(null);

                if (_usingSteps) StepChanged?.Invoke(Length.Step, oldStep);
                else ValueChanged?.Invoke(this, RawValue, oldValue);

                ArcCanvas.Cursor = new Cursor(StandardCursorType.Hand);

            } else if (!mouseHeld && MouseButton == PointerUpdateKind.RightButtonReleased) {
                Started?.Invoke();
                
                UsingSteps = !UsingSteps;
                
                ModeChanged?.Invoke(UsingSteps, !UsingSteps);
            }
        }

        protected void MouseMove(object sender, PointerEventArgs e) {
            if (mouseHeld && Enabled) {
                double Y = e.GetPosition(ArcCanvas).Y;

                if (_usingSteps) {
                    if (Math.Abs(Y - lastY) >= 8) {
                        _length.Step -= (int)((Y - lastY) / 8);

                        StepChanged?.Invoke(_length.Step, null);

                        DrawArcSteps();
                        lastY = Y;
                    }
                } else {
                    Value += (lastY - Y) / 200;
                    lastY = Y;
                }
            }
        }

        Action Input_Update;

        protected void Input_Changed(string text) {
            if (text == null) return;
            if (text == "") return;

            Input_Update = () => { Input.Text = RawValue.ToString(); };

            if (double.TryParse(text, out double value)) {
                if (Minimum <= value && value <= Maximum) {
                    RawValue = value;
                    Input_Update = () => { Input.Foreground = App.GetResource<IBrush>("ThemeForegroundBrush"); };
                } else {
                    Input_Update = () => { Input.Foreground = App.GetResource<IBrush>("ErrorBrush"); };
                }

                Input_Update += () => {
                    if (value <= -1) text = $"-{text.Substring(1).TrimStart('0')}";
                    else if (value >= 1) text = text.TrimStart('0');
                    else if (!text.Contains('.') && text[0] != '-') text = "0";

                    if (Minimum >= 0) {
                        if (value < 0) text = "0";

                    } else {
                        int lower = - (int)Math.Pow(10, ((int)Minimum).ToString().Length - 1) + 1;
                        if (value < lower) text = lower.ToString();
                    }

                    int upper = (int)Math.Pow(10, ((int)Maximum).ToString().Length) - 1;
                    if (value > upper) text = upper.ToString();
                    
                    Input.Text = text;
                };
            }

            if (Minimum < 0 && text == "-") Input_Update = null;

            Dispatcher.UIThread.InvokeAsync(() => {
                Input_Update?.Invoke();
                Input_Update = null;
            });
        }

        protected void DisplayPressed(object sender, PointerPressedEventArgs e) {
            PointerUpdateKind MouseButton = e.GetCurrentPoint(this).Properties.PointerUpdateKind;

            if (MouseButton == PointerUpdateKind.LeftButtonPressed && e.ClickCount == 2 && !_usingSteps && Enabled) {
                Input.Text = RawValue.ToString();
                oldValue = RawValue;

                Input.SelectionStart = 0;
                Input.SelectionEnd = Input.Text.Length;
                Input.CaretIndex = Input.Text.Length;

                Input.Opacity = 1;
                Input.IsHitTestVisible = true;
                Input.Focus();

                Started?.Invoke();

                e.Handled = true;
            }
        }
        
        protected void Input_LostFocus(object sender, RoutedEventArgs e) {
            Input.Text = RawValue.ToString();

            Input.Opacity = 0;
            Input.IsHitTestVisible = false;

            ValueChanged?.Invoke(this, _raw, oldValue);
        }

        protected void Input_KeyDown(object sender, KeyEventArgs e) {
            if (App.Dragging) return;

            if (e.Key == Key.Return)
                this.Focus();

            e.Key = Key.None;
        }

        protected void Input_KeyUp(object sender, KeyEventArgs e) {
            if (App.Dragging) return;

            e.Key = Key.None;
        }

        protected void Input_MouseUp(object sender, PointerReleasedEventArgs e) => e.Handled = true;
    }
}
