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

        public unsafe void Invert()
        {
            Byte* p = this.Start;
            Byte* end = p + this.Length;
            while (p != end)
            {
                *p = (Byte)(255 - *p);
                p++;
            }
        }

        /// <summary>
        /// 计算八联结的联结数，计算公式为：
        ///     (p6 - p6*p7*p0) + sigma(pk - pk*p(k+1)*p(k+2)), k = {0,2,4)
        /// </summary>
        /// <param name="list"></param>
        /// <returns></returns>
        private unsafe Int32 DetectConnectivity(Int32* list)
        {
            Int32 count = list[6] - list[6] * list[7] * list[0];
            count += list[0] - list[0] * list[1] * list[2];
            count += list[2] - list[2] * list[3] * list[4];
            count += list[4] - list[4] * list[5] * list[6];
            return count;
        }

        private unsafe void FillNeighbors(Byte* p, Int32* list, Int32 width, Byte foreground = 255)
        {
            // list 存储的是补集，即前景点为0，背景点为1，以方便联结数的计算

            list[0] = p[1] == foreground ? 0 : 1;
            list[1] = p[1 - width] == foreground ? 0 : 1;
            list[2] = p[-width] == foreground ? 0 : 1;
            list[3] = p[-1 - width] == foreground ? 0 : 1;
            list[4] = p[-1] == foreground ? 0 : 1;
            list[5] = p[-1 + width] == foreground ? 0 : 1;
            list[6] = p[width] == foreground ? 0 : 1;
            list[7] = p[1 + width] == foreground ? 0 : 1;
        }

        /// <summary>
        /// 使用 hilditch 算法进行细化
        /// </summary>
        public unsafe void Thinning(Byte foreground = 255)
        {
            Byte* start = this.Start;
            Int32 width = this.Width;
            Int32 height = this.Height;
            Int32* list = stackalloc Int32[8];
            Byte background = (Byte)(255 - foreground);
            Int32 length = this.Length;

            using (ImageU8 mask = new ImageU8(this.Width, this.Height))
            {
                mask.Fill(0);

                Boolean loop = true;
                while (loop == true)
                {
                    loop = false;
                    for (Int32 r = 1; r < height - 1; r++)
                    {
                        for (Int32 c = 1; c < width - 1; c++)
                        {
                            Byte* p = start + r * width + c;

                            // 条件1：p 必须是前景点
                            if (*p != foreground) continue;

                            //  p3  p2  p1
                            //  p4  p   p0
                            //  p5  p6  p7
                            // list 存储的是补集，即前景点为0，背景点为1，以方便联结数的计算
                            FillNeighbors(p, list, width, foreground);

                            // 条件2：p0,p2,p4,p6 不皆为前景点
                            if (list[0] == 0 && list[2] == 0 && list[4] == 0 && list[6] == 0)
                                continue;

                            // 条件3: p0~p7至少两个是前景点
                            Int32 count = 0;
                            for (int i = 0; i < 8; i++)
                            {
                                count += list[i];
                            }

                            if (count > 6) continue;

                            // 条件4：联结数等于1
                            if (DetectConnectivity(list) != 1) continue;

                            // 条件5: 假设p2已标记删除，则令p2为背景，不改变p的联结数
                            if (mask[r - 1, c] == 1)
                            {
                                list[2] = 1;
                                if (DetectConnectivity(list) != 1)
                                    continue;
                                list[2] = 0;
                            }

                            // 条件6: 假设p4已标记删除，则令p4为背景，不改变p的联结数
                            if (mask[r, c - 1] == 1)
                            {
                                list[4] = 1;
                                if (DetectConnectivity(list) != 1)
                                    continue;
                            }
                            mask[r, c] = 1; // 标记删除
                            loop = true;
                        }
                    }

                    for (int i = 0; i < length; i++)
                    {
                        if (mask[i] == 1)
                        {
                            this[i] = background;
                        }
                    }
                }
            }
        }

        public unsafe void SkeletonizeByMidPoint(Byte foreground = 255)
        {
            using (ImageU8 mask = new ImageU8(this.Width, this.Height))
            {
                mask.Fill(0);
                Int32 width = this.Width;
                Int32 height = this.Height;
                for (Int32 r = 0; r < height; r++)
                {
                    Int32 lineStart = -1;
                    for (Int32 c = 0; c < width; c++)
                    {
                        if (this[r, c] == foreground)
                        {
                            if (lineStart == -1) lineStart = c;
                        }
                        else
                        {
                            if (lineStart != -1)
                            {
                                mask[r, (lineStart + c) / 2] = 1;
                                lineStart = -1;
                            }
                        }
                    }
                }

                for (Int32 c = 0; c < width; c++)
                {
                    Int32 lineStart = -1;
                    for (Int32 r = 0; r < height; r++)
                    {
                        if (this[r, c] == foreground)
                        {
                            if (lineStart == -1) lineStart = r;
                        }
                        else
                        {
                            if (lineStart != -1)
                            {
                                mask[(lineStart + r) / 2, c] = 1;
                                lineStart = -1;
                            }
                        }
                    }
                }

                Byte bg = (Byte)(255 - foreground);
                Int32 length = this.Length;
                for (int i = 0; i < length; i++)
                {
                    if (mask[i] == 1)
                    {
                        this[i] = foreground;
                    }
                    else
                    {
                        this[i] = bg;
                    }
                }
            }
        }

        public unsafe ImageInt32 ToImageInt32()
        {
            ImageInt32 img32 = new ImageInt32(this.Width, this.Height);
            Byte* start = this.Start;
            Byte* end = this.Start + this.Length;
            Int32* dst = img32.Start;
            while (start != end)
            {
                *dst = *start;
                start++;
                dst++;
            }
            return img32;
        }

        public void ApplyCannyEdgeDetector(byte lowThreshold = 20, byte highThreshold = 100)
        {

        }

        public void ApplyGaussianBlur(double sigma = 1.4, int size = 5)
        {
            ConvolutionKernel kernel = ConvolutionKernel.CreateGaussianKernel(sigma, size);
            this.ApplyConvolution(kernel);
        }
    }
}