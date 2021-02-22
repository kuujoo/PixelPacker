using StbImageWriteSharp;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;

namespace kuujoo.Pixel
{
    class Program
    {
        static void Main(string[] args)
        {

            var currentDirectory = Directory.GetCurrentDirectory();
            var aseFiles = Directory.GetFiles(currentDirectory, "*.ase", SearchOption.AllDirectories);
            var pngFiles = Directory.GetFiles(currentDirectory, "*.png", SearchOption.AllDirectories);
            var asepriteFiles = Directory.GetFiles(currentDirectory, "*.aseprite", SearchOption.AllDirectories);

            SpritePacker packer = new SpritePacker(2048, 2048);

            foreach(var png in pngFiles)
            {
                if (png.StartsWith("atlas_")) continue;
                packer.Add(png);
            }
            foreach (var ase in aseFiles)
            {
                packer.Add(ase);
            }
            foreach (var ase in asepriteFiles)
            {
                packer.Add(ase);
            }

            var atlases = packer.Pack();

            int i = 0;
            foreach(var atlas in atlases)
            {
                atlas.Save($"atlas_{i}");
                i++;
                System.Console.WriteLine($"Saved atlas atlas_{i}.png & atlas_{i}.json");
            }          
        }
    }
}
