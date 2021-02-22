using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;


// Originaly by Noel Berry: https://gist.github.com/NoelFB/778d190e5d17f1b86ebf39325346fcc5
//
// File Format:
// https://github.com/aseprite/aseprite/blob/master/docs/ase-file-specs.md

// Note: I didn't test with with Indexed or Grayscale colors
// Only implemented the stuff I needed / wanted, other stuff is ignored


namespace kuujoo.Pixel
{
    public struct Color
    {
        public byte R;
        public byte G;
        public byte B;
        public byte A;

        public Color(int r, int g, int b, int a)
        {
            R = (byte)r;
            G = (byte)g;
            B = (byte)b;
            A = (byte)a;
        }

        // this premultiplies the alpha ...
        // depending on your use-case, you may not want this
        public static Color FromNonPremultiplied(int r, int g, int b, int a)
        {
            return new Color(
                (r * a / 255),
                (g * a / 255),
                (b * a / 255),
                a
            );
        }
    }

    public struct Point
    {
        public int X;
        public int Y;

        public Point(int x, int y) { X = x; Y = y; }
    }

    public class Aseprite
    {

        public enum Modes
        {
            Indexed = 1,
            Grayscale = 2,
            RGBA = 4
        }

        private enum Chunks
        {
            OldPaletteA = 0x0004,
            OldPaletteB = 0x0011,
            Layer = 0x2004,
            Cel = 0x2005,
            CelExtra = 0x2006,
            Mask = 0x2016,
            Path = 0x2017,
            FrameTags = 0x2018,
            Palette = 0x2019,
            UserData = 0x2020,
            Slice = 0x2022
        }

        public readonly Modes Mode;
        public readonly int Width;
        public readonly int Height;
        public readonly int FrameCount;

        public List<Layer> Layers = new List<Layer>();
        public List<Frame> Frames = new List<Frame>();
        public List<Tag> Tags = new List<Tag>();
        public List<Slice> Slices = new List<Slice>();

        public Aseprite(Modes mode, int width, int height)
        {
            Mode = mode;
            Width = width;
            Height = height;
        }

        #region .ase Parser

        public Aseprite(string file, bool loadImageData = true)
        {
            using (var stream = File.OpenRead(file))
            {
                var reader = new BinaryReader(stream);

                // wrote these to match the documentation names so it's easier (for me, anyway) to parse
                byte BYTE() { return reader.ReadByte(); }
                ushort WORD() { return reader.ReadUInt16(); }
                short SHORT() { return reader.ReadInt16(); }
                uint DWORD() { return reader.ReadUInt32(); }
                long LONG() { return reader.ReadInt32(); }
                string STRING() { return Encoding.UTF8.GetString(BYTES(WORD())); }
                byte[] BYTES(int number) { return reader.ReadBytes(number); }
                void SEEK(int number) { reader.BaseStream.Position += number; }

                // Header
                {
                    // file size
                    DWORD();

                    // Magic number (0xA5E0)
                    var magic = WORD();
                    if (magic != 0xA5E0)
                        throw new Exception("File is not in .ase format");

                    // Frames / Width / Height / Color Mode
                    FrameCount = WORD();
                    Width = WORD();
                    Height = WORD();
                    Mode = (Modes)(WORD() / 8);

                    // Other Info, Ignored
                    DWORD();       // Flags
                    WORD();        // Speed (deprecated)
                    DWORD();       // Set be 0
                    DWORD();       // Set be 0
                    BYTE();        // Palette entry 
                    SEEK(3);       // Ignore these bytes
                    WORD();        // Number of colors (0 means 256 for old sprites)
                    BYTE();        // Pixel width
                    BYTE();        // Pixel height
                    SEEK(92);      // For Future
                }

                // temporary variables
                var temp = new byte[Width * Height * (int)Mode];
                var palette = new Color[256];
                IUserData last = null;

                // Frames
                for (int i = 0; i < FrameCount; i++)
                {
                    var frame = new Frame(this);
                    if (loadImageData)
                        frame.Pixels = new Color[Width * Height];
                    Frames.Add(frame);

                    long frameStart, frameEnd;
                    int chunkCount;

                    // frame header
                    {
                        frameStart = reader.BaseStream.Position;
                        frameEnd = frameStart + DWORD();
                        WORD();                  // Magic number (always 0xF1FA)
                        chunkCount = WORD();     // Number of "chunks" in this frame
                        frame.Duration = WORD(); // Frame duration (in milliseconds)
                        SEEK(6);                 // For future (set to zero)
                    }

                    // chunks
                    for (int j = 0; j < chunkCount; j++)
                    {
                        long chunkStart, chunkEnd;
                        Chunks chunkType;

                        // chunk header
                        {
                            chunkStart = reader.BaseStream.Position;
                            chunkEnd = chunkStart + DWORD();
                            chunkType = (Chunks)WORD();
                        }

                        // LAYER CHUNK
                        if (chunkType == Chunks.Layer)
                        {
                            // create layer
                            var layer = new Layer();

                            // get layer data
                            layer.Flag = (Layer.Flags)WORD();
                            layer.Type = (Layer.Types)WORD();
                            layer.ChildLevel = WORD();
                            WORD(); // width (unused)
                            WORD(); // height (unused)
                            layer.BlendMode = WORD();
                            layer.Alpha = (BYTE() / 255f);
                            SEEK(3); // for future
                            layer.Name = STRING();

                            last = layer;
                            Layers.Add(layer);
                        }
                        // CEL CHUNK
                        else if (chunkType == Chunks.Cel)
                        {
                            // create cel
                            var cel = new Cel();

                            // get cel data
                            cel.Layer = Layers[WORD()];
                            cel.X = SHORT();
                            cel.Y = SHORT();
                            cel.Alpha = BYTE() / 255f;
                            var celType = WORD(); // type
                            SEEK(7);

                            if (loadImageData)
                            {
                                // RAW or DEFLATE
                                if (celType == 0 || celType == 2)
                                {
                                    cel.Width = WORD();
                                    cel.Height = WORD();

                                    var count = cel.Width * cel.Height * (int)Mode;

                                    // RAW
                                    if (celType == 0)
                                    {
                                        reader.Read(temp, 0, cel.Width * cel.Height * (int)Mode);
                                    }
                                    // DEFLATE
                                    else
                                    {
                                        SEEK(2);

                                        var deflate = new DeflateStream(reader.BaseStream, CompressionMode.Decompress);
                                        deflate.Read(temp, 0, count);
                                    }

                                    cel.Pixels = new Color[cel.Width * cel.Height];
                                    BytesToPixels(temp, cel.Pixels, Mode, palette);
                                    CelToFrame(frame, cel);
                                }
                                // REFERENCE
                                else if (celType == 1)
                                {
                                    // not gonna worry about it
                                }
                            }

                            last = cel;
                            frame.Cels.Add(cel);
                        }
                        // PALETTE CHUNK
                        else if (chunkType == Chunks.Palette)
                        {
                            var size = DWORD();
                            var start = DWORD();
                            var end = DWORD();
                            SEEK(8); // for future

                            for (int p = 0; p < (end - start) + 1; p++)
                            {
                                var hasName = WORD();
                                palette[start + p] = Color.FromNonPremultiplied(BYTE(), BYTE(), BYTE(), BYTE());
                                if (IsBitSet(hasName, 0))
                                    STRING();
                            }
                        }
                        // USERDATA
                        else if (chunkType == Chunks.UserData)
                        {
                            if (last != null)
                            {
                                var flags = (int)DWORD();

                                // has text
                                if (IsBitSet(flags, 0))
                                    last.UserDataText = STRING();

                                // has color
                                if (IsBitSet(flags, 1))
                                    last.UserDataColor = Color.FromNonPremultiplied(BYTE(), BYTE(), BYTE(), BYTE());
                            }
                        }
                        // TAG
                        else if (chunkType == Chunks.FrameTags)
                        {
                            var count = WORD();
                            SEEK(8);

                            for (int t = 0; t < count; t++)
                            {
                                var tag = new Tag();
                                tag.From = WORD();
                                tag.To = WORD();
                                tag.LoopDirection = (Tag.LoopDirections)BYTE();
                                SEEK(8);
                                tag.Color = Color.FromNonPremultiplied(BYTE(), BYTE(), BYTE(), 255);
                                SEEK(1);
                                tag.Name = STRING();
                                Tags.Add(tag);
                            }
                        }
                        // SLICE
                        else if (chunkType == Chunks.Slice)
                        {
                            var count = DWORD();
                            var flags = (int)DWORD();
                            DWORD(); // reserved
                            var name = STRING();

                            for (int s = 0; s < count; s++)
                            {
                                var slice = new Slice();
                                slice.Name = name;
                                slice.Frame = (int)DWORD();
                                slice.OriginX = (int)LONG();
                                slice.OriginY = (int)LONG();
                                slice.Width = (int)DWORD();
                                slice.Height = (int)DWORD();

                                // 9 slice (ignored atm)
                                if (IsBitSet(flags, 0))
                                {
                                    LONG();
                                    LONG();
                                    DWORD();
                                    DWORD();
                                }

                                // pivot point
                                if (IsBitSet(flags, 1))
                                    slice.Pivot = new Point((int)DWORD(), (int)DWORD());

                                last = slice;
                                Slices.Add(slice);
                            }
                        }

                        reader.BaseStream.Position = chunkEnd;
                    }

                    reader.BaseStream.Position = frameEnd;
                }
            }
        }

        #endregion

        #region Data Structures

        public class Frame
        {
            public Aseprite Sprite;
            public int Duration;
            public Color[] Pixels;
            public List<Cel> Cels;

            public Frame(Aseprite sprite)
            {
                Sprite = sprite;
                Cels = new List<Cel>();
            }
        }

        public class Tag
        {
            public enum LoopDirections
            {
                Forward = 0,
                Reverse = 1,
                PingPong = 2
            }

            public string Name;
            public LoopDirections LoopDirection;
            public int From;
            public int To;
            public Color Color;
        }

        public interface IUserData
        {
            string UserDataText { get; set; }
            Color UserDataColor { get; set; }
        }

        public struct Slice : IUserData
        {
            public int Frame;
            public string Name;
            public int OriginX;
            public int OriginY;
            public int Width;
            public int Height;
            public Point? Pivot;
            public string UserDataText { get; set; }
            public Color UserDataColor { get; set; }
        }

        public class Cel : IUserData
        {
            public Layer Layer;
            public Color[] Pixels;

            public int X;
            public int Y;
            public int Width;
            public int Height;
            public float Alpha;

            public string UserDataText { get; set; }
            public Color UserDataColor { get; set; }
        }

        public class Layer : IUserData
        {
            [Flags]
            public enum Flags
            {
                Visible = 1,
                Editable = 2,
                LockMovement = 4,
                Background = 8,
                PreferLinkedCels = 16,
                Collapsed = 32,
                Reference = 64
            }

            public enum Types
            {
                Normal = 0,
                Group = 1
            }

            public Flags Flag;
            public Types Type;
            public string Name;
            public int ChildLevel;
            public int BlendMode;
            public float Alpha;

            public string UserDataText { get; set; }
            public Color UserDataColor { get; set; }
        }

        #endregion

        #region Blend Modes

        // Copied from Aseprite's source code:
        // https://github.com/aseprite/aseprite/blob/master/src/doc/blend_funcs.cpp

        private delegate void Blend(ref Color dest, Color src, byte opacity);

        private static Blend[] BlendModes = new Blend[]
        {
            // 0 - NORMAL
            (ref Color dest, Color src, byte opacity) =>
            {
                int r, g, b, a;

                if (dest.A == 0)
                {
                    r = src.R;
                    g = src.G;
                    b = src.B;
                }
                else if (src.A == 0)
                {
                    r = dest.R;
                    g = dest.G;
                    b = dest.B;
                }
                else
                {
                    r = (dest.R + MUL_UN8((src.R - dest.R), opacity));
                    g = (dest.G + MUL_UN8((src.G - dest.G), opacity));
                    b = (dest.B + MUL_UN8((src.B - dest.B), opacity));
                }

                a = (dest.A + MUL_UN8((src.A - dest.A), opacity));
                if (a == 0)
                    r = g = b = 0;

                dest.R = (byte)r;
                dest.G = (byte)g;
                dest.B = (byte)b;
                dest.A = (byte)a;
            }
        };

        private static int MUL_UN8(int a, int b)
        {
            var t = (a * b) + 0x80;
            return (((t >> 8) + t) >> 8);
        }

        #endregion

        #region Utils

        /// <summary>
        /// Converts an array of Bytes to an array of Colors, using the specific Aseprite Mode & Palette
        /// </summary>
        private void BytesToPixels(byte[] bytes, Color[] pixels, Aseprite.Modes mode, Color[] palette)
        {
            int len = pixels.Length;
            if (mode == Modes.RGBA)
            {
                for (int p = 0, b = 0; p < len; p++, b += 4)
                {
                    pixels[p].R = (byte)(bytes[b + 0] * bytes[b + 3] / 255);
                    pixels[p].G = (byte)(bytes[b + 1] * bytes[b + 3] / 255);
                    pixels[p].B = (byte)(bytes[b + 2] * bytes[b + 3] / 255);
                    pixels[p].A = bytes[b + 3];
                }
            }
            else if (mode == Modes.Grayscale)
            {
                for (int p = 0, b = 0; p < len; p++, b += 2)
                {
                    pixels[p].R = pixels[p].G = pixels[p].B = (byte)(bytes[b + 0] * bytes[b + 1] / 255);
                    pixels[p].A = bytes[b + 1];
                }
            }
            else if (mode == Modes.Indexed)
            {
                for (int p = 0, b = 0; p < len; p++, b += 1)
                    pixels[p] = palette[b];
            }
        }

        /// <summary>
        /// Applies a Cel's pixels to the Frame, using its Layer's BlendMode & Alpha
        /// </summary>
        private void CelToFrame(Frame frame, Cel cel)
        {
            var opacity = (byte)((cel.Alpha * cel.Layer.Alpha) * 255);
            var blend = BlendModes[cel.Layer.BlendMode];

            for (int sx = 0; sx < cel.Width; sx++)
            {
                int dx = cel.X + sx;
                int dy = cel.Y * frame.Sprite.Width;

                for (int i = 0, sy = 0; i < cel.Height; i++, sy += cel.Width, dy += frame.Sprite.Width)
                    blend(ref frame.Pixels[dx + dy], cel.Pixels[sx + sy], opacity);
            }
        }

        private static bool IsBitSet(int b, int pos)
        {
            return (b & (1 << pos)) != 0;
        }

        #endregion
    }
}