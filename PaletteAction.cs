using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DotEdit
{
    class PaletteAction : IAction
    {
        private readonly byte[][] paletteState;
        private readonly ImageData image;

        public PaletteAction(ImageData image)
        {
            this.image = image;

            paletteState = new byte[this.image.Palette.Length][];

            ReassignPalette(paletteState, this.image.Palette);
        }

        public static void ReassignPalette(byte[][] dest, byte[][] src)
        {
            for (int i = 0; i < src.Length; i++)
            {
                dest[i] = [
                    src[i][(int)ImageData.ColourChannelIndex.BLUE],
                    src[i][(int)ImageData.ColourChannelIndex.GREEN],
                    src[i][(int)ImageData.ColourChannelIndex.RED],
                    src[i][(int)ImageData.ColourChannelIndex.ALPHA]
                ];
            }
        }

        public IAction ExecuteAction()
        {
            PaletteAction redo = new(image);

            image.Palette = paletteState;

            return redo;
        }
    }
}
