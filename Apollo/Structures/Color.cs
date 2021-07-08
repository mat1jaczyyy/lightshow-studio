using System;
using System.Linq;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using AvaloniaColor = Avalonia.Media.Color;

using Apollo.Core;

namespace Apollo.Structures {
    public class Color {
        byte _r, _g, _b;

        bool IsValid(byte value) => 0 <= value && value <= 63;

        public byte Red {
            get => _r;
            set {
                if (IsValid(value)) _r = value;
            }
        }

        public byte Green {
            get => _g;
            set {
                if (IsValid(value)) _g = value;
            }
        }

        public byte Blue {
            get => _b;
            set {
                if (IsValid(value)) _b = value;
            }
        }

        public bool Lit {
            get => _r != 0 || _g != 0 || _b != 0;
        }

        public byte Max {
            get => new byte[] {_r, _g, _b}.Max();
        }

        public Color Clone() => new Color(_r, _g, _b);

        public Color(byte bright = 63) {
            if (!IsValid(bright)) bright = 63;
            _r = _g = _b = bright;
        }

        public Color(byte red, byte green, byte blue) {
            _r = _g = _b = 63;
            Red = red;
            Green = green;
            Blue = blue;
        }

        public void Mix(Color other, bool multiply) {
            if (multiply) {
                Red = (byte)((double)Red * other.Red / 63);
                Green = (byte)((double)Green * other.Green / 63);
                Blue = (byte)((double)Blue * other.Blue / 63);
            } else {
                Red = (byte)(63 - (double)(63 - Red) * (63 - other.Red) / 63);
                Green = (byte)(63 - (double)(63 - Green) * (63 - other.Green) / 63);
                Blue = (byte)(63 - (double)(63 - Blue) * (63 - other.Blue) / 63);
            }
        }

        public (double, double, double) ToHSV() {
            double r = Red / 63.0;
            double g = Green / 63.0;
            double b = Blue / 63.0;
            double[] colors = new [] {r, g, b};

            double min = colors.Min();
            double max = colors.Max();

            double hue = 0;
            if (min != max) {
                double diff = max - min;

                if (max == r) {
                    hue = (g - b) / diff;
                } else if (max == g) {
                    hue = (b - r) / diff + 2.0;
                } else if (max == b) {
                    hue = (r - g) / diff + 4.0;
                }
                if (hue < 0) hue += 6.0;
            }

            double saturation = 0;
            if (max != 0) saturation = 1 - (min / max);

            return (hue * 60, saturation, max);
        }

        public static Color FromHSV(double hue, double saturation, double value) {
            hue /= 60;

            int hi = Convert.ToInt32(Math.Floor(hue)) % 6;
            double f = hue - Math.Floor(hue);
            value *= 63;

            byte v = Convert.ToByte(value);
            byte p = Convert.ToByte(value * (1 - saturation));
            byte q = Convert.ToByte(value * (1 - f * saturation));
            byte t = Convert.ToByte(value * (1 - (1 - f) * saturation));

            if (hi == 0)      return new Color(v, t, p);
            else if (hi == 1) return new Color(q, v, p);
            else if (hi == 2) return new Color(p, v, t);
            else if (hi == 3) return new Color(p, q, v);
            else if (hi == 4) return new Color(t, p, v);
            else              return new Color(v, p, q);
        }

        public override bool Equals(object obj) {
            if (!(obj is Color)) return false;
            return this == (Color)obj;
        }

        public static bool operator ==(Color a, Color b) {
            if (a is null || b is null) return ReferenceEquals(a, b);
            return a.Red == b.Red && a.Green == b.Green && a.Blue == b.Blue;
        }
        public static bool operator !=(Color a, Color b) => !(a == b);

        public override int GetHashCode() => HashCode.Combine(Red, Green, Blue);

        public AvaloniaColor ToAvaloniaColor() => new AvaloniaColor(
            255,
            (byte)(_r * (255.0 / 63)),
            (byte)(_g * (255.0 / 63)),
            (byte)(_b * (255.0 / 63))
        );

        public SolidColorBrush ToBrush() => new SolidColorBrush(ToAvaloniaColor());
        public SolidColorBrush ToScreenBrush() {
            double h, s, val;
            (h, s, val) = ToHSV();

            s = Math.Pow(s, 1.8);
            val = Math.Pow(val, 1 / 4.5);

            byte fr, fg, fb;

            h /= 60;

            int hi = Convert.ToInt32(Math.Floor(h)) % 6;
            double f = h - Math.Floor(h);
            val *= 255;

            byte v = Convert.ToByte(val);
            byte p = Convert.ToByte(val * (1 - s));
            byte q = Convert.ToByte(val * (1 - f * s));
            byte t = Convert.ToByte(val * (1 - (1 - f) * s));

            if (hi == 0)      (fr, fg, fb) = (v, t, p);
            else if (hi == 1) (fr, fg, fb) = (q, v, p);
            else if (hi == 2) (fr, fg, fb) = (p, v, t);
            else if (hi == 3) (fr, fg, fb) = (p, q, v);
            else if (hi == 4) (fr, fg, fb) = (t, p, v);
            else              (fr, fg, fb) = (v, p, q);

            double max = new double[] {fr, fg, fb}.Max() / 255;

            AvaloniaColor bg = App.GetResource<AvaloniaColor>("ThemeForegroundLowColor");

            return new SolidColorBrush(new AvaloniaColor(
                255,
                (byte)(fr * max + bg.R * (1 - max)),
                (byte)(fg * max + bg.G * (1 - max)),
                (byte)(fb * max + bg.B * (1 - max))
            ));
        }

        public string ToHex() => $"{Red.ToString("X2")}{Green.ToString("X2")}{Blue.ToString("X2")}";

        public override string ToString() => $"({Red}, {Green}, {Blue})";
        
        public uint ToUInt32(){
            uint generatedColor = this.ToScreenBrush().Color.ToUint32();

            byte r = (byte)(generatedColor >> 16);
            byte g = (byte)(generatedColor >> 8);
            byte b = (byte)generatedColor;

            return (uint)(255 << 24 | b << 16 | g << 8 | r);
        }
    }
}