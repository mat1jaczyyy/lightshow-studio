using System.Collections.Generic;

using Apollo.DeviceViewers;
using Apollo.Elements;
using Apollo.Rendering;
using Apollo.Structures;

namespace Apollo.Devices {
    public class PreviewData: DeviceData {
        public PreviewViewer Viewer => Instance?.SpecificViewer<PreviewViewer>();

        public PreviewData() {}

        protected override DeviceData CloneSpecific()
            => new PreviewData();

        protected override Device ActivateSpecific(DeviceData data)
            => new Preview((PreviewData)data);
    }

    public class Preview: Device {
        public new PreviewData Data => (PreviewData)Data;

        public delegate void PreviewResetHandler();
        public static event PreviewResetHandler Clear;

        public static void InvokeClear() => Clear?.Invoke();

        Screen screen;

        void HandleClear() {
            screen.Clear();
            Data.Viewer?.Clear();
        }

        public Preview(PreviewData data = null): base(data?? new(), "preview") {
            screen = new Screen() { ScreenExit = PreviewExit };
            
            Clear += HandleClear;
        }
        
        public void PreviewExit(List<RawUpdate> n, Color[] snapshot) {
            if (Data.Viewer != null)
                n.ForEach(Data.Viewer.Render);
        }

        public override void MIDIProcess(List<Signal> n) {
            n.ForEach(screen.MIDIEnter);
            InvokeExit(n);
        }

        public override void Dispose() {
            if (Disposed) return;

            screen.Dispose();
            Clear -= HandleClear;

            base.Dispose();
        }
    }
}