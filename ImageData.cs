using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace DotEdit
{
    class ImageData
    {
        private const int PIXEL_DATA_ADDRESS_OFFSET = 10;
        private const int DIB_HEADER_OFFSET = 14;
        private const int WIDTH_OFFSET = 18;
        private const int HEIGHT_OFFSET = 22;
        private const int BPP_OFFSET = 28;
        private const int COMPRESSION_OFFSET = 30;
        private const int BI_RGB_COMPRESSION = 0;
        private const int BITMAP_INFO_HEADER_SIZE = 40;

        private const int COLOUR_TABLE_OFFSET = DIB_HEADER_OFFSET + BITMAP_INFO_HEADER_SIZE;

        public static readonly int PIXEL_PER_BYTE = 2;
        public static readonly int COLOUR_CHANNELS = 4;
        public static readonly int BIT_PER_PIXEL = 4;
        public static readonly int MIN_CANVAS_SIZE = 8;
        public static readonly int MAX_CANVAS_SIZE = 256;

        public static readonly int ERR_INVALID_FORMAT = 1;

        public static readonly int DLV1_SIZE_THRESHOLD = 64;
        public static readonly int DLV1_PALETTE_THRESHOLD = 8;

        public enum ColourChannelIndex
        {
            ALPHA = 3,
            RED = 2,
            GREEN = 1,
            BLUE = 0
        }

        public int Width { get; set; } = MIN_CANVAS_SIZE;
        public int Height { get; set; } = MIN_CANVAS_SIZE;
        public byte[][] Palette { get; set; } = [
            [0x00, 0x00, 0x00, 0xff],
            [0xff, 0xff, 0xff, 0xff],
            [0x00, 0x00, 0xff, 0xff],
            [0x00, 0xff, 0x00, 0xff],
            [0xff, 0x00, 0x00, 0xff],
            [0x00, 0xff, 0xff, 0xff],
            [0xff, 0x00, 0xff, 0xff],
            [0xff, 0xff, 0x00, 0xff],
            [0xff, 0xff, 0xff, 0xff],
            [0xff, 0xff, 0xff, 0xff],
            [0xff, 0xff, 0xff, 0xff],
            [0xff, 0xff, 0xff, 0xff],
            [0xff, 0xff, 0xff, 0xff],
            [0xff, 0xff, 0xff, 0xff],
            [0xff, 0xff, 0xff, 0xff],
            [0xff, 0xff, 0xff, 0xff],
        ];
        public byte[] Data { get; set; } = [];

        private byte[]? temp = null;

        public ImageData(int width, int height, byte[][]? palette = null)
        {
            if (width < MIN_CANVAS_SIZE || height < MIN_CANVAS_SIZE || width > MAX_CANVAS_SIZE || height > MAX_CANVAS_SIZE)
            {
                throw new ArgumentException("Invalid canvas size (8px - 256px).");
            }

            Width = width;
            Height = height;

            Data = new byte[Width * Height];

            Array.Fill(Data, (byte)1, 0, Data.Length);

            if (palette != null)
            {
                bool hasError = false;

                for (int i = 0; i < Palette.Length; i++)
                {
                    if (palette[i].Length != COLOUR_CHANNELS)
                    {
                        hasError = true;
                        break;
                    }

                    for (int j = 0; j < Palette[i].Length; j++)
                    {
                        Palette[i][j] = palette[i][j];
                    }
                }

                if (hasError)
                {
                    throw new ArgumentException("Invalid palette data (BGRA)");
                }
            }
        }

        public ImageData(string url)
        {
            if (LoadFromImage(url) == ERR_INVALID_FORMAT)
            {
                throw new FileFormatException("Invalid file format.");
            }
        }

        public void SetData(int x, int y, int colourIndex = 0)
        {
            if (x < 0 && x >= Width || y < 0 && y >= Height)
            {
                return;
            }

            Data[x + y * Width] = (byte)colourIndex;
        }

        public string GetFormatIdentifier()
        {
            string format = "DLV1";

            if (GetUsedPalette().Length > DLV1_PALETTE_THRESHOLD || Width > DLV1_SIZE_THRESHOLD || Height > DLV1_SIZE_THRESHOLD)
            {
                format = "DLV2";
            }

            return format;
        }

        public byte[] GetLevelData()
        {
            string format = GetFormatIdentifier();

            List<byte> levelData = [.. Encoding.UTF8.GetBytes(format)];

            levelData.Add((byte)Width);
            levelData.Add((byte)Height);
            levelData.Add((byte)GetUsedPalette().Length);

            for (int i = 0; i < Palette.Length; i++)
            {
                levelData.Add(Palette[i][(int)ColourChannelIndex.BLUE]);
                levelData.Add(Palette[i][(int)ColourChannelIndex.GREEN]);
                levelData.Add(Palette[i][(int)ColourChannelIndex.RED]);
                levelData.Add(Palette[i][(int)ColourChannelIndex.ALPHA]);
            }

            foreach (int i in Data)
            {
                levelData.Add((byte)i);
            }

            return [.. levelData];
        }

        public byte[] PackPixelData()
        {
            List<byte> pixels = [];

            for (int i = 0; i < Data.Length; i += 2)
            {
                byte high = (byte)(Data[i] << BIT_PER_PIXEL);
                byte low = Data[i + 1];
                byte mixed = (byte)(high | low);

                pixels.Add(mixed);
            }

            return [.. pixels];
        }

        public byte[] GetUsedPalette()
        {
            List<byte> colours = [];

            foreach (byte pixel in Data)
            {
                if (!colours.Contains(pixel))
                {
                    colours.Add(pixel);
                }
            }

            return [.. colours];
        }

        private int LoadFromImage(string url)
        {
            if (File.Exists(url))
            {
                byte[] imgData = File.ReadAllBytes(url);

                Int32 pixelDataAddress = BitConverter.ToInt32(imgData, PIXEL_DATA_ADDRESS_OFFSET);
                Int32 dibSize = BitConverter.ToInt32(imgData, DIB_HEADER_OFFSET);
                Int16 bpp = BitConverter.ToInt16(imgData, BPP_OFFSET);
                Int32 compression = BitConverter.ToInt32(imgData, COMPRESSION_OFFSET);

                Width = BitConverter.ToInt32(imgData, WIDTH_OFFSET);
                Height = BitConverter.ToInt32(imgData, HEIGHT_OFFSET);

                if (
                    bpp != BIT_PER_PIXEL ||
                    dibSize != BITMAP_INFO_HEADER_SIZE ||
                    compression != BI_RGB_COMPRESSION ||
                    Width < MIN_CANVAS_SIZE ||
                    Width > MAX_CANVAS_SIZE ||
                    Height < MIN_CANVAS_SIZE ||
                    Height > MAX_CANVAS_SIZE
                )
                {
                    return ERR_INVALID_FORMAT;
                }


                Palette = new byte[(int)Math.Pow(2, bpp)][];

                for (int i = 0; i < Palette.Length; i++)
                {
                    Palette[i] = new byte[COLOUR_CHANNELS];

                    Palette[i] = [
                        imgData[COLOUR_TABLE_OFFSET + (i * BIT_PER_PIXEL) + (int)ColourChannelIndex.BLUE],
                        imgData[COLOUR_TABLE_OFFSET + (i * BIT_PER_PIXEL) + (int)ColourChannelIndex.GREEN],
                        imgData[COLOUR_TABLE_OFFSET + (i * BIT_PER_PIXEL) + (int)ColourChannelIndex.RED],
                        imgData[COLOUR_TABLE_OFFSET + (i * BIT_PER_PIXEL) + (int)ColourChannelIndex.ALPHA]
                    ];
                }

                Data = new byte[Width * Height];

                for (int i = 0; i < (Width / PIXEL_PER_BYTE) * Height; i++)
                {
                    Data[i * PIXEL_PER_BYTE + 0] = (byte)((imgData[pixelDataAddress + i] & 0b11110000) >> BIT_PER_PIXEL);
                    Data[i * PIXEL_PER_BYTE + 1] = (byte)((imgData[pixelDataAddress + i] & 0b00001111) >> 0);
                }

                Data = Data.Reverse().ToArray();
            }

            return 0;
        }

        public bool CoordsInBounds(int x, int y)
        {
            return (x >= 0 && x < Width && y >= 0 && y < Height);
        }

        public void SetPixel(int x, int y, int colour = 0)
        {
            if (!CoordsInBounds(x, y))
            {
                return;
            }

            Data[x + y * Width] = (byte)colour;
        }

        public void DrawLine(int x1, int y1, int x2, int y2, int colour = 0, bool finalize = false)
        {
            if (temp != null)
            {
                Array.Copy(temp, Data, Data.Length);
            } else
            {
                temp = new byte[Data.Length];

                Array.Copy(Data, temp, Data.Length);
            }

            Bresenham(x1, y1, x2, y2, colour);

            if (finalize)
            {
                temp = null;
            }
        }

        private void Bresenham(int x1, int y1, int x2, int y2, int colour)
        {
            int dx = Math.Abs(x2 - x1);
            int sx = (x1 < x2) ? 1 : -1;
            int dy = -1 * Math.Abs(y2 - y1);
            int sy = (y1 < y2) ? 1 : -1;
            int err = dx + dy;

            while (true)
            {
                SetPixel(x1, y1, colour);

                if (x1 == x2 && y1 == y2)
                {
                    break;
                }

                int err2 = err << 1;

                if (err2 >= dy)
                {
                    if (x1 == x2)
                    {
                        break;
                    }

                    err += dy;
                    x1 += sx;
                }

                if (err2 <= dx)
                {
                    if (y1 == y2)
                    {
                        break;
                    }

                    err += dx;
                    y1 += sy;
                }
            }
        }

        public void DrawRect(int x1, int y1, int x2, int y2, int colour = 0, bool finalize = false)
        {
            if (temp != null)
            {
                Array.Copy(temp, Data, Data.Length);
            }
            else
            {
                temp = new byte[Data.Length];

                Array.Copy(Data, temp, Data.Length);
            }

            for (int y = Math.Min(y1, y2); y <= Math.Max(y1, y2); y++)
            {
                for (int x = Math.Min(x1, x2); x <= Math.Max(x1, x2); x++)
                {
                    if (x == x1 || x == x2 || y == y1 || y == y2)
                    {
                        SetPixel(x, y, colour);
                    }
                }
            }

            if (finalize)
            {
                temp = null;
            }
        }

        public void DrawEllipse(int rx, int ry, int cx, int cy, int colour = 0, bool finalize = false)
        {
            if (temp != null)
            {
                Array.Copy(temp, Data, Data.Length);
            }
            else
            {
                temp = new byte[Data.Length];

                Array.Copy(Data, temp, Data.Length);
            }

            MidPtEllipse(rx, ry, cx, cy, colour);

            if (finalize)
            {
                temp = null;
            }
        }

        private void MidPtEllipse(int rx, int ry, int cx, int cy, int colour)
        {
            double x = 0;
            double y = ry;

            double dx = 2 * ry * ry * x;
            double dy = 2 * rx * rx * y;

            double d1 = (ry * ry) - (rx * rx * ry) + (.25f * rx * rx);

            while (dx < dy)
            {
                SetPixel((int)(x + cx), (int)(y + cy), colour);
                SetPixel((int)(-x + cx), (int)(y + cy), colour);
                SetPixel((int)(x + cx), (int)(-y + cy), colour);
                SetPixel((int)(-x + cx), (int)(-y + cy), colour);

                if (d1 < 0)
                {
                    x++;

                    dx += 2 * ry * ry;
                    d1 += dx + ry * ry;
                } else
                {
                    x++;
                    y--;
                    dx += 2 * ry * ry;
                    dy -= 2 * rx * rx;
                    d1 += dx - dy + ry * ry;
                }
            }

            double d2 = ((ry * ry) * ((x + .5f) * (x + .5f))) + ((rx * rx) * ((y - 1) * (y - 1))) - (rx * rx * ry * ry);

            while (y >= 0)
            {
                SetPixel((int)(x + cx), (int)(y + cy), colour);
                SetPixel((int)(-x + cx), (int)(y + cy), colour);
                SetPixel((int)(x + cx), (int)(-y + cy), colour);
                SetPixel((int)(-x + cx), (int)(-y + cy), colour);

                if (d2 > 0)
                {
                    y--;
                    dy -= 2 * rx * rx;
                    d2 += rx * rx - dy;
                } else
                {
                    y--;
                    x++;
                    dx += 2 * ry * ry;
                    dy -= 2 * rx * rx;
                    d2 += dx - dy + rx * rx;
                }
            }
        }

        public void FloodFill(int x, int y, int colour = 0)
        {
            Queue<int[]> toVisit = [];

            toVisit.Enqueue([x, y]);

            int target = -1;

            while (toVisit.Count > 0)
            {
                int[] curPos = toVisit.Dequeue();

                if (curPos[0] < 0 || curPos[0] >= Width || curPos[1] < 0 || curPos[1] >= Height)
                {
                    continue;
                }

                if (target < 0)
                {
                    target = Data[curPos[0] + curPos[1] * Width];
                }

                int idx = curPos[0] + curPos[1] * Width;

                if (colour == Data[idx])
                {
                    continue;
                }

                if (Data[idx] == target)
                {
                    Data[idx] = (byte)colour;

                    toVisit.Enqueue([curPos[0] + 0, curPos[1] - 1]);
                    toVisit.Enqueue([curPos[0] - 1, curPos[1] + 0]);
                    toVisit.Enqueue([curPos[0] + 1, curPos[1] + 0]);
                    toVisit.Enqueue([curPos[0] + 0, curPos[1] + 1]);
                } else
                {
                    continue;
                }
            }
        }
    }
}
