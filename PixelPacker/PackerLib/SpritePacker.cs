using StbImageSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace kuujoo.Pixel.Packer
{

    public class SpritePacker
    {
        class Packet : IComparable<Packet>
        {
            public Node Node { get; set; }
            public string Name { get; set; }
            public string Tag { get; set; }
            public int Frame { get; set; }
            public Color[] Pixels { get; set; }
            public Slice[] Slices { get; set; }
            public int X => Node.X;
            public int Y => Node.Y;
            public int Width { get; set; }
            public int Height { get; set; }
            public int Duration { get; set; }
            public int CompareTo(Packet other)
            {
                var r = (Math.Max(Width, Height)).CompareTo(Math.Max(Width, Height));
                if (r != 0) return r;

                return (other.Width * other.Height).CompareTo(Width * Height);
            }
        }
        class Node
        {
            public Node Right;
            public Node Bottom;
            public int X;
            public int Y;
            public int Width;
            public int Height;
            public bool Reserved = false;
        }
        List<Packet> _packets = new List<Packet>();
        List<Atlas> _atlas = new List<Atlas>();
        int _atlasWidth;
        int _atlasHeight;
        public SpritePacker(int atlasWidth, int atlasHeight)
        {
            _atlasWidth = atlasWidth;
            _atlasHeight = atlasHeight;
        }
        public void Add(string file)
        {
            if (!File.Exists(file)) return;

            var ext = Path.GetExtension(file);
            if(ext == ".ase" || ext == ".asesprite")
            {
                AddAseFile(file);
            }
            else
            {
                AddImageFile(file);
            }
        }
        public void Add(string name, int width, int height, Color[] pixels)
        {
            var packet = new Packet()
            {
                Name = name,
                Width = width,
                Height = height,
                Pixels = pixels
           };
            _packets.Add(packet);
        }
        public void Add(string name, string tag, int frame, int width, int height, Color[] pixels)
        {
            var packet = new Packet()
            {
                Name = name,
                Width = width,
                Height = height,
                Pixels = pixels,
                Tag = tag,
                Frame = frame,
            };
            _packets.Add(packet);
        }
        void Add(string name, int frame, int duration, int width, int height, Color[] pixels, Slice[] slices)
        {
            var packet = new Packet()
            {
                Name = name,
                Width = width,
                Height = height,
                Pixels = pixels,
                Slices = slices,
                Frame = frame,
                Duration = duration
            };
            _packets.Add(packet);
        }
        void Add(string name, string tag, int frame, int duration, int width, int height, Color[] pixels, Slice[] slices)
        {
            var packet = new Packet()
            {
                Name = name,
                Width = width,
                Height = height,
                Pixels = pixels,
                Tag = tag,
                Slices = slices,
                Frame = frame,
                Duration = duration
            };
            _packets.Add(packet);
        }
        public void Add(string name,  int width, int height, byte[] pixels)
        {
            var pixls = new Color[width * height];
            for(var i = 0; i < width * height; i++)
            {
                var r = pixels[i * 4];
                var g = pixels[i * 4 + 1];
                var b = pixels[i * 4 + 2];
                var a = pixels[i * 4 + 3];
                pixls[i] = new Color(r, g, b, a);
            }
            var packet = new Packet()
            {
                Name = name,
                Width = width,
                Height = height,
                Pixels = pixls,
            };
            _packets.Add(packet);
        }
        Aseprite.Tag GetAseTag(Aseprite ase, int frame)
        {
            for (var t = 0; t < ase.Tags.Count; t++)
            {
                var tag = ase.Tags[t];
                if (frame >= tag.From && frame <= tag.To)
                {
                    return tag;
                }
            }
            return null;
        }
        Slice[] GetAseSlices(Aseprite ase, int frame)
        {
            List<Aseprite.Slice> aseSlices = new List<Aseprite.Slice>();
            for (var i = 0; i < ase.Slices.Count; i++)
            {
                if(ase.Slices[i].Frame == frame)
                {
                    aseSlices.Add(ase.Slices[i]);
                }
            }

            Slice[] slices = new Slice[aseSlices.Count];
            for(var i = 0; i < aseSlices.Count; i++)
            {
                var aseSlice = aseSlices[i];
                slices[i] = new Slice()
                {
                    X = aseSlice.OriginX,
                    Y = aseSlice.OriginY,
                    Width = aseSlice.Width,
                    Height = aseSlice.Height,
                    Name = aseSlice.Name
                };
                if(aseSlice.Pivot.HasValue)
                {
                    slices[i].PivotX = aseSlice.Pivot.Value.X;
                    slices[i].PivotY = aseSlice.Pivot.Value.Y;
                }
            }
            return slices;
        }
        void AddAseFile(string file)
        {
            var fileName = Path.GetFileNameWithoutExtension(file);
            var ase = new Aseprite(file);

            for (var f = 0; f < ase.FrameCount; f++)
            {
                var frame = ase.Frames[f];
                var tag = GetAseTag(ase, f);
                var slices = GetAseSlices(ase, f);
                if (tag != null)
                {
                    if (slices.Length > 0)
                    {
                        Add(fileName, tag.Name, f, frame.Duration, ase.Width, ase.Height, frame.Pixels, slices);
                    }
                    else
                    {
                        Add(fileName, tag.Name, f, frame.Duration, ase.Width, ase.Height, frame.Pixels, null);
                    }
                 
                }
                else
                {
                    if (slices.Length > 0)
                    {
                        Add(fileName, f, frame.Duration, ase.Width, ase.Height, frame.Pixels, slices);
                    }
                    else
                    {
                        Add(fileName, f, frame.Duration, ase.Width, ase.Height, frame.Pixels, null);
                    }
                }
            }
        }
        void AddImageFile(string file)
        {
            var fileName = Path.GetFileNameWithoutExtension(file);
            ImageResult image;
            using (var stream = File.OpenRead(file))
            {
                image = ImageResult.FromStream(stream, ColorComponents.RedGreenBlueAlpha);
                Add(fileName, image.Width, image.Height, image.Data);
            }
        }
        public List<Atlas> Pack()
        {
            while (_packets.Count > 0)
            {
                int packets = DoPack(ref _packets);
                var atlas = GetAtlas();
                for(var i = 0; i < packets; i++)
                {
                    var packet = _packets[i];
                    atlas.AddPixels(packet.Name, packet.Tag, packet.Frame, packet.Duration, packet.X, packet.Y, packet.Width, packet.Height, packet.Pixels, packet.Slices);
                }
                _packets.RemoveRange(0, packets);
            }

            return _atlas;
        }
        Atlas GetAtlas()
        {
            var atlas = new Atlas(_atlasWidth, _atlasHeight); ;
            _atlas.Add(atlas);
            return atlas;
        }
        int DoPack(ref List<Packet> packets)
        {
            packets.Sort();
            Node root = new Node()
            {
                Width = _atlasWidth,
                Height = _atlasHeight
            };

            for (var i = 0; i < packets.Count; i++)
            {
                var rect = packets[i];
                var node = FindNode(root, rect.Width, rect.Height);
                if (node != null)
                {
                    rect.Node = SplitNode(node, rect.Width, rect.Height);
                }
                else
                {
                    return i;
                }
            }
            return _packets.Count;
        }
        Node FindNode(Node root, int width, int height)
        {
            if (root.Reserved)
            {
                var next = FindNode(root.Right, width, height);
                if (next == null)
                {
                    next = FindNode(root.Bottom, width, height);
                }
                return next;
            }
            else if (width <= root.Width && height <= root.Height)
            {
                return root;
            }
            else
            {
                return null;
            }
        }
        Node SplitNode(Node node, int width, int height)
        {
            node.Reserved = true;
            node.Right = new Node()
            {
                X = node.X + width,
                Y = node.Y,
                Width = node.Width - width,
                Height = height
            };
            node.Bottom = new Node()
            {
                X = node.X,
                Y = node.Y + height,
                Width = node.Width,
                Height = node.Height - height
            };
            return node;
        }
    }
}
