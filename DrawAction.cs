using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DotEdit
{
    class DrawAction : IAction
    {
        private readonly byte[] canvasState;
        private readonly ImageData image;

        public DrawAction(ImageData image)
        {
            this.image = image;

            canvasState = new byte[this.image.Width * this.image.Height];

            ReassignCanvasData(canvasState, this.image.Data);
        }

        public static void ReassignCanvasData(byte[] dest, byte[] src)
        {
            for (int i = 0; i < src.Length; i++)
            {
                dest[i] = src[i];
            }
        }

        public IAction ExecuteAction()
        {
            CanvasAction redo = new(image);

            image.Data = canvasState;

            return redo;
        }
    }
}
