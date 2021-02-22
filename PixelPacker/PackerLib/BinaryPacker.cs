    using System;
    using System.Collections.Generic;

namespace kuujoo.Pixel.Packer
{

    public class BinaryPackerNode
    {
        public BinaryPackerNode Right;
        public BinaryPackerNode Bottom;
        public int X;
        public int Y;
        public int Width;
        public int Height;
        public bool Reserved = false;
    }
    public abstract class BinaryPackerRect : IComparable<BinaryPackerRect>
    {
        public BinaryPackerNode Node;
        public int X => Node.X;
        public int Y => Node.Y;
        public abstract int Width { get; set; }
        public abstract int Height { get; set; }
        public int CompareTo(BinaryPackerRect other)
        {
            var r = (Math.Max(other.Width, other.Height)).CompareTo(Math.Max(Width, Height));
            if (r != 0) return r;

            return (other.Width * other.Height).CompareTo(Width * Height);
        }
    }

    public static class BinaryPacker
    {
        public static int Pack<T>(int width, int height, ref List<T> _rects) where T : BinaryPackerRect
        {
            _rects.Sort();
            BinaryPackerNode root = new BinaryPackerNode()
            {
                Width = width,
                Height = height
            };

            for (var i = 0; i < _rects.Count; i++)
            {
                var rect = _rects[i];
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
            return _rects.Count;
        }
        static BinaryPackerNode FindNode(BinaryPackerNode root, int width, int height)
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
        static BinaryPackerNode SplitNode(BinaryPackerNode node, int width, int height)
        {
            node.Reserved = true;
            node.Right = new BinaryPackerNode()
            {
                X = node.X + width,
                Y = node.Y,
                Width = node.Width - width,
                Height = height
            };
            node.Bottom = new BinaryPackerNode()
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

