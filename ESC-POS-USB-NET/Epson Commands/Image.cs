using System.Collections;
using System;
using System.IO;
using ESC_POS_USB_NET.Interfaces.Command;
using System.Drawing;

namespace ESC_POS_USB_NET.EpsonCommands
{
    public class Image : IImage
    {
        private static BitmapData GetBitmapData(Bitmap bmp, int? customWidth = null)
        {
            var threshold = 127;
            var index = 0;

            int targetWidth = customWidth ?? 576; // Use custom width if provided, fallback to 576
            double scale = (double)(targetWidth / (double)bmp.Width);
            int xheight = (int)(bmp.Height * scale);
            int xwidth = (int)(bmp.Width * scale);
            var dimensions = xwidth * xheight;
            var dots = new BitArray(dimensions);

            for (var y = 0; y < xheight; y++)
            {
                for (var x = 0; x < xwidth; x++)
                {
                    var _x = (int)(x / scale);
                    var _y = (int)(y / scale);
                    var color = bmp.GetPixel(_x, _y);
                    var luminance = (int)(color.R * 0.3 + color.G * 0.59 + color.B * 0.11);
                    dots[index] = (luminance < threshold);
                    index++;
                }
            }

            return new BitmapData()
            {
                Dots = dots,
                Height = xheight,
                Width = xwidth
            };
        }

        byte[] IImage.Print(Bitmap image, int customWidth = 288)
        {
            // Specify the desired width here (e.g., half of 576 = 288)
            var data = GetBitmapData(image, customWidth);

            BitArray dots = data.Dots;
            byte[] width = BitConverter.GetBytes(data.Width);

            int offset = 0;
            MemoryStream stream = new MemoryStream();
            BinaryWriter bw = new BinaryWriter(stream);

            bw.Write((char)0x1B);
            bw.Write('@');

            bw.Write((char)0x1B);
            bw.Write('3');
            bw.Write((byte)24);

            while (offset < data.Height)
            {
                bw.Write((char)0x1B);
                bw.Write('*');         // bit-image mode
                bw.Write((byte)33);    // 24-dot double-density
                bw.Write(width[0]);  // width low byte
                bw.Write(width[1]);  // width high byte

                for (int x = 0; x < data.Width; ++x)
                {
                    for (int k = 0; k < 3; ++k)
                    {
                        byte slice = 0;
                        for (int b = 0; b < 8; ++b)
                        {
                            int y = (((offset / 8) + k) * 8) + b;
                            int i = (y * data.Width) + x;

                            bool v = false;
                            if (i < dots.Length)
                            {
                                v = dots[i];
                            }
                            slice |= (byte)((v ? 1 : 0) << (7 - b));
                        }

                        bw.Write(slice);
                    }
                }
                offset += 24;
                bw.Write((char)0x0A);
            }
            bw.Write((char)0x1B);
            bw.Write('3');
            bw.Write((byte)30);

            bw.Flush();
            byte[] bytes = stream.ToArray();
            bw.Dispose();
            return bytes;
        }
    }

    public class BitmapData
    {
        public BitArray Dots { get; set; }
        public int Height { get; set; }
        public int Width { get; set; }
    }
}

