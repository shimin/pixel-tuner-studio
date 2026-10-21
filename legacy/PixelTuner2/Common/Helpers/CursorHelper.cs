using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Drawing;

namespace Common.Helpers
{
    public class CursorHelper
    {
        public struct IconInfo
        {
            public bool fIcon;
            public int xHotspot;
            public int yHotspot;
            public IntPtr hbmMask;
            public IntPtr hbmColor;
        }
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetIconInfo(IntPtr hIcon, ref IconInfo pIconInfo);
        [DllImport("user32.dll")]
        public static extern IntPtr CreateIconIndirect(ref IconInfo icon);

        /// <summary>
        /// Create a cursor from a bitmap without resizing and with the specified
        /// hot spot
        /// </summary>
        public static Cursor CreateCursorNoResize(Bitmap bmp, int xHotSpot, int yHotSpot)
        {
            IntPtr ptr = bmp.GetHicon();
            IconInfo tmp = new IconInfo();
            GetIconInfo(ptr, ref tmp);
            tmp.xHotspot = xHotSpot;
            tmp.yHotspot = yHotSpot;
            tmp.fIcon = false;
            ptr = CreateIconIndirect(ref tmp);
            return new Cursor(ptr);
        }


        /// <summary>
        /// Create a 32x32 cursor from a bitmap, with the hot spot in the middle
        /// </summary>
        public static Cursor CreateCursor(Bitmap bmp, int size = 32, int xHotSpot = 16, int yHotSpot = 16)
        {
            IntPtr ptr = ((Bitmap)ResizeImage(bmp, size, size)).GetHicon();
            IconInfo tmp = new IconInfo();
            GetIconInfo(ptr, ref tmp);
            tmp.xHotspot = xHotSpot;
            tmp.yHotspot = yHotSpot;
            tmp.fIcon = false;
            ptr = CreateIconIndirect(ref tmp);
            return new Cursor(ptr);
        }

        private static Bitmap ResizeImage(Bitmap src, int targetWidth, int targetHeight)
        {
            Bitmap result = new Bitmap(targetWidth, targetHeight);
            var g = Graphics.FromImage(result);
            g.DrawImage(src, new Rectangle(0, 0, targetWidth, targetHeight), new Rectangle(0, 0, src.Width, src.Height), GraphicsUnit.Pixel);
            g.Dispose();
            return result;
        }
    }
}
