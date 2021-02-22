# PixelPacker

Image atlas builder made in C# .NET Core

Supported files:

* Aseprite
* PNG
* JPEG
* BMP

```csharp

// Prepare color data
Color[] Red = new Color[256 * 256];
Color[] Green = new Color[256 * 256];
Color[] Blue = new Color[256 * 256];
for (var i = 0; i < 256 * 256; i++)
{
	Red[i] = new Color(255, 0, 0, 255);
	Green[i] = new Color(0, 255, 0, 255);
	Blue[i] = new Color(0, 0, 255, 255);
}

// Maximum atlas size 2048 x 2048
SpritePacker packer = new SpritePacker(2048, 2048);

//Add color data
packer.Add("Red", 256, 256, Red);
packer.Add("Green", 256, 256, Green);
packer.Add("Blue", 256, 256, Blue);

// Add png image
packer.Add("image.png");

// Add Aseprite
packer.Add("image.ase");
var atlas = packer.Pack();

// Save first atlas to file (generates .png and .json that descripes subimages+slices)
atlas[0].Save("test");	
```

#### Special thanks

* [AseSprite Parser by Noel Berry](https://gist.github.com/NoelFB/778d190e5d17f1b86ebf39325346fcc5) - Asesprite loader
* [StbSharp](https://github.com/StbSharp/StbImageSharp) - image file loaders / writers
