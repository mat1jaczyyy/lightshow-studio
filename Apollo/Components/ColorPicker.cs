using System;
using System.Collections.Generic;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using AvaloniaColor = Avalonia.Media.Color;
using LinearGradientBrush = Avalonia.Media.LinearGradientBrush;
using GradientStop = Avalonia.Media.GradientStop;
using IBrush = Avalonia.Media.IBrush;
using Avalonia.Threading;
using Avalonia.VisualTree;

using Apollo.Core;
using Apollo.Enums;
using Apollo.Structures;

namespace Apollo.Components {
    public class ColorPicker: UserControl {
        void InitializeComponent() {
            AvaloniaXamlLoader.Load(this);

            Preview = this.Get<Ellipse>("Color");

            MainCanvas = this.Get<Canvas>("MainCanvas");
            HueCanvas = this.Get<Canvas>("HueCanvas");

            MainThumb = this.Get<Canvas>("MainThumb");
            HueThumb = this.Get<Canvas>("HueThumb");

            MainColor = ((LinearGradientBrush)this.Get<Grid>("MainColor").Background).GradientStops[1];
            
            Hex = this.Get<TextBox>("Hex");
            Red = this.Get<TextBox>("Red");
            Green = this.Get<TextBox>("Green");
            Blue = this.Get<TextBox>("Blue");
            
            TopBar = this.Get<Rectangle>("TopBar");
            BottomBar = this.Get<Rectangle>("BottomBar");
            LeftBar = this.Get<Rectangle>("LeftBar");
            RightBar = this.Get<Rectangle>("RightBar");
        }

        enum MouseLock {
            Horizontal,
            Vertical,
            None,
            Ready
        }
        
        HashSet<IDisposable> observables = new();
        
        public delegate void ColorChangedEventHandler(Color value, Color old);
        public event ColorChangedEventHandler ColorChanged;

        Color _color = new Color();
        public Color Color {
            get => _color;
            private set {
                _color = value;
                ColorChanged?.Invoke(_color, null);
            }
        }

        public void SetColor(Color color) {
            if (_color != color) {
                _color = color;
                Preview.Fill = Color.ToScreenBrush();
                InitCanvas();
            }
        }

        Ellipse Preview;
        Canvas MainCanvas, HueCanvas, MainThumb, HueThumb;
        GradientStop MainColor;
        TextBox Hex, Red, Green, Blue;
        Rectangle TopBar, BottomBar, LeftBar, RightBar;

        void Update_ColorDisplayFormat() {
            Red.IsVisible = Green.IsVisible = Blue.IsVisible = Preferences.ColorDisplayFormat == ColorDisplayType.RGB;
            Hex.IsVisible = Preferences.ColorDisplayFormat == ColorDisplayType.Hex;

            ((Window)this.GetVisualRoot())?.Focus();
        }

        bool hexValidation, rgbValidation;
        object mouseHeld;
        double offsetX, offsetY;
        MouseLock mouseLock = MouseLock.None;
        Color oldColor;

        public ColorPicker() {
            InitializeComponent();

            observables.Add(MainCanvas.GetObservable(Visual.BoundsProperty).Subscribe(Bounds_Updated));
            observables.Add(HueCanvas.GetObservable(Visual.BoundsProperty).Subscribe(Bounds_Updated));

            hexValidation = true;
            observables.Add(Hex.GetObservable(TextBox.TextProperty).Subscribe(Hex_Changed));
            
            rgbValidation = true;
            observables.Add(Red.GetObservable(TextBox.TextProperty).Subscribe((string text) => RGB_Changed(text, Red)));
            observables.Add(Green.GetObservable(TextBox.TextProperty).Subscribe((string text) => RGB_Changed(text, Green)));
            observables.Add(Blue.GetObservable(TextBox.TextProperty).Subscribe((string text) => RGB_Changed(text, Blue)));
            
            Preferences.ColorDisplayFormatChanged += Update_ColorDisplayFormat;
            Update_ColorDisplayFormat();
        }

        void Unloaded(object sender, VisualTreeAttachmentEventArgs e) {
            ColorChanged = null;
            
            Preferences.ColorDisplayFormatChanged -= Update_ColorDisplayFormat;

            foreach (IDisposable observable in observables)
                observable.Dispose();
        }

        public void Bounds_Updated(Rect bounds) {
            if (!bounds.IsEmpty) InitCanvas();
        }

        void InitCanvas() {
            double hueHeight = HueCanvas.Bounds.Height;
            double mainWidth = MainCanvas.Bounds.Width;
            double mainHeight = MainCanvas.Bounds.Height;

            if (hueHeight == 0 || mainWidth == 0 || mainHeight == 0) return;

            (double hue, double saturation, double value) = Color.ToHSV();

            Canvas.SetTop(HueThumb, hue * hueHeight / 360);
            Canvas.SetLeft(MainThumb, saturation * mainWidth);
            Canvas.SetTop(MainThumb, (1 - value) * mainHeight);

            UpdateCanvas();
            Hex.Text = Color.ToHex();
            
            Red.Text = Color.Red.ToString();
            Green.Text = Color.Green.ToString();
            Blue.Text = Color.Blue.ToString();
        }

        void UpdateText() {
            hexValidation = false;
            rgbValidation = false;
            
            Hex.Text = Color.ToHex();
            
            Red.Text = Color.Red.ToString();
            Green.Text = Color.Green.ToString();
            Blue.Text = Color.Blue.ToString();

            hexValidation = true;
            rgbValidation = true;
        }

        void UpdateColor() {
            double hue = Canvas.GetTop(HueThumb) * 360 / HueCanvas.Bounds.Height;
            double saturation = Canvas.GetLeft(MainThumb) / MainCanvas.Bounds.Width;
            double value = (1 - (Canvas.GetTop(MainThumb) / MainCanvas.Bounds.Height));

            Color = Color.FromHSV(hue, saturation, value);

            Preview.Fill = Color.ToScreenBrush();

            UpdateText();
            Hex.Foreground = App.GetResource<IBrush>("ThemeForegroundBrush");
        }

        void UpdateCanvas() {
            double hue = Canvas.GetTop(HueThumb) * 6 / HueCanvas.Bounds.Height;

            int hi = Convert.ToInt32(Math.Floor(hue)) % 6;
            double f = hue - Math.Floor(hue);

            byte v = 255;
            byte p = 0;
            byte q = Convert.ToByte(255 * (1 - f));
            byte t = Convert.ToByte(255 * f);

            if (hi == 0)      MainColor.Color = new AvaloniaColor(255, v, t, p);
            else if (hi == 1) MainColor.Color = new AvaloniaColor(255, q, v, p);
            else if (hi == 2) MainColor.Color = new AvaloniaColor(255, p, v, t);
            else if (hi == 3) MainColor.Color = new AvaloniaColor(255, p, q, v);
            else if (hi == 4) MainColor.Color = new AvaloniaColor(255, t, p, v);
            else              MainColor.Color = new AvaloniaColor(255, v, p, q);
        }

        void Main_Move(object sender, PointerEventArgs e) {
            Vector position = e.GetPosition(MainThumb);
            position = position.WithX(position.X - offsetX)
                               .WithY(position.Y - offsetY);

            double x = Canvas.GetLeft(MainThumb);
            double y = Canvas.GetTop(MainThumb);
            
            if (mouseLock == MouseLock.Ready)
                if (Math.Abs(position.X) > Math.Abs(position.Y)) {
                    mouseLock = MouseLock.Horizontal;
                    LeftBar.IsVisible = RightBar.IsVisible = true;
                } else {
                    mouseLock = MouseLock.Vertical;
                    TopBar.IsVisible = BottomBar.IsVisible = true;
                }

            if (mouseLock == MouseLock.None || mouseLock == MouseLock.Horizontal) {
                x += position.X;
                x = (x < 0)? 0 : x;
                x = (x > MainCanvas.Bounds.Width)? MainCanvas.Bounds.Width : x;
            }

            if (mouseLock == MouseLock.None || mouseLock == MouseLock.Vertical) {
                y += position.Y;
                y = (y < 0)? 0 : y;
                y = (y > MainCanvas.Bounds.Height)? MainCanvas.Bounds.Height : y;
            }
            
            Canvas.SetLeft(MainThumb, x);
            Canvas.SetTop(MainThumb, y);
            
            UpdateColor();
            UpdateThumbAxes();
        }

        void Hue_Move(object sender, PointerEventArgs e) {
            Vector position = e.GetPosition(HueThumb);
            position = position.WithX(position.X - offsetX)
                               .WithY(position.Y - offsetY);

            double y = Canvas.GetTop(HueThumb) + position.Y;
            y = (y < 0)? 0 : y;
            y = y > HueCanvas.Bounds.Height? HueCanvas.Bounds.Height : y;

            Canvas.SetTop(HueThumb, y);

            UpdateColor();
            UpdateCanvas();
        }

        void Start(object sender, PointerEventArgs e, bool move, bool hue) {
            Control source = (Control)sender;

            mouseHeld = sender;
            e.Pointer.Capture(source);

            oldColor = Color.Clone();

            if (move) {
                offsetX = offsetY = 0;

                if (hue) Hue_Move(sender, e);
                else Main_Move(sender, e);

            } else {
                Vector position = e.GetPosition(source);
                offsetX = position.X;
                offsetY = position.Y;
            }
        }

        void End(PointerEventArgs e,bool hue) {
            mouseHeld = null;
            e.Pointer.Capture(null);
            
            if (!hue) {
                mouseLock = MouseLock.None;
                TopBar.IsVisible = BottomBar.IsVisible = LeftBar.IsVisible = RightBar.IsVisible = false;
            }

            if (oldColor != Color)
                ColorChanged?.Invoke(Color, oldColor);
        }
        
        void Canvas_MouseDown(object sender, PointerPressedEventArgs e) {
            PointerUpdateKind MouseButton = e.GetCurrentPoint(this).Properties.PointerUpdateKind;

            if (MouseButton == PointerUpdateKind.LeftButtonPressed) Start(sender, e, true, sender == HueCanvas);
        }

        void Canvas_MouseUp(object sender, PointerReleasedEventArgs e) {
            PointerUpdateKind MouseButton = e.GetCurrentPoint(this).Properties.PointerUpdateKind;

            if (MouseButton == PointerUpdateKind.LeftButtonReleased) End(e, sender == HueCanvas);
        }

        void MainThumb_MouseDown(object sender, PointerPressedEventArgs e) {
            PointerUpdateKind MouseButton = e.GetCurrentPoint(this).Properties.PointerUpdateKind;

            if (mouseHeld != null) return;

            if (MouseButton == PointerUpdateKind.RightButtonPressed)
                mouseLock = MouseLock.Ready;
                
            if (MouseButton == PointerUpdateKind.LeftButtonPressed || MouseButton == PointerUpdateKind.RightButtonPressed)
                Start(sender, e, false, false);
                
            e.Handled = true;
        }

        void MainThumb_MouseUp(object sender, PointerReleasedEventArgs e) {
            PointerUpdateKind MouseButton = e.GetCurrentPoint(this).Properties.PointerUpdateKind;
            
            if (mouseHeld == null) return;

            if (MouseButton == PointerUpdateKind.LeftButtonReleased || MouseButton == PointerUpdateKind.RightButtonReleased)
                End(e, false);
            
            e.Handled = true;
        }

        void Main_MouseMove(object sender, PointerEventArgs e) {
            if (mouseHeld == sender) Main_Move(sender, e);
        }

        void HueThumb_MouseDown(object sender, PointerPressedEventArgs e) {
            PointerUpdateKind MouseButton = e.GetCurrentPoint(this).Properties.PointerUpdateKind;

            if (mouseHeld != null) return;

            if (MouseButton == PointerUpdateKind.LeftButtonPressed)
                Start(sender, e, false, true);
                
            e.Handled = true;
        }

        void HueThumb_MouseUp(object sender, PointerReleasedEventArgs e) {
            PointerUpdateKind MouseButton = e.GetCurrentPoint(this).Properties.PointerUpdateKind;
            
            if (mouseHeld == null) return;

            if (MouseButton == PointerUpdateKind.LeftButtonReleased)
                End(e, true);
            
            e.Handled = true;
        }

        void Hue_MouseMove(object sender, PointerEventArgs e) {
            if (mouseHeld == sender) Hue_Move(sender, e);
        }

        bool Hex_Dirty, RGB_Dirty = false;

        Action HexAction(string text) {
            Action update = () => Hex.Foreground = App.GetResource<IBrush>("ThemeForegroundBrush");

            foreach (char i in text)
                if (!"0123456789ABCDEF".Contains(i))
                    return update + (() => UpdateText());

            if (text.Length > 6) return update + (() => UpdateText());
            if (text.Length < 6) return () => Hex.Foreground = App.GetResource<IBrush>("ErrorBrush");

            int r = Convert.ToInt32(text.Substring(0, 2), 16);
            int g = Convert.ToInt32(text.Substring(2, 2), 16);
            int b = Convert.ToInt32(text.Substring(4, 2), 16);

            r = (r > 63)? 63 : r;
            g = (g > 63)? 63 : g;
            b = (b > 63)? 63 : b;

            if (!Hex_Dirty) {
                oldColor = Color.Clone();
                Hex_Dirty = true;
            }

            return update + (() => { 
                Color = new Color((byte)r, (byte)g, (byte)b);

                Preview.Fill = Color.ToScreenBrush();
                
                hexValidation = rgbValidation = false;
                InitCanvas();
                hexValidation = rgbValidation = true;
            });
        }
        
        Action RGBAction(string text, TextBox sender) {
            Action update = () => {};
            
            foreach (char i in text)
                if (!"0123456789".Contains(i))
                    return update + (() => UpdateText());
            
            int val;
            
            if (text == "") val = 0;
            else val = Convert.ToInt32(text);
            
            val = (val > 63)? 63 : val;  
            
            if (!RGB_Dirty) {
                oldColor = Color.Clone();
                RGB_Dirty = true;
            }
            
            Color newColor = _color.Clone();
            
            if (sender == Red) newColor.Red = (byte)val;
            else if (sender == Green) newColor.Green = (byte)val;
            else if (sender == Blue) newColor.Blue = (byte)val;
            
            return update + (() => {
                Color = newColor;
                
                Preview.Fill = Color.ToScreenBrush();
                
                hexValidation = rgbValidation = false;
                InitCanvas();
                hexValidation = rgbValidation = true;
            });
        }
        
        void RGB_Changed(string text, TextBox sender) {
            if (!rgbValidation) return;
            
            if (text == "" || text == null) return;
            
            Dispatcher.UIThread.InvokeAsync(RGBAction(text, sender));
        }
        
        void RGB_MouseUp(object sender, PointerReleasedEventArgs e) {
            if (sender is TextBox textBox) textBox.Focus();
        }

        void RGB_Unfocus(object sender, RoutedEventArgs e){
            if (sender is TextBox textBox && textBox.Text == "")
                Dispatcher.UIThread.InvokeAsync(RGBAction("0", textBox));
                
            if (oldColor != Color)
                ColorChanged?.Invoke(Color, oldColor);
                
            RGB_Dirty = false;
        }
        
        void Hex_Changed(string text) {
            if (!hexValidation) return;
            
            if (text == null) return;

            Dispatcher.UIThread.InvokeAsync(HexAction(text.ToUpper()));
        }

        void Hex_MouseUp(object sender, PointerReleasedEventArgs e) => Hex.Focus();
        
        void Text_KeyDown(object sender, KeyEventArgs e) {
            if (App.Dragging) return;

            if (e.Key == Key.Return)
                this.Focus();
            
            if (e.Key == Key.Tab && sender is TextBox textBox) {
                if (textBox == Red) ((e.KeyModifiers == KeyModifiers.Shift)? Blue : Green).Focus();
                if (textBox == Green) ((e.KeyModifiers == KeyModifiers.Shift)? Red : Blue).Focus();
                if (textBox == Blue) ((e.KeyModifiers == KeyModifiers.Shift)? Green : Red).Focus();
            }
            
            e.Key = Key.None;
        }
        
        void Hex_KeyUp(object sender, KeyEventArgs e) {
            if (App.Dragging) return;

            e.Key = Key.None;
        }

        void Hex_Unfocus(object sender, RoutedEventArgs e) {
            if (oldColor != Color)
                ColorChanged?.Invoke(Color, oldColor);

            Hex_Dirty = false;
        }
    
        void UpdateThumbAxes() {
            double ThumbLeft = MainThumb.GetValue(Canvas.LeftProperty);
            double ThumbTop = MainThumb.GetValue(Canvas.TopProperty);
            
            double width = MainCanvas.Bounds.Width;
            double height = MainCanvas.Bounds.Height;
            
            TopBar.SetValue(Canvas.LeftProperty, ThumbLeft - 0.5);
            TopBar.SetValue(Canvas.TopProperty, ThumbTop - 4 - height);
            TopBar.SetValue(HeightProperty, height);
            
            BottomBar.SetValue(Canvas.LeftProperty, ThumbLeft - 0.5);
            BottomBar.SetValue(Canvas.TopProperty, ThumbTop + 4);
            BottomBar.SetValue(HeightProperty, height);
            
            LeftBar.SetValue(Canvas.LeftProperty, ThumbLeft - 4 - width);
            LeftBar.SetValue(Canvas.TopProperty, ThumbTop - 0.5);
            LeftBar.SetValue(WidthProperty, width);
            
            RightBar.SetValue(Canvas.LeftProperty, ThumbLeft + 4);
            RightBar.SetValue(Canvas.TopProperty, ThumbTop - 0.5);
            RightBar.SetValue(WidthProperty, width);
        }
    }
}
