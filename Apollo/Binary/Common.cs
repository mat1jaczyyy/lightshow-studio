using System;

using Apollo.Core;
using Apollo.Devices;
using Apollo.Elements;
using Apollo.Helpers;
using Apollo.Structures;
using Apollo.Undo;

namespace Apollo.Binary {
    public static class Common {
        public const int version = 32;

        public static readonly Type[] id = new[] {
            typeof(Preferences),
            typeof(Copyable),

            typeof(Project),
            typeof(Track),
            typeof(ChainData),
            typeof(DeviceData),
            typeof(Launchpad),

            typeof(Group),
            typeof(CopyData),
            typeof(DelayData),
            typeof(FadeData),
            typeof(FlipData),
            typeof(HoldData),
            typeof(KeyFilterData),
            typeof(LayerData),
            typeof(MoveData),
            typeof(Multi),
            typeof(OutputData),
            typeof(MacroFilterData),
            typeof(Switch),
            typeof(PaintData),
            typeof(Pattern),
            typeof(PreviewData),
            typeof(Rotate),
            typeof(Tone),

            typeof(Color),
            typeof(Frame),
            typeof(LengthData),
            typeof(OffsetData),
            typeof(TimeData),

            typeof(ChokeData),
            typeof(ColorFilterData),
            typeof(ClearData),
            typeof(LayerFilterData),
            typeof(LoopData),
            typeof(RefreshData),
            typeof(UndoManager)
        };
    }
}