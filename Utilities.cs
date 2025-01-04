using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;

namespace DotEdit
{
    class Utilities
    {
        public static int[] GetHexcodeFromString(string hexCode)
        {
            int[] value = new int[ImageData.COLOUR_CHANNELS];

            string hex = hexCode.Trim().TrimStart('#').ToUpper();

            if (hex.Length == 3)
            {
                value[0] = int.Parse($"{hex[..1]}{hex[..1]}", System.Globalization.NumberStyles.HexNumber);
                value[1] = int.Parse($"{hex[1..2]}{hex[1..2]}", System.Globalization.NumberStyles.HexNumber);
                value[2] = int.Parse($"{hex[2..3]}{hex[2..3]}", System.Globalization.NumberStyles.HexNumber);
            }
            else if (hex.Length == 6)
            {
                value[0] = int.Parse($"{hex[..2]}", System.Globalization.NumberStyles.HexNumber);
                value[1] = int.Parse($"{hex[2..4]}", System.Globalization.NumberStyles.HexNumber);
                value[2] = int.Parse($"{hex[4..6]}", System.Globalization.NumberStyles.HexNumber);
            }

            return value;
        }

        public static void ClearBitmap(WriteableBitmap bmp, int clearR = 0, int clearG = 0, int clearB = 0, int clearA = 0)
        {
            byte[] pixels = new byte[bmp.PixelWidth * bmp.PixelHeight * ImageData.COLOUR_CHANNELS];

            for (int i = 0; i < pixels.Length; i += 4)
            {
                pixels[i + (int)ImageData.ColourChannelIndex.ALPHA] = (byte)clearA;
                pixels[i + (int)ImageData.ColourChannelIndex.RED] = (byte)clearR;
                pixels[i + (int)ImageData.ColourChannelIndex.GREEN] = (byte)clearG;
                pixels[i + (int)ImageData.ColourChannelIndex.BLUE] = (byte)clearB;
            }

            Int32Rect rect = new()
            {
                X = 0,
                Y = 0,
                Width = bmp.PixelWidth,
                Height = bmp.PixelHeight
            };

            bmp.WritePixels(rect, pixels, bmp.PixelWidth * ImageData.BIT_PER_PIXEL, 0);
        }
    }
}
