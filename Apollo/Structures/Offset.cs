using Apollo.Enums;

namespace Apollo.Structures {
    public class OffsetData {
        public Offset Instance;

        int _x = 0;
        public int X {
            get => _x;
            set {
                if (-9 <= value && value <= 9 && _x != value) {
                    _x = value;
                    Instance?.InvokeChanged();
                }
            }
        }

        int _y = 0;
        public int Y {
            get => _y;
            set {
                if (-9 <= value && value <= 9 && _y != value) {
                    _y = value;
                    Instance?.InvokeChanged();
                }
            }
        }

        bool _absolute = false;
        public bool IsAbsolute {
            get => _absolute;
            set {
                if (_absolute != value) {
                    _absolute = value;
                    Instance?.InvokeChanged();
                }
            }
        }

        int _ax = 5;
        public int AbsoluteX {
            get => _ax;
            set {
                if (0 <= value && value <= 9 && _ax != value) {
                    _ax = value;
                    Instance?.InvokeChanged();
                }
            }
        }

        int _ay = 5;
        public int AbsoluteY {
            get => _ay;
            set {
                if (0 <= value && value <= 9 && _ay != value) {
                    _ay = value;
                    Instance?.InvokeChanged();
                }
            }
        }

        public OffsetData(int x = 0, int y = 0, bool absolute = false, int ax = 5, int ay = 5) {
            X = x;
            Y = y;
            IsAbsolute = absolute;
            AbsoluteX = ax;
            AbsoluteY = ay;
        }

        public OffsetData Clone()
            => new OffsetData(X, Y, IsAbsolute, AbsoluteX, AbsoluteY);

        public Offset Activate()
            => new Offset(Clone());
    }

    public class Offset {
        public readonly OffsetData Data;

        public delegate void ChangedEventHandler(Offset sender);
        public event ChangedEventHandler Changed;

        public void InvokeChanged() => Changed?.Invoke(this);

        public Offset(OffsetData data)
            => Data = data;

        public static bool Validate(int x, int y, GridType gridMode, bool wrap, out int result) {
            if (wrap) {
                x = gridMode.Wrap(x);
                y = gridMode.Wrap(y);
            }

            result = y * 10 + x;

            if (gridMode == GridType.Full) {
                if (0 <= x && x <= 9 && 0 <= y && y <= 9)
                    return true;
                
                if (y == -1 && 4 <= x && x <= 5) {
                    result = 100;
                    return true;
                }

            } else if (gridMode == GridType.Square)
                if (1 <= x && x <= 8 && 1 <= y && y <= 8)
                    return true;
             
            return false;
        }

        public bool Apply(int index, GridType gridMode, bool wrap, out int x, out int y, out int result) {
            if (Data.IsAbsolute) {
                x = Data.AbsoluteX;
                y = Data.AbsoluteY;
                return Validate(x, y, gridMode, wrap, out result);
            }

            x = index % 10;
            y = index / 10;

            if (gridMode == GridType.Square && (x == 0 || x == 9 || y == 0 || y == 9)) {
                result = 0;
                return false;
            }

            x += Data.X;
            y += Data.Y;

            return Validate(x, y, gridMode, wrap, out result);
        }

        public void Dispose() => Changed = null;
    }
}