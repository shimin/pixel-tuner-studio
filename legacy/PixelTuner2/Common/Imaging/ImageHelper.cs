using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using System.Windows;

using SD = System.Drawing;
using SDI = System.Drawing.Imaging;

namespace Common.Imaging
{
    public class ImageHelper
    {
        public static BitmapImage CreateImage(Uri path)
        {
            BitmapImage image = new BitmapImage();

            try
            {
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
                image.UriSource = path;
                image.EndInit();
            }
            catch
            {
                return null;
            }

            return image;
        }
        
        #region BitmapImage / GDI Bitmap conversion

        /// <summary>
        /// Converts wpf BitmapImage to GDI bitmap
        /// </summary>
        /// <typeparam name="EncoderType">type of encoder (such as BmpBitmapEncoder)</typeparam>
        /// <param name="bitmapImage">Source image</param>
        /// <returns>GDI Bitmap</returns>
        public static SD.Bitmap BitmapImageToBitmap<EncoderType>(BitmapSource bitmapImage)
            where EncoderType : BitmapEncoder, new()
        {
            using (MemoryStream outStream = new MemoryStream())
            {
                var enc = new EncoderType();
                enc.Frames.Add(BitmapFrame.Create(bitmapImage));
                enc.Save(outStream);
                SD.Bitmap bitmap = new SD.Bitmap(outStream);
                return new SD.Bitmap(bitmap);
            }
        }

        /// <summary>
        /// Converts GDI bitmap to wpf BitmapImage
        /// </summary>
        /// <param name="image">Source bitmap</param>
        /// <returns>returns BitmapImage</returns>
        public static BitmapImage BitmapImageFromBitmap(SD.Bitmap image)
        {
            MemoryStream ms = new MemoryStream();
            image.Save(ms, SDI.ImageFormat.Bmp);
            return BitmapImageFromBytes(ms, image.Width, image.Height);
        }
        
        #endregion

        #region BitmapImage / byte array conversion

        public static BitmapImage BitmapImageFromBytes(byte[] imageData, int decodePixelWidth, int decodePixelHeight)
        {
            MemoryStream ms = new MemoryStream(imageData);
            return BitmapImageFromBytes(ms, decodePixelWidth, decodePixelHeight);
        }

        public static BitmapImage BitmapImageFromBytes(Stream imageData, int decodePixelWidth, int decodePixelHeight)
        {
            if (imageData == null) return null;

            BitmapImage result = new BitmapImage();
            result.BeginInit();
            if (decodePixelWidth > 0)
            {
                result.DecodePixelWidth = decodePixelWidth;
            }
            if (decodePixelHeight > 0)
            {
                result.DecodePixelHeight = decodePixelHeight;
            }
            result.StreamSource = imageData;
            result.CreateOptions = BitmapCreateOptions.None;
            result.CacheOption = BitmapCacheOption.Default;
            result.EndInit();
            return result;
        }

        public static byte[] BitmapImageToBytes<EncoderType>(ImageSource image)
            where EncoderType : BitmapEncoder, new()
        {
            byte[] result = null;

            BitmapEncoder encoder = new EncoderType();
            if (image is BitmapSource)
            {
                MemoryStream stream = new MemoryStream();
                encoder.Frames.Add(BitmapFrame.Create(image as BitmapSource));
                encoder.Save(stream);
                stream.Seek(0, SeekOrigin.Begin);
                result = new byte[stream.Length];
                BinaryReader br = new BinaryReader(stream);
                br.Read(result, 0, (int)stream.Length);
                br.Close();
                stream.Close();
            }

            return result;
        }

        #endregion

        #region Paste from clipboard

        public static System.Windows.Media.ImageSource ImageFromClipboard()
        {
            MemoryStream ms = Clipboard.GetData("DeviceIndependentBitmap") as MemoryStream;
            if (ms != null)
            {
                byte[] dibBuffer = new byte[ms.Length];
                ms.Read(dibBuffer, 0, dibBuffer.Length);

                BITMAPINFOHEADER infoHeader =
                    BinaryStructConverter.FromByteArray<BITMAPINFOHEADER>(dibBuffer);

                int fileHeaderSize = Marshal.SizeOf(typeof(BITMAPFILEHEADER));
                int infoHeaderSize = infoHeader.biSize;
                int fileSize = fileHeaderSize + infoHeader.biSize + infoHeader.biSizeImage;

                BITMAPFILEHEADER fileHeader = new BITMAPFILEHEADER();
                fileHeader.bfType = BITMAPFILEHEADER.BM;
                fileHeader.bfSize = fileSize;
                fileHeader.bfReserved1 = 0;
                fileHeader.bfReserved2 = 0;
                fileHeader.bfOffBits = fileHeaderSize + infoHeaderSize + infoHeader.biClrUsed * 4;

                byte[] fileHeaderBytes =
                    BinaryStructConverter.ToByteArray<BITMAPFILEHEADER>(fileHeader);

                MemoryStream msBitmap = new MemoryStream();
                msBitmap.Write(fileHeaderBytes, 0, fileHeaderSize);
                msBitmap.Write(dibBuffer, 0, dibBuffer.Length);
                msBitmap.Seek(0, SeekOrigin.Begin);

                return BitmapFrame.Create(msBitmap);
            }
            return null;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 2)]
        private struct BITMAPFILEHEADER
        {
            public static readonly short BM = 0x4d42; // BM

            public short bfType;
            public int bfSize;
            public short bfReserved1;
            public short bfReserved2;
            public int bfOffBits;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct BITMAPINFOHEADER
        {
            public int biSize;
            public int biWidth;
            public int biHeight;
            public short biPlanes;
            public short biBitCount;
            public int biCompression;
            public int biSizeImage;
            public int biXPelsPerMeter;
            public int biYPelsPerMeter;
            public int biClrUsed;
            public int biClrImportant;
        }

        public static class BinaryStructConverter
        {
            public static T FromByteArray<T>(byte[] bytes) where T : struct
            {
                IntPtr ptr = IntPtr.Zero;
                try
                {
                    int size = Marshal.SizeOf(typeof(T));
                    ptr = Marshal.AllocHGlobal(size);
                    Marshal.Copy(bytes, 0, ptr, size);
                    object obj = Marshal.PtrToStructure(ptr, typeof(T));
                    return (T)obj;
                }
                finally
                {
                    if (ptr != IntPtr.Zero)
                        Marshal.FreeHGlobal(ptr);
                }
            }

            public static byte[] ToByteArray<T>(T obj) where T : struct
            {
                IntPtr ptr = IntPtr.Zero;
                try
                {
                    int size = Marshal.SizeOf(typeof(T));
                    ptr = Marshal.AllocHGlobal(size);
                    Marshal.StructureToPtr(obj, ptr, true);
                    byte[] bytes = new byte[size];
                    Marshal.Copy(ptr, bytes, 0, size);
                    return bytes;
                }
                finally
                {
                    if (ptr != IntPtr.Zero)
                        Marshal.FreeHGlobal(ptr);
                }
            }
        }


        #endregion

        #region System.Drawing

        #region Save/Load
        private static Dictionary<string, SDI.ImageCodecInfo> encoders = null;
        public static Dictionary<string, SDI.ImageCodecInfo> Encoders
        {
            //get accessor that creates the dictionary on demand
            get
            {
                //if the quick lookup isn't initialised, initialise it
                if (encoders == null)
                {
                    encoders = new Dictionary<string, SDI.ImageCodecInfo>();
                }

                //if there are no codecs, try loading them
                if (encoders.Count == 0)
                {
                    //get all the codecs
                    foreach (SDI.ImageCodecInfo codec in SDI.ImageCodecInfo.GetImageEncoders())
                    {
                        //add each codec to the quick lookup
                        encoders.Add(codec.MimeType.ToLower(), codec);
                    }
                }

                //return the lookup
                return encoders;
            }
        }

        /// <summary> 
        /// Saves an image as a jpeg image, with the given quality 
        /// </summary> 
        /// <param name="path">Path to which the image would be saved.</param> 
        /// <param name="quality">An integer from 0 to 100, with 100 being the 
        /// highest quality</param> 
        /// <exception cref="ArgumentOutOfRangeException">
        /// An invalid value was entered for image quality.
        /// </exception>
        public static void SaveJpeg(string path, SD.Image image, int quality)
        {
            //ensure the quality is within the correct range
            if ((quality < 0) || (quality > 100))
            {
                //create the error message
                string error = string.Format("Jpeg image quality must be between 0 and 100, with 100 being the highest quality.  A value of {0} was specified.", quality);
                //throw a helpful exception
                throw new ArgumentOutOfRangeException(error);
            }

            //create an encoder parameter for the image quality
            SDI.EncoderParameter qualityParam = new SDI.EncoderParameter(System.Drawing.Imaging.Encoder.Quality, quality);
            //get the jpeg codec
            SDI.ImageCodecInfo jpegCodec = GetEncoderInfo("image/jpeg");

            //create a collection of all parameters that we will pass to the encoder
            SDI.EncoderParameters encoderParams = new SDI.EncoderParameters(1);
            //set the quality parameter for the codec
            encoderParams.Param[0] = qualityParam;
            //save the image using the codec and the parameters
            image.Save(path, jpegCodec, encoderParams);
        }

        /// <summary> 
        /// Saves an image as a bmp image
        /// </summary> 
        /// <param name="path">Path to which the image would be saved.</param> 
        /// <exception cref="ArgumentOutOfRangeException">
        /// An invalid value was entered for image quality.
        /// </exception>
        public static void SaveBmp(string path, SD.Image image)
        {
            //get the bmp codec
            SDI.ImageCodecInfo bmpCodec = GetEncoderInfo("image/bmp");

            //create a collection of all parameters that we will pass to the encoder
            SDI.EncoderParameters encoderParams = new SDI.EncoderParameters(1);
            encoderParams.Param[0] = new SDI.EncoderParameter(System.Drawing.Imaging.Encoder.Quality, 100L);
            image.Save(path, bmpCodec, encoderParams);
        }

        public static void SaveImageData(byte[] imageData, string filePath)
        {
            FileStream fs = new FileStream(filePath, FileMode.Create, FileAccess.Write);
            BinaryWriter bw = new BinaryWriter(fs);
            bw.Write(imageData);
            bw.Close();
            fs.Close();
        }

        /// <summary> 
        /// Returns the image codec with the given mime type 
        /// </summary> 
        public static SDI.ImageCodecInfo GetEncoderInfo(string mimeType)
        {
            //do a case insensitive search for the mime type
            string lookupKey = mimeType.ToLower();

            //the codec to return, default to null
            SDI.ImageCodecInfo foundCodec = null;

            //if we have the encoder, get it to return
            if (Encoders.ContainsKey(lookupKey))
            {
                //pull the codec from the lookup
                foundCodec = Encoders[lookupKey];
            }

            return foundCodec;
        }
        #endregion

        #region GDI bitmap / byte array conversion

        public static byte[] ImageToBytes(SD.Bitmap src)
        {
            var rgbValues = new byte[0];

            SDI.BitmapData bd =
                src.LockBits(new SD.Rectangle(0, 0, src.Width, src.Height), SDI.ImageLockMode.ReadOnly, src.PixelFormat);

            IntPtr ptr = bd.Scan0;
            int bytes = bd.Stride * src.Height;
            rgbValues = new byte[bytes];

            System.Runtime.InteropServices.Marshal.Copy(ptr, rgbValues, 0, rgbValues.Length);

            src.UnlockBits(bd);

            return rgbValues;
        }

        public static SD.Bitmap ImageFromBytes(byte[] pixels, int w, int h, SDI.PixelFormat format)
        {
            SD.Bitmap b = new SD.Bitmap(w, h, format);

            SDI.BitmapData bd = b.LockBits(new SD.Rectangle(0, 0, b.Width, b.Height), SDI.ImageLockMode.WriteOnly, b.PixelFormat);
            System.Runtime.InteropServices.Marshal.Copy(pixels, 0, bd.Scan0, pixels.Length);
            b.UnlockBits(bd);

            return b;
        }

        #endregion

        /// <summary>
        /// Resize the image to the specified width and height.
        /// </summary>
        /// <param name="image">The image to resize.</param>
        /// <param name="width">The width to resize to.</param>
        /// <param name="height">The height to resize to.</param>
        /// <returns>The resized image.</returns>
        public static SD.Bitmap ResizeImage(SD.Image image, int width, int height)
        {
            //a holder for the result
            SD.Bitmap result = new SD.Bitmap(width, height);
            result.SetResolution(72, 72);
            //set the resolutions the same to avoid cropping due to resolution differences
            result.SetResolution(image.HorizontalResolution, image.VerticalResolution);

            //use a graphics object to draw the resized image into the bitmap
            using (SD.Graphics graphics = SD.Graphics.FromImage(result))
            {
                //set the resize quality modes to high quality
                graphics.CompositingQuality = SD.Drawing2D.CompositingQuality.HighQuality;
                graphics.InterpolationMode = SD.Drawing2D.InterpolationMode.HighQualityBicubic;
                graphics.PixelOffsetMode = SD.Drawing2D.PixelOffsetMode.HighQuality;
                graphics.SmoothingMode = SD.Drawing2D.SmoothingMode.None;
                //draw the image into the target bitmap
                graphics.DrawImage(image, 0, 0, result.Width, result.Height);
            }

            //return the resulting bitmap
            return result;
        }

        /// <summary>
        /// Resize the image to the specified width and height.
        /// </summary>
        /// <param name="image">The image to resize.</param>
        /// <param name="width">The width to resize to.</param>
        /// <param name="height">The height to resize to.</param>
        /// <returns>The resized image.</returns>
        public static SD.Bitmap CropImage(SD.Image image, int left, int top, int right, int bottom)
        {
            //a holder for the result
            SD.Bitmap result = new SD.Bitmap(right - left, bottom - top);
            result.SetResolution(72, 72);
            //set the resolutions the same to avoid cropping due to resolution differences
            result.SetResolution(image.HorizontalResolution, image.VerticalResolution);

            //use a graphics object to draw the resized image into the bitmap
            using (SD.Graphics graphics = SD.Graphics.FromImage(result))
            {
                //set the resize quality modes to high quality
                graphics.CompositingQuality = SD.Drawing2D.CompositingQuality.HighQuality;
                graphics.InterpolationMode = SD.Drawing2D.InterpolationMode.HighQualityBicubic;
                graphics.SmoothingMode = SD.Drawing2D.SmoothingMode.None;
                //draw the image into the target bitmap
                SD.Rectangle srcRect = new SD.Rectangle(left, top, right - left, bottom - top);
                graphics.DrawImage(image, 0, 0, srcRect, SD.GraphicsUnit.Pixel);
            }

            //return the resulting bitmap
            return result;
        }

        /// <summary>
        /// Makes image bigger by converting each pixel to square with specified side length
        /// </summary>
        /// <param name="source">Source bitmap</param>
        /// <param name="squareSize">Square side</param>
        /// <returns>Grown bitmap</returns>
        public static SD.Bitmap Pixelate(SD.Bitmap source, int squareSize)
        {
            SD.Bitmap result = new SD.Bitmap(source.Width * squareSize, source.Height * squareSize);
            using (SD.Graphics g = SD.Graphics.FromImage(result))
            {
                for (int r = 0; r < source.Height; r++)
                {
                    for (int c = 0; c < source.Width; c++)
                    {
                        SD.Color sc = source.GetPixel(c, r);
                        g.FillRectangle(new SD.SolidBrush(sc), c * squareSize, r * squareSize, squareSize, squareSize);
                    }
                }
            }
            return result;
        }

        #endregion


    }
}
