using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace Common.Imaging
{
    public static class ImageUtilities
    {
        /// <summary>
        /// A quick lookup for getting image encoders
        /// </summary>
        private static Dictionary<string, ImageCodecInfo> encoders = null;

        /// <summary>
        /// A quick lookup for getting image encoders
        /// </summary>
        public static Dictionary<string, ImageCodecInfo> Encoders
        {
            //get accessor that creates the dictionary on demand
            get
            {
                //if the quick lookup isn't initialised, initialise it
                if (encoders == null)
                {
                    encoders = new Dictionary<string, ImageCodecInfo>();
                }

                //if there are no codecs, try loading them
                if (encoders.Count == 0)
                {
                    //get all the codecs
                    foreach (ImageCodecInfo codec in ImageCodecInfo.GetImageEncoders())
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
        /// Resize the image to the specified width and height.
        /// </summary>
        /// <param name="image">The image to resize.</param>
        /// <param name="width">The width to resize to.</param>
        /// <param name="height">The height to resize to.</param>
        /// <returns>The resized image.</returns>
        public static System.Drawing.Bitmap ResizeImage(System.Drawing.Image image, int width, int height)
        {
            //a holder for the result
            Bitmap result = new Bitmap(width, height);
            result.SetResolution(72, 72);
            //set the resolutions the same to avoid cropping due to resolution differences
            result.SetResolution(image.HorizontalResolution, image.VerticalResolution);

            //use a graphics object to draw the resized image into the bitmap
            using (Graphics graphics = Graphics.FromImage(result))
            {
                //set the resize quality modes to high quality
                graphics.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
                graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.None;
                //draw the image into the target bitmap
                graphics.DrawImage(image, 0, 0, result.Width, result.Height);
            }

            //return the resulting bitmap
            return result;
        }

        /// <summary>
        /// Sharpens the specified image.
        /// </summary>
        /// <param name="image">The image.</param>
        /// <param name="strength">The strength.</param>
        /// <returns></returns>
        public static Bitmap Sharpen(Image image, double strength)
        {
            var bitmap = image as Bitmap;
            if (bitmap != null)
            {
                var sharpenImage = bitmap.Clone() as Bitmap;

                int width = image.Width;
                int height = image.Height;

                // Create sharpening filter.
                const int filterSize = 5;

                var filter = new double[,]
                {
                    {-1, -1, -1, -1, -1},
                    {-1,  2,  2,  2, -1},
                    {-1,  2, 16,  2, -1},
                    {-1,  2,  2,  2, -1},
                    {-1, -1, -1, -1, -1}
                };

                double bias = 1.0 - strength;
                double factor = strength / 16.0;

                const int s = filterSize / 2;

                var result = new Color[image.Width, image.Height];

                // Lock image bits for read/write.
                if (sharpenImage != null)
                {
                    BitmapData pbits = sharpenImage.LockBits(new Rectangle(0, 0, width, height),
                                                                ImageLockMode.ReadWrite,
                                                                PixelFormat.Format24bppRgb);

                    // Declare an array to hold the bytes of the bitmap.
                    int bytes = pbits.Stride * height;
                    var rgbValues = new byte[bytes];

                    // Copy the RGB values into the array.
                    System.Runtime.InteropServices.Marshal.Copy(pbits.Scan0, rgbValues, 0, bytes);

                    int rgb;
                    // Fill the color array with the new sharpened color values.
                    for (int x = s; x < width - s; x++)
                    {
                        for (int y = s; y < height - s; y++)
                        {
                            double red = 0.0, green = 0.0, blue = 0.0;

                            for (int filterX = 0; filterX < filterSize; filterX++)
                            {
                                for (int filterY = 0; filterY < filterSize; filterY++)
                                {
                                    int imageX = (x - s + filterX + width) % width;
                                    int imageY = (y - s + filterY + height) % height;

                                    rgb = imageY * pbits.Stride + 3 * imageX;

                                    red += rgbValues[rgb + 2] * filter[filterX, filterY];
                                    green += rgbValues[rgb + 1] * filter[filterX, filterY];
                                    blue += rgbValues[rgb + 0] * filter[filterX, filterY];
                                }

                                rgb = y * pbits.Stride + 3 * x;

                                int r = Math.Min(Math.Max((int)(factor * red + (bias * rgbValues[rgb + 2])), 0), 255);
                                int g = Math.Min(Math.Max((int)(factor * green + (bias * rgbValues[rgb + 1])), 0), 255);
                                int b = Math.Min(Math.Max((int)(factor * blue + (bias * rgbValues[rgb + 0])), 0), 255);

                                result[x, y] = Color.FromArgb(r, g, b);
                            }
                        }
                    }

                    // Update the image with the sharpened pixels.
                    for (int x = s; x < width - s; x++)
                    {
                        for (int y = s; y < height - s; y++)
                        {
                            rgb = y * pbits.Stride + 3 * x;

                            rgbValues[rgb + 2] = result[x, y].R;
                            rgbValues[rgb + 1] = result[x, y].G;
                            rgbValues[rgb + 0] = result[x, y].B;
                        }
                    }

                    // Copy the RGB values back to the bitmap.
                    System.Runtime.InteropServices.Marshal.Copy(rgbValues, 0, pbits.Scan0, bytes);
                    // Release image bits.
                    sharpenImage.UnlockBits(pbits);
                }

                return sharpenImage;
            }
            return null;
        }


        /// <summary>
        /// Resize the image to the specified width and height.
        /// </summary>
        /// <param name="image">The image to resize.</param>
        /// <param name="width">The width to resize to.</param>
        /// <param name="height">The height to resize to.</param>
        /// <returns>The resized image.</returns>
        public static System.Drawing.Bitmap CropImage(System.Drawing.Image image, int left, int top, int right, int bottom)
        {
            //a holder for the result
            Bitmap result = new Bitmap(right - left, bottom - top);
            result.SetResolution(72, 72);
            //set the resolutions the same to avoid cropping due to resolution differences
            result.SetResolution(image.HorizontalResolution, image.VerticalResolution);

            //use a graphics object to draw the resized image into the bitmap
            using (Graphics graphics = Graphics.FromImage(result))
            {
                //set the resize quality modes to high quality
                graphics.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
                graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.None;
                //draw the image into the target bitmap
                Rectangle srcRect = new Rectangle(left, top, right - left, bottom - top);
                graphics.DrawImage(image, 0, 0, srcRect, GraphicsUnit.Pixel);
            }

            //return the resulting bitmap
            return result;
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
        public static void SaveJpeg(string path, Image image, int quality)
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
            EncoderParameter qualityParam = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, quality);
            //get the jpeg codec
            ImageCodecInfo jpegCodec = GetEncoderInfo("image/jpeg");

            //create a collection of all parameters that we will pass to the encoder
            EncoderParameters encoderParams = new EncoderParameters(1);
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
        public static void SaveBmp(string path, Image image)
        {
            //get the bmp codec
            ImageCodecInfo bmpCodec = GetEncoderInfo("image/bmp");

            //create a collection of all parameters that we will pass to the encoder
            EncoderParameters encoderParams = new EncoderParameters(1);
            encoderParams.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, 100L);
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
        public static ImageCodecInfo GetEncoderInfo(string mimeType)
        {
            //do a case insensitive search for the mime type
            string lookupKey = mimeType.ToLower();

            //the codec to return, default to null
            ImageCodecInfo foundCodec = null;

            //if we have the encoder, get it to return
            if (Encoders.ContainsKey(lookupKey))
            {
                //pull the codec from the lookup
                foundCodec = Encoders[lookupKey];
            }

            return foundCodec;
        }
    }
}
