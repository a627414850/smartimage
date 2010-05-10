﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Drawing;

namespace Orc.SmartImage
{
    public struct ByteConverter : IByteConverter<Byte>
    {
        public unsafe void Copy(byte* from, Argb32* to)
        {
            Byte data = *from;
            to->Blue = data;
            to->Green = data;
            to->Red = data;
            to->Alpha = 255;
        }

        public unsafe void Copy(Argb32* from, byte* to)
        {
            *to = (Byte)(from->Blue * 0.114 + from->Green * 0.587 + from->Red * 0.299);
        }

        public unsafe void Copy(Rgb24* from, byte* to)
        {
            *to = (Byte)(from->Blue * 0.114 + from->Green * 0.587 + from->Red * 0.299);
        }

        public unsafe void Copy(byte* from, ref byte to)
        {
            to = *from;
        }

        public unsafe void Copy(ref byte from, byte* to)
        {
            *to = from;
        }
    }

    public partial class ImageU8 : UnmanagedImage<Byte>
    {
        public unsafe Byte* Start { get { return (Byte*)this.StartIntPtr; } }

        public unsafe ImageU8(Int32 width, Int32 height)
            : base(width, height)
        {
        }

        public ImageU8(Bitmap map)
            : base(map)
        {
        }

        protected override IByteConverter<Byte> CreateByteConverter()
        {
            return new ByteConverter();
        }

        public override IImage Clone()
        {
            ImageU8 img = new ImageU8(this.Width, this.Height);
            img.CloneFrom(this);
            return img;
        }

        public unsafe void Remove(Byte frontPixel, Byte replacedPixel)
        {
            List<Int32> mask = new List<int>(this.Length);

            for (int r = 1; r < this.Height - 1; r++)
            {
                for (int c = 1; c < this.Width -1; c++)
                {
                    int index = r * this.Width + c;
                    Byte b00 = Start[(r - 1) * this.Width + c -1 ];
                    Byte b01 = Start[(r - 1) * this.Width + c];
                    Byte b02 = Start[(r - 1) * this.Width + c + 1];
                    Byte b10 = Start[r * this.Width + c -1];
                    Byte b11 = Start[r * this.Width + c];
                    Byte b12 = Start[r * this.Width + c + 1];
                    Byte b20 = Start[(r + 1) * this.Width + c -1];
                    Byte b21 = Start[(r + 1) * this.Width + c];
                    Byte b22 = Start[(r + 1) * this.Width + c + 1];
                    if (b00 == frontPixel
                        && b01 == frontPixel
                        && b02 == frontPixel
                        && b10 == frontPixel
                        && b11 == frontPixel
                        && b12 == frontPixel
                        && b20 == frontPixel
                        && b21 == frontPixel
                        && b22 == frontPixel)
                    {
                        mask.Add(r * this.Width + c);
                    }
                }
            }

            foreach (var item in mask)
            {
                Start[item] = replacedPixel;
            }
        }
    }
}
