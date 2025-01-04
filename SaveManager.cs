using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using System.Windows.Media;

namespace DotEdit
{
    class SaveManager
    {
        public static bool SaveImageToDisk(string url, ImageData image)
        {
            try
            {
                using (FileStream file = new(url, FileMode.Create))
                {
                    List<Color> colours = [];

                    foreach (byte[] color in image.Palette)
                    {
                        byte a = color[(int)ImageData.ColourChannelIndex.ALPHA];
                        byte r = color[(int)ImageData.ColourChannelIndex.RED];
                        byte g = color[(int)ImageData.ColourChannelIndex.GREEN];
                        byte b = color[(int)ImageData.ColourChannelIndex.BLUE];

                        colours.Add(Color.FromArgb(a, r, g, b));
                    }

                    BitmapSource result = BitmapSource.Create(
                        image.Width,
                        image.Height,
                        300,
                        300,
                        PixelFormats.Indexed4,
                        new(colours),
                        image.PackPixelData(),
                        image.Width / ImageData.PIXEL_PER_BYTE
                    );

                    BmpBitmapEncoder encoder = new();

                    encoder.Frames.Add(BitmapFrame.Create(result));
                    encoder.Save(file);
                }

                File.AppendAllBytes(url, image.GetLevelData());

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static ImageData? LoadImageFromDisk(string url)
        {
            try
            {
                ImageData image = new(url);

                return image;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
