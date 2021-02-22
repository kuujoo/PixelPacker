using StbImageWriteSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text.Json;

namespace kuujoo.Pixel
{
    public class Atlas
    {
        public class SubImage : IComparable<SubImage>
        {
            public string Name { get; set; }
            public string Tag { get; set; }
            public int Frame { get; set; }
            public int X { get; set; }
            public int Y { get; set; }
            public int Width { get; set; }
            public int Height { get; set; }
            public Slice[] Slices { get; set; }

            public int CompareTo(SubImage obj)
            {
                return Frame.CompareTo(obj.Frame);
            }
        }

        public Color[] Pixels { get; private set; }
        public List<SubImage> Subimages { get; private set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public Atlas(int width, int height)
        {
            Width = width;
            Height = height;
            Pixels = new Color[Width * Height];
            var black = new Color(0, 0, 0, 255);
            for(var i = 0; i < Width * Height; i++)
            {
                Pixels[i] = black;
            }
            Subimages = new List<SubImage>();
        }
        public byte[] PixelsAsByteArray()
        {
            var bytes = new byte[Width * Height * 4];
            for (var i = 0; i < Width * Height; i++)
            {
                bytes[i * 4 + 0] = Pixels[i].R;
                bytes[i * 4 + 1] = Pixels[i].G;
                bytes[i * 4 + 2] = Pixels[i].B;
                bytes[i * 4 + 3] = Pixels[i].A;
            }
            return bytes;
        }
        public void FillSubimages(string name, string tag, ref List<SubImage> images)
        {
            for(var i = 0; i < Subimages.Count; i++)
            {
                var subImage = Subimages[i];
                if(subImage.Name == name && subImage.Tag == tag)
                {
                    images.Add(subImage);
                }
            }
            images.Sort();
        }
        public void AddPixels(string name, string tag, int frame, int x, int y, int width, int height, Color[] pixels, Slice[] slices)
        {
            for(int i = 0; i < width; i++)
            {
                for(int j = 0; j < height; j++)
                {
                    var srcIdx = j * width + i;
                    var dstIdx = (y + j) * Height + (i + x);
                    Pixels[dstIdx] = pixels[srcIdx];
                }
            }

            Subimages.Add(new SubImage()
            {
                Name = name,
                Tag = tag,
                Frame = frame,
                X = x,
                Y = y,
                Width = width,
                Height = height,
                Slices = slices
            });
        }
        public void Save(string file)
        {

            var atlasImageFile = file + ".png";
            var atlasJsonFile = file + ".json";
            using (Stream stream = File.OpenWrite(atlasImageFile))
            {
                ImageWriter writer = new ImageWriter();
                writer.WritePng(PixelsAsByteArray(), Width, Height, ColorComponents.RedGreenBlueAlpha, stream);
            }

            var atlasJson = JsonSerializer.Serialize(Subimages);
            File.WriteAllText(atlasJsonFile, atlasJson);
        }
    }
}
