using System;

using Apollo.Core;

namespace Apollo.Structures {
    public class LengthData {
        public Length Instance;

        int _step;
        public int Step {
            get => _step;
            set {
                if (0 <= value && value <= 9 && _step != value) {
                    _step = value;
                    Instance?.InvokeChanged();
                }
            }
        }

        public LengthData(int step = 5)
            => Step = step;
        
        public LengthData Clone()
            => new LengthData(Step);
        
        public Length Activate()
            => new Length(Clone());
    }

    public class Length {
        public readonly LengthData Data;

        public delegate void ChangedEventHandler();
        public event ChangedEventHandler Changed;

        public void InvokeChanged()
            => Changed?.Invoke();

        public static string[] Steps = new []
            {"1/128", "1/64", "1/32", "1/16", "1/8", "1/4", "1/2", "1/1", "2/1", "4/1"};
        
        public double Value => Convert.ToDouble(Math.Pow(2, Data.Step - 7));

        public Length(LengthData data)
            => Data = data;

        public override bool Equals(object obj) {
            if (!(obj is Length)) return false;
            return this == (Length)obj;
        }

        public static bool operator ==(Length a, Length b) {
            if (a is null || b is null) return ReferenceEquals(a, b);
            return a.Data.Step == b.Data.Step;
        }
        public static bool operator !=(Length a, Length b) => !(a == b);

        public override int GetHashCode() => HashCode.Combine(Data.Step);

        public static implicit operator int(Length x) => (int)(x.Value * 240000 / Program.Project.BPM);
        public static implicit operator double(Length x) => x.Value * 240000 / Program.Project.BPM;

        public override string ToString() => Steps[Data.Step];

        public void Dispose() => Changed = null;
    }
}