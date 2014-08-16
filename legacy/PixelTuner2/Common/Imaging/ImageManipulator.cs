using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using System.Windows;

namespace Common.Imaging
{
    public class ImageManipulator
    {
        static void Main(string[] args)
        {
            if (args.Length < 3)
            {
                Usage();
                return;
            }

            string filename = args[0];

            switch (args[1].ToLower())
            {
                case "rotate":
                    switch (args[2].ToLower())
                    {
                        case "left":
                            RotateImage(filename, -90);
                            break;
                        case "right":
                            RotateImage(filename, 90);
                            break;
                        default:
                            Usage();
                            break;
                    }
                    break;

                case "flip":
                    switch (args[2].ToLower())
                    {
                        case "horizontally":
                            FlipImage(filename, true);
                            break;
                        case "vertically":
                            FlipImage(filename, false);
                            break;
                        default:
                            Usage();
                            break;
                    }
                    break;

                case "crop":
                    if (args[2].ToLower() != "square")
                    {
                        Usage();
                        return;
                    }
                    CropImage(filename);
                    break;

                case "resize":
                    if (args.Length < 4)
                    {
                        Usage();
                        return;
                    }
                    int width = int.Parse(args[2]);
                    int height = int.Parse(args[3]);
                    ResizeImage(filename, width, height);
                    break;

                default:
                    Usage();
                    break;
            }
        }

        private static void Usage()
        {
            Console.WriteLine("\nUsage: ChangeImage <filename> command\n");
            Console.WriteLine("Commands:");
            Console.WriteLine("  rotate [left|right]");
            Console.WriteLine("  flip [horizontally|vertically]");
            Console.WriteLine("  crop square");
            Console.WriteLine("  resize <width> <height>");
        }

        private static void ResizeImage(string filename, int width, int height)
        {
            string extension = Path.GetExtension(filename);
            string rootFilename = Path.GetDirectoryName(filename)
                + @"\" + Path.GetFileNameWithoutExtension(filename);

            // load the file data
            byte[] imageBytes = LoadImageData(filename);

            // decode the image to the requested width and height
            BitmapSource imageSource = CreateImage(imageBytes, width, height);

            // encode the image using the original format
            byte[] encodedBytes = GetEncodedImageData(imageSource, extension);

            // save the modified image
            filename = rootFilename + "Resized" + extension;
            SaveImageData(encodedBytes, filename);

            Console.WriteLine("Resized image to {0} x {1} and saved as {2}.\n",
                imageSource.PixelWidth, imageSource.PixelHeight, filename);
        }

        private static void CropImage(string filename)
        {
            string extension = Path.GetExtension(filename);
            string rootFilename = Path.GetDirectoryName(filename)
                + @"\" + Path.GetFileNameWithoutExtension(filename);

            // load the file data
            byte[] imageBytes = LoadImageData(filename);

            // decode the image to its natural size
            BitmapSource imageSource = CreateImage(imageBytes, 0, 0);

            // crop the image so that it is square
            bool isPortrait = imageSource.Height > imageSource.Width;
            int squareLength = Math.Min(imageSource.PixelWidth, imageSource.PixelHeight);
            imageSource = new CroppedBitmap(imageSource,
                new Int32Rect(isPortrait ? 0 : (int)((imageSource.PixelWidth - squareLength) / 2),
                              isPortrait ? (int)((imageSource.PixelHeight - squareLength) / 2) : 0,
                              squareLength, squareLength));

            // encode the image using the original format
            byte[] encodedBytes = GetEncodedImageData(imageSource, extension);

            // save the modified image
            filename = rootFilename + "Square" + extension;
            SaveImageData(encodedBytes, filename);

            Console.WriteLine("Cropped image to {0} x {1} and saved as {2}.\n",
                imageSource.PixelWidth, imageSource.PixelHeight, filename);
        }

        private static void RotateImage(string filename, double angle)
        {
            string extension = Path.GetExtension(filename);
            string rootFilename = Path.GetDirectoryName(filename)
                + @"\" + Path.GetFileNameWithoutExtension(filename);

            // load the file data
            byte[] imageBytes = LoadImageData(filename);

            // decode the image to its natural size
            BitmapSource imageSource = CreateImage(imageBytes, 0, 0);

            // apply a rotate transform to the image
            imageSource = new TransformedBitmap(imageSource, new RotateTransform(angle));

            // encode the image using the original format
            byte[] encodedBytes = GetEncodedImageData(imageSource, extension);

            // save the modified image
            filename = rootFilename + "Rotated" + extension;
            SaveImageData(encodedBytes, filename);

            Console.WriteLine("Rotated image {0} degrees and saved as {1}.\n",
                angle, filename);
        }

        private static void FlipImage(string filename, bool isHorizontalFlip)
        {
            string extension = Path.GetExtension(filename);
            string rootFilename = Path.GetDirectoryName(filename)
                + @"\" + Path.GetFileNameWithoutExtension(filename);

            // load the file data
            byte[] imageBytes = LoadImageData(filename);

            // decode the image to its natural size
            BitmapSource imageSource = CreateImage(imageBytes, 0, 0);

            // apply a rotate transform to the image
            imageSource = new TransformedBitmap(imageSource,
                new ScaleTransform(isHorizontalFlip ? -1 : 1, isHorizontalFlip ? 1 : -1));

            // encode the image using the original format
            byte[] encodedBytes = GetEncodedImageData(imageSource, extension);

            // save the modified image
            filename = rootFilename + "Flipped" + extension;
            SaveImageData(encodedBytes, filename);

            Console.WriteLine("Flipped image {0} and saved as {1}.\n",
                isHorizontalFlip ? "horizontally" : "vertically", filename);
        }

        private static byte[] LoadImageData(string filePath)
        {
            FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read);
            BinaryReader br = new BinaryReader(fs);
            byte[] imageBytes = br.ReadBytes((int)fs.Length);
            br.Close();
            fs.Close();
            return imageBytes;
        }

        private static void SaveImageData(byte[] imageData, string filePath)
        {
            FileStream fs = new FileStream(filePath, FileMode.Create, FileAccess.Write);
            BinaryWriter bw = new BinaryWriter(fs);
            bw.Write(imageData);
            bw.Close();
            fs.Close();
        }

        private static BitmapSource CreateImage(byte[] imageData, int decodePixelWidth, int decodePixelHeight)
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
            result.StreamSource = new MemoryStream(imageData);
            result.CreateOptions = BitmapCreateOptions.None;
            result.CacheOption = BitmapCacheOption.Default;
            result.EndInit();
            return result;
        }

        private static byte[] GetEncodedImageData(ImageSource image, string preferredFormat)
        {
            byte[] result = null;
            BitmapEncoder encoder = null;
            switch (preferredFormat.ToLower())
            {
                case ".jpg":
                case ".jpeg":
                    encoder = new JpegBitmapEncoder();
                    break;

                case ".bmp":
                    encoder = new BmpBitmapEncoder();
                    break;

                case ".png":
                    encoder = new PngBitmapEncoder();
                    break;

                case ".tif":
                case ".tiff":
                    encoder = new TiffBitmapEncoder();
                    break;

                case ".gif":
                    encoder = new GifBitmapEncoder();
                    break;

                case ".wmp":
                    encoder = new WmpBitmapEncoder();
                    break;
            }

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

    }
}
