using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DotEdit
{
    class PaletteManager
    {
        public static bool LoadPalette(byte[][] palette, string url)
        {
            try
            {
                string[] hexcodes = File.ReadAllLines(url);

                for (int i = 0; i < hexcodes.Length; i++)
                {
                    int[] value = Utilities.GetHexcodeFromString(hexcodes[i]);

                    palette[i][(int)ImageData.ColourChannelIndex.RED] = (byte)value[0];
                    palette[i][(int)ImageData.ColourChannelIndex.GREEN] = (byte)value[1];
                    palette[i][(int)ImageData.ColourChannelIndex.BLUE] = (byte)value[2];
                    palette[i][(int)ImageData.ColourChannelIndex.ALPHA] = 0xff;
                }

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static bool SavePalette(string url, byte[][] palette)
        {
            try
            {
                using FileStream file = new(url, FileMode.Create);
                string hexcodes = "";

                foreach (byte[] color in palette)
                {
                    byte r = color[(int)ImageData.ColourChannelIndex.RED];
                    byte g = color[(int)ImageData.ColourChannelIndex.GREEN];
                    byte b = color[(int)ImageData.ColourChannelIndex.BLUE];

                    hexcodes += $"{r.ToString("X2").ToLower()}{g.ToString("X2").ToLower()}{b.ToString("X2").ToLower()}";
                }

                File.WriteAllText(url, hexcodes);

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
