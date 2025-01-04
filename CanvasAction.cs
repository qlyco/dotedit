using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DotEdit
{
    class CanvasAction : IAction
    {
        private readonly ImageData image;
        private byte[] canvasState;
        private byte[][] paletteState;

        public CanvasAction(ImageData image)
        {
            this.image = image;

            canvasState = new byte[this.image.Width * this.image.Height];
            paletteState = new byte[this.image.Palette.Length][];

            DrawAction.ReassignCanvasData(canvasState, this.image.Data);
            PaletteAction.ReassignPalette(paletteState, this.image.Palette);
        }

        public IAction ExecuteAction()
        {
            CanvasAction redo = new(image);

            image.Data = canvasState;
            image.Palette = paletteState;

            return redo;
        }
    }
}
