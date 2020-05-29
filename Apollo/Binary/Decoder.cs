using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

using Apollo.Components;
using Apollo.Core;
using Apollo.Devices;
using Apollo.Elements;
using Apollo.Enums;
using Apollo.Helpers;
using Apollo.Selection;
using Apollo.Structures;
using Apollo.Undo;
using Apollo.Windows;

namespace Apollo.Binary {
    public static class Decoder {
        static bool DecodeHeader(BinaryReader reader) => reader.ReadBytes(4).Select(i => (char)i).SequenceEqual(new char[] {'A', 'P', 'O', 'L'});

        static Type DecodeID(BinaryReader reader) => Common.id[reader.ReadByte()];

        static void Decode(Stream input, Action<BinaryReader, int> decoder) {
            using (BinaryReader reader = new BinaryReader(input)) {
                if (!DecodeHeader(reader)) throw new InvalidDataException();

                int version = reader.ReadInt32();

                if (version > Common.version) throw new InvalidDataException();

                if (typeof(Preferences) != DecodeID(reader)) throw new InvalidDataException();

                decoder?.Invoke(reader, version);
            }
        }

        public static void DecodeConfig(Stream input) => Decode(input, (reader, version) => {
            Preferences.AlwaysOnTop = reader.ReadBoolean();
            Preferences.CenterTrackContents = reader.ReadBoolean();

            if (version >= 24) {
                Preferences.ChainSignalIndicators = reader.ReadBoolean();
                Preferences.DeviceSignalIndicators = reader.ReadBoolean();
            } else if (version >= 23) {
                Preferences.ChainSignalIndicators = Preferences.DeviceSignalIndicators = reader.ReadBoolean();
            }
            
            if (version >= 28) {
                Preferences.ColorDisplayFormat = (ColorDisplayType)reader.ReadInt32();
            }

            if (version >= 9) {
                Preferences.LaunchpadStyle = (LaunchpadStyles)reader.ReadInt32();
            }

            if (version >= 14) {
                Preferences.LaunchpadGridRotation = reader.ReadInt32() > 0;
            }

            if (version >= 24) {
                LaunchpadModels model = (LaunchpadModels)reader.ReadInt32();

                if (version <= 28 && model >= LaunchpadModels.ProMK3)
                    model++;

                Preferences.LaunchpadModel = model;
            }

            Preferences.AutoCreateKeyFilter = reader.ReadBoolean();
            Preferences.AutoCreateMacroFilter = reader.ReadBoolean();

            if (version >= 11) {
                Preferences.AutoCreatePattern = reader.ReadBoolean();
            }

            Preferences.FadeSmoothness = reader.ReadDouble();
            Preferences.CopyPreviousFrame = reader.ReadBoolean();

            if (version >= 7) {
                Preferences.CaptureLaunchpad = reader.ReadBoolean();
            }
            
            Preferences.EnableGestures = reader.ReadBoolean();

            if (version >= 7) {
                Preferences.PaletteName = reader.ReadString();
                Preferences.CustomPalette = new Palette(Enumerable.Range(0, 128).Select(i => Decode<Color>(reader, version)).ToArray());
                Preferences.ImportPalette = (Palettes)reader.ReadInt32();

                Preferences.Theme = (ThemeType)reader.ReadInt32();
            }

            if (version >= 10) {
                Preferences.Backup = reader.ReadBoolean();
                Preferences.Autosave = reader.ReadBoolean();
            }

            if (version >= 12) {
                Preferences.UndoLimit = reader.ReadBoolean();
            }

            if (version <= 0) {
                Preferences.DiscordPresence = true;
                reader.ReadBoolean();
            } else {
                Preferences.DiscordPresence = reader.ReadBoolean();
            }
            
            Preferences.DiscordFilename = reader.ReadBoolean();

            ColorHistory.Set(
                Enumerable.Range(0, reader.ReadInt32()).Select(i => Decode<Color>(reader, version)).ToList()
            );

            if (version >= 2)
                MIDI.Devices = Enumerable.Range(0, reader.ReadInt32()).Select(i => Decode<Launchpad>(reader, version)).ToList();

            if (version >= 15) {
                Preferences.Recents = Enumerable.Range(0, reader.ReadInt32()).Select(i => reader.ReadString()).ToList();
            }

            if (version >= 25) {
                Preferences.VirtualLaunchpads = Enumerable.Range(0, reader.ReadInt32()).Select(i => reader.ReadInt32()).ToList();
            }

            if (15 <= version && version <= 22) {
                reader.ReadString();
                reader.ReadString();
            }

            if (version >= 23) {
                Preferences.Crashed = reader.ReadBoolean();
                Preferences.CrashPath = reader.ReadString();
            }

            if (version >= 16)
                Preferences.CheckForUpdates = reader.ReadBoolean();

            if (17 <= version && version <= 28)
                Preferences.BaseTime = reader.ReadInt64();
        });
        
        public static void DecodeStats(Stream input) => Decode(input, (reader, version) => {
            if (version >= 29)
                Preferences.BaseTime = reader.ReadInt64();
        });

        public static async Task<T> Decode<T>(Stream input) {
            int version;

            using (BinaryReader reader = new BinaryReader(input)) {
                if (!DecodeHeader(reader)) throw new InvalidDataException();

                version = reader.ReadInt32();

                if (version > Common.version && await MessageWindow.Create(
                    "The content you're attempting to read was created with a newer version of\n" +
                    "Apollo Studio, which might result in it not loading correctly.\n\n" +
                    "Are you sure you want to proceed?",
                    new string[] { "Yes", "No" }, null
                ) == "No") throw new InvalidDataException();

                if (version < 30) return Decode<T>(reader, version);
                else {
                    byte[] compressed = reader.ReadBytes((int)(input.Length - input.Position));

                    try {
                        using (MemoryStream zipFile = new MemoryStream(compressed))
                            using (ZipArchive archive = new ZipArchive(zipFile))
                                using (Stream apoldata = archive.GetEntry(Common.zipKey).Open())
                                    using (BinaryReader uncompressed = new BinaryReader(apoldata))
                                        return Decode<T>(uncompressed, version);
                    } catch {
                        throw new InvalidDataException();
                    }
                }
            }
        }

        public static T DecodeAnything<T>(BinaryReader reader, int version) {
            Type type = typeof(T);
            bool nullable = Nullable.GetUnderlyingType(type) != null;
            Type returnType = nullable? Nullable.GetUnderlyingType(type) : type;
            
            MethodInfo decoder = typeof(BinaryReader).GetMethods()
                .Where(i => i.Name != "Read" && i.Name.StartsWith("Read") && !i.GetParameters().Any())
                .FirstOrDefault(i => i.ReturnType == returnType);
                
            if (nullable)
                return reader.ReadBoolean()
                    ? (T)decoder.Invoke(reader, new object[0])
                    : default(T);
            
            else if (decoder != null)
                return (T)decoder.Invoke(reader, new object[0]);
            
            else if (type.IsEnum)
                return (T)(object)reader.ReadInt32();
            
            else if (type != typeof(string) && typeof(IEnumerable).IsAssignableFrom(type)) { // Lists and Arrays
                Type EnumerableType = type.IsArray
                    ? type.GetElementType()
                    : type.GetGenericArguments()[0];
                
                dynamic decodedList = typeof(Decoder).GetMethod("DecodeEnumerable", BindingFlags.NonPublic | BindingFlags.Static)
                    .MakeGenericMethod(EnumerableType)
                    .Invoke(null, new object[] { reader, version });
                
                return type.IsArray
                    ? decodedList.ToArray()
                    : decodedList;

            } else return Decode<T>(reader, version);
        }
        
        static IEnumerable<T> DecodeEnumerable<T>(BinaryReader reader, int version)
            => Enumerable.Range(0, reader.ReadInt32())
                .Select(i => DecodeAnything<T>(reader, version)).ToList();
        
        public static T Decode<T>(BinaryReader reader, int version)
            => (T)(typeof(T) == typeof(ISelect)
                ? Decode(reader, version)
                : Decode(reader, version, typeof(T))
            );
        
        static dynamic Decode(BinaryReader reader, int version, Type ensure = null) { 
            Type t = DecodeID(reader);
            if (ensure != null && ensure != t) throw new InvalidDataException();

            if (t == typeof(Copyable))
                return new Copyable() {
                    Contents = Enumerable.Range(0, reader.ReadInt32()).Select(i => Decode<ISelect>(reader, version)).ToList()
                };

            else if (t == typeof(Project)) {
                int bpm = reader.ReadInt32();
                int[] macros = (version >= 25)? Enumerable.Range(0, 4).Select(i => reader.ReadInt32()).ToArray() : new int[4] {reader.ReadInt32(), 1, 1, 1};
                List<Track> tracks = Enumerable.Range(0, reader.ReadInt32()).Select(i => Decode<Track>(reader, version)).ToList();

                string author = "";
                long time = 0;
                long started = 0;

                if (version >= 17) {
                    author = reader.ReadString();
                    time = reader.ReadInt64();
                    started = reader.ReadInt64();
                }

                UndoManager undo = null;
                if (version >= 30) {
                    undo = Decode<UndoManager>(reader, version);
                }

                return new Project(bpm, macros, tracks, author, time, started, undo);
            
            } else if (t == typeof(Track)) {
                Chain chain = Decode<Chain>(reader, version);
                Launchpad lp = Decode<Launchpad>(reader, version);
                string name = reader.ReadString();

                bool enabled = true;
                if (version >= 8) {
                    enabled = reader.ReadBoolean();
                }

                return new Track(chain, lp, name) {
                    Enabled = enabled
                };

            } else if (t == typeof(Chain)) {
                List<Device> devices = Enumerable.Range(0, reader.ReadInt32()).Select(i => Decode<Device>(reader, version)).ToList();
                string name = reader.ReadString();

                bool enabled = true;
                if (version >= 6) {
                    enabled = reader.ReadBoolean();
                }

                bool[] filter = null;
                if (version >= 29) {
                    filter = Enumerable.Range(0, 101).Select(i => reader.ReadBoolean()).ToArray();
                }

                return new Chain(devices, name, filter) {
                    Enabled = enabled
                };

            } else if (t == typeof(Device)) {
                bool collapsed = false;
                if (version >= 5) {
                    collapsed = reader.ReadBoolean();
                }

                bool enabled = true;
                if (version >= 5) {
                    enabled = reader.ReadBoolean();
                }

                Device ret = (Device)Decode(reader, version); // This needs to be a cast!
                ret.Collapsed = collapsed;
                ret.Enabled = enabled;

                return ret;
            
            } else if (t == typeof(Launchpad)) {
                string name = reader.ReadString();
                if (name == "") return MIDI.NoOutput;

                InputType format = InputType.DrumRack;
                if (version >= 2) format = (InputType)reader.ReadInt32();

                RotationType rotation = RotationType.D0;
                if (version >= 9) rotation = (RotationType)reader.ReadInt32();

                foreach (Launchpad lp in MIDI.Devices)
                    if (lp.Name == name) {
                        if (lp.GetType() == typeof(Launchpad)) {
                            lp.InputFormat = format;
                            lp.Rotation = rotation;
                        }
                        return lp;
                    }
                
                Launchpad ret;
                if (name.Contains("Virtual Launchpad ")) ret = new VirtualLaunchpad(name, Convert.ToInt32(name.Substring(18)));
                else if (name.Contains("Ableton Connector ")) ret = new AbletonLaunchpad(name);
                else ret = new Launchpad(name, format, rotation);

                MIDI.Devices.Add(ret);

                return ret;

            } else if (t == typeof(Group))
                return new Group(
                    Enumerable.Range(0, reader.ReadInt32()).Select(i => Decode<Chain>(reader, version)).ToList(),
                    reader.ReadBoolean()? (int?)reader.ReadInt32() : null
                );

            else if (t == typeof(Choke))
                return new Choke(
                    reader.ReadInt32(),
                    Decode<Chain>(reader, version)
                );

            else if (t == typeof(Clear))
                return new Clear(
                    (ClearType)reader.ReadInt32()
                );

            else if (t == typeof(ColorFilter))
                return new ColorFilter(
                    reader.ReadDouble(),
                    reader.ReadDouble(),
                    reader.ReadDouble(),
                    reader.ReadDouble(),
                    reader.ReadDouble(),
                    reader.ReadDouble()
                );

            else if (t == typeof(Copy)) {
                Time time;
                if (version <= 2) {
                    time = new Time(
                        reader.ReadBoolean(),
                        Decode(reader, version),
                        reader.ReadInt32()
                    );
                } else {
                    time = Decode(reader, version);
                }

                double gate;
                if (version <= 13) {
                    gate = (double)reader.ReadDecimal();
                } else {
                    gate = reader.ReadDouble();
                }

                double pinch = 0;
                if (version >= 26) {
                    pinch = reader.ReadDouble();
                }
                
                bool bilateral = false;
                if (version >= 28) {
                    bilateral = reader.ReadBoolean();
                }

                bool reverse = false;
                if (version >= 26) {
                    reverse = reader.ReadBoolean();
                }

                bool infinite = false;
                if (version >= 27) {
                    infinite = reader.ReadBoolean();
                }

                CopyType copyType = (CopyType)reader.ReadInt32();
                GridType gridType = (GridType)reader.ReadInt32();
                bool wrap = reader.ReadBoolean();

                int count;
                List<Offset> offsets = Enumerable.Range(0, count = reader.ReadInt32()).Select(i => Decode<Offset>(reader, version)).ToList();
                List<int> angles = Enumerable.Range(0, count).Select(i => (version >= 25)? reader.ReadInt32() : 0).ToList();

                return new Copy(time, gate, pinch, bilateral, reverse, infinite, copyType, gridType, wrap, offsets, angles);
            
            } else if (t == typeof(Delay)) {
                Time time;
                if (version <= 2) {
                    time = new Time(
                        reader.ReadBoolean(),
                        Decode(reader, version),
                        reader.ReadInt32()
                    );
                } else {
                    time = Decode(reader, version);
                }

                double gate;
                if (version <= 13) {
                    gate = (double)reader.ReadDecimal();
                } else {
                    gate = reader.ReadDouble();
                }

                return new Delay(time, gate);
            
            } else if (t == typeof(Fade)) {
                Time time;
                if (version <= 2) {
                    time = new Time(
                        reader.ReadBoolean(),
                        Decode(reader, version),
                        reader.ReadInt32()
                    );
                } else {
                    time = Decode(reader, version);
                }

                double gate;
                if (version <= 13) {
                    gate = (double)reader.ReadDecimal();
                } else {
                    gate = reader.ReadDouble();
                }

                FadePlaybackType playmode = (FadePlaybackType)reader.ReadInt32();

                int count;
                List<Color> colors = Enumerable.Range(0, count = reader.ReadInt32()).Select(i => Decode<Color>(reader, version)).ToList();
                List<double> positions = Enumerable.Range(0, count).Select(i => (version <= 13)? (double)reader.ReadDecimal() : reader.ReadDouble()).ToList();
                List<FadeType> types = Enumerable.Range(0, count - 1).Select(i => (version <= 24) ? FadeType.Linear : (FadeType)reader.ReadInt32()).ToList();

                int? expanded = null;
                if (version >= 23) {
                    expanded = reader.ReadBoolean()? (int?)reader.ReadInt32() : null;
                }

                return new Fade(time, gate, playmode, colors, positions, types, expanded);

            } else if (t == typeof(Flip))
                return new Flip(
                    (FlipType)reader.ReadInt32(),
                    reader.ReadBoolean()
                );
            
            else if (t == typeof(Hold)) {
                Time time;
                if (version <= 2) {
                    time = new Time(
                        reader.ReadBoolean(),
                        Decode(reader, version),
                        reader.ReadInt32()
                    );
                } else {
                    time = Decode(reader, version);
                }

                double gate;
                if (version <= 13) {
                    gate = (double)reader.ReadDecimal();
                } else {
                    gate = reader.ReadDouble();
                }

                return new Hold(
                    time,
                    gate,
                    reader.ReadBoolean(),
                    reader.ReadBoolean()
                );
                
            } else if (t == typeof(KeyFilter)) {
                bool[] filter;
                if (version <= 18) {
                    List<bool> oldFilter = Enumerable.Range(0, 100).Select(i => reader.ReadBoolean()).ToList();
                    oldFilter.Insert(99, false);
                    filter = oldFilter.ToArray();
                } else
                    filter = Enumerable.Range(0, 101).Select(i => reader.ReadBoolean()).ToArray();

                return new KeyFilter(filter);

            } else if (t == typeof(Layer)) {
                int target = reader.ReadInt32();

                BlendingType blending = BlendingType.Normal;
                if (version >= 5) {
                    if (version == 5) {
                        blending = (BlendingType)reader.ReadInt32();
                        if ((int)blending == 2) blending = BlendingType.Mask;

                    } else {
                        blending = (BlendingType)reader.ReadInt32();
                    }
                }

                int range = 200;
                if (version >= 21)
                    range = reader.ReadInt32();

                return new Layer(target, blending, range);

            } else if (t == typeof(LayerFilter))
                return new LayerFilter(
                    reader.ReadInt32(),
                    reader.ReadInt32()
                );

            else if (t == typeof(Loop))
                return new Loop(
                    Decode(reader, version),
                    reader.ReadDouble(),
                    reader.ReadInt32(),
                    reader.ReadBoolean()
                );
                
            else if (t == typeof(Move))
                return new Move(
                    Decode(reader, version),
                    (GridType)reader.ReadInt32(),
                    reader.ReadBoolean()
                );
            
            else if (t == typeof(Multi)) {
                Chain preprocess = Decode(reader, version);

                int count = reader.ReadInt32();
                List<Chain> init = Enumerable.Range(0, count).Select(i => Decode<Chain>(reader, version)).ToList();

                if (version == 28) {
                    List<bool[]> filters = Enumerable.Range(0, count).Select(i =>
                        Enumerable.Range(0, 101).Select(i => reader.ReadBoolean()).ToArray()
                    ).ToList();

                    for (int i = 0; i < count; i++)
                        init[i].SecretMultiFilter = filters[i];
                }
                
                int? expanded = reader.ReadBoolean()? (int?)reader.ReadInt32() : null;
                MultiType mode = (MultiType)reader.ReadInt32();
                
                return new Multi(preprocess, init, expanded, mode);
            
            } else if (t == typeof(Output))
                return new Output(
                    reader.ReadInt32()
                );
            
            else if (t == typeof(MacroFilter))
                return new MacroFilter(
                    (version >= 25)? reader.ReadInt32() : 1,
                    Enumerable.Range(0, 100).Select(i => reader.ReadBoolean()).ToArray()
                );
            
            else if (t == typeof(Paint))
                return new Paint(
                    Decode(reader, version)
                );
            
            else if (t == typeof(Pattern)) {
                int repeats = 1;
                if (version >= 11) {
                    repeats = reader.ReadInt32();
                }

                double gate;
                if (version <= 13) {
                    gate = (double)reader.ReadDecimal();
                } else {
                    gate = reader.ReadDouble();
                }

                double pinch = 0;
                if (version >= 24) {
                    pinch = reader.ReadDouble();
                }

                bool bilateral = false;
                if (version >= 28) {
                    bilateral = reader.ReadBoolean();
                }

                List<Frame> frames = Enumerable.Range(0, reader.ReadInt32()).Select(i => Decode<Frame>(reader, version)).ToList();
                PlaybackType mode = (PlaybackType)reader.ReadInt32();

                bool chokeenabled = false;
                int choke = 8;

                if (version <= 10) {
                    chokeenabled = reader.ReadBoolean();

                    if (version <= 0) {
                        if (chokeenabled)
                            choke = reader.ReadInt32();
                    } else {
                        choke = reader.ReadInt32();
                    }
                }

                bool infinite = false;
                if (version >= 4) {
                    infinite = reader.ReadBoolean();
                }

                int? rootkey = null;
                if (version >= 12) {
                    rootkey = reader.ReadBoolean()? (int?)reader.ReadInt32() : null;
                }

                bool wrap = false;
                if (version >= 13) {
                    wrap = reader.ReadBoolean();
                }

                int expanded = reader.ReadInt32();

                Pattern ret = new Pattern(repeats, gate, pinch, bilateral, frames, mode, infinite, rootkey, wrap, expanded);

                if (chokeenabled) {
                    return new Choke(choke, new Chain(new List<Device>() {ret}));
                }

                return ret;
            
            } else if (t == typeof(Preview))
                return new Preview();
            
            else if (t == typeof(Rotate))
                return new Rotate(
                    (RotateType)reader.ReadInt32(),
                    reader.ReadBoolean()
                );

            else if (t == typeof(Refresh)) 
                return new Refresh(
                    Enumerable.Range(0, 4).Select(i => reader.ReadBoolean()).ToArray()
                );
            
            else if (t == typeof(Switch)) {
                int target = (version >= 25)? reader.ReadInt32() : 1;
                int value = reader.ReadInt32();

                if (18 <= version && version <= 21 && reader.ReadBoolean())
                    return new Group(new List<Chain>() {
                        new Chain(new List<Device>() {
                            new Switch(1, value), new Clear(ClearType.Multi)
                        }, "Switch Reset")
                    });

                return new Switch(target, value);
            
            } else if (t == typeof(Tone))
                return new Tone(
                    reader.ReadDouble(),
                    reader.ReadDouble(),
                    reader.ReadDouble(),
                    reader.ReadDouble(),
                    reader.ReadDouble()
                );
            
            else if (t == typeof(Color)) {
                byte red = reader.ReadByte();
                byte green = reader.ReadByte();
                byte blue = reader.ReadByte();

                if (version == 24) {
                    if (red > 0) red = (byte)((red - 1) * 62.0 / 126 + 1);
                    if (green > 0) green = (byte)((green - 1) * 62.0 / 126 + 1);
                    if (blue > 0) blue = (byte)((blue - 1) * 62.0 / 126 + 1);
                }

                return new Color(red, green, blue);
            
            } else if (t == typeof(Frame)) {
                Time time;
                if (version <= 2) {
                    time = new Time(
                        reader.ReadBoolean(),
                        Decode(reader, version),
                        reader.ReadInt32()
                    );
                } else {
                    time = Decode(reader, version);
                }

                Color[] screen;
                if (version <= 19) {
                    List<Color> oldScreen = Enumerable.Range(0, 100).Select(i => Decode<Color>(reader, version)).ToList();
                    oldScreen.Insert(99, new Color(0));
                    screen = oldScreen.ToArray();
                } else
                    screen = Enumerable.Range(0, 101).Select(i => Decode<Color>(reader, version)).ToArray();

                return new Frame(time, screen);
            
            } else if (t == typeof(Length))
                return new Length(
                    reader.ReadInt32()
                );
            
            else if (t == typeof(Offset)) {
                int x = reader.ReadInt32();
                int y = reader.ReadInt32();

                bool absolute = false;
                int ax = 5;
                int ay = 5;
                if (version >= 25) {
                    absolute = reader.ReadBoolean();
                    ax = reader.ReadInt32();
                    ay = reader.ReadInt32();
                }

                return new Offset(x, y, absolute, ax, ay);
            
            } else if (t == typeof(Time))
                return new Time(
                    reader.ReadBoolean(),
                    Decode(reader, version),
                    reader.ReadInt32()
                );
            
            else if (t == typeof(UndoManager))
                return new UndoManager(
                    Enumerable.Range(0, reader.ReadInt32()).Select(i => UndoEntry.DecodeEntry(reader, version)).ToList(),
                    reader.ReadInt32()
                );

            throw new InvalidDataException();
        }
    }
}