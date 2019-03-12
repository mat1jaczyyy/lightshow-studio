using System;
using System.Collections.Generic;

namespace Apollo.Components {
    public class Pixel {
        public Action<Signal> MIDIExit = null;
        
        private SortedList<int, Signal> _signals = new SortedList<int, Signal>();
        private int? _highest = null;
        private object locker = new object();

        public Pixel() {}

        public void MIDIEnter(Signal n) {
            lock (locker) {
                int layer = -n.Layer;

                if (_signals.ContainsKey(layer)) {
                    if (n.Color.Lit) {
                        _signals[layer] = n.Clone();
                        
                        if (layer == _highest)
                            MIDIExit?.Invoke(n.Clone());

                    } else {
                        _signals.Remove(layer);

                        if (layer == _highest) {
                            if (_signals.Count == 0) {
                                _highest = null;

                                MIDIExit?.Invoke(n.Clone());

                            } else {
                                _highest = _signals.Keys[0];

                                MIDIExit?.Invoke(_signals[(int)_highest].Clone());
                            }
                        }
                    }
                
                } else {
                    if (n.Color.Lit) {
                        _signals[layer] = n.Clone();

                        if (!_highest.HasValue || layer < _highest) {
                            _highest = layer;

                            MIDIExit?.Invoke(n.Clone());
                        }
                    }
                }
            }
        }
    }
}