using System;

namespace Apollo.Structures {
    public class TimeData {
        public Time Instance;

        int _free;
        public int Free {
            get => _free;
            set {
                if ((Instance?.Minimum?? 0) <= value && value <= (Instance?.Minimum?? int.MaxValue) && _free != value) {
                    _free = value;
                    Instance?.InvokeFreeChanged(_free);
                }
            }
        }

        bool _mode; // true uses Length
        public bool Mode {
            get => _mode;
            set {
                if (_mode != value) {
                    _mode = value;
                    Instance?.InvokeModeChanged(_mode);
                }
            }
        }

        public LengthData Length;
        
        public TimeData(bool mode = true, LengthData length = null, int free = 1000) {
            _free = free;
            _mode = mode;
            Length = length?? new LengthData();
        }

        public TimeData With(bool? mode = null, LengthData length = null, int? free = null)
            => new TimeData(mode?? _mode, length?? Length.Clone(), free?? _free);

        public TimeData Clone()
            => With();
        
        public Time Activate()
            => new Time(Clone());
    }

    public class Time {
        public readonly TimeData Data;

        public delegate void FreeChangedEventHandler(int free);
        public event FreeChangedEventHandler FreeChanged;

        public void InvokeFreeChanged(int free)
            => FreeChanged?.Invoke(free);
        
        public delegate void StepChangedEventHandler(Length step);
        public event StepChangedEventHandler StepChanged;

        void LengthChanged()
            => StepChanged?.Invoke(Length);

        public delegate void ModeChangedEventHandler(bool mode);
        public event ModeChangedEventHandler ModeChanged;

        public void InvokeModeChanged(bool mode)
            => ModeChanged?.Invoke(mode);

        int _min = 0;
        public int Minimum {
            get => _min;
            set {
                _min = value;

                if (Data.Free < _min)
                    Data.Free = _min;
            }
        }
        
        int _max = int.MaxValue;
        public int Maximum {
            get => _max;
            set {
                _max = value;
                
                if (Data.Free > _max)
                    Data.Free = _max;
            }
        }

        public readonly Length Length;

        public Time(TimeData data) {
            Data = data;

            Length = Data.Length.Activate();
            Length.Changed += LengthChanged;
        }

        public override bool Equals(object obj) {
            if (!(obj is Time)) return false;
            return this == (Time)obj;
        }

        public static bool operator ==(Time a, Time b) {
            if (a is null || b is null) return object.ReferenceEquals(a, b);
            return a.Data.Mode == b.Data.Mode && a.Data.Length == b.Data.Length && a.Data.Free == b.Data.Free;
        }
        public static bool operator !=(Time a, Time b) => !(a == b);

        public override int GetHashCode() => HashCode.Combine(Data.Mode, Data.Length, Data.Free);

        public static implicit operator int(Time x) => x.Data.Mode? (int)x.Length : x.Data.Free;
        public static implicit operator double(Time x) => x.Data.Mode? x.Length : x.Data.Free;
        
        public override string ToString() => Data.Mode? Data.Length.ToString() : $"{Data.Free}ms";

        public void Dispose() {
            FreeChanged = null;
            StepChanged = null;
            ModeChanged = null;

            Length.Dispose();
        }
    }
}