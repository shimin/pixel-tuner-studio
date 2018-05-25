using MediaToolkit;
using MediaToolkit.Model;
using MediaToolkit.Options;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace PixelTuner
{
    /// <summary>
    /// Логика взаимодействия для MainWindow.xaml
    /// </summary>
    public partial class VideoImportWindow : Window, INotifyPropertyChanged
    {
        string _sourcePath;
        public string SourcePath
        {
            get { return _sourcePath; }
            set
            {
                _sourcePath = value;
                NotifyPropertyChanged("SourcePath");
            }
        }

        string _destPath;
        public string DestPath
        {
            get
            {
                return _destPath;
            }
            set
            {
                _destPath = value;
                NotifyPropertyChanged("DestPath");
            }
        }

        string _thumbnailPath;
        public string ThumbnailPath
        {
            get
            {
                return _thumbnailPath;
            }
            set
            {
                _thumbnailPath = value;
                NotifyPropertyChanged("ThumbnailPath");
            }
        }

        string _fps;
        public string Fps
        {
            get { return _fps; }
            set { _fps = value; NotifyPropertyChanged("Fps"); }
        }

        string _size;
        public string Size
        {
            get { return _size; }
            set { _size = value; NotifyPropertyChanged("Size"); }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void NotifyPropertyChanged(String propertyName)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        public VideoImportWindow()
        {
            DataContext = this;
            InitializeComponent();
            Closing += VideoImportWindow_Closing;
        }

        private void VideoImportWindow_Closing(object sender, CancelEventArgs e)
        {
            bw.CancelAsync();
            bw.Dispose();
        }

        public void UpdateInfo()
        {
            if (!string.IsNullOrEmpty(_sourcePath))
            {
                using (var engine = new Engine())
                {
                    var inputFile = new MediaFile { Filename = SourcePath };

                    var outputFile = new MediaFile
                    {
                        Filename = System.IO.Path.Combine(System.IO.Path.GetTempFileName().Replace(".tmp", ".bmp"))
                    };

                    engine.GetMetadata(inputFile);
                    var options = new ConversionOptions
                    {
                        Seek = TimeSpan.FromMilliseconds(0)
                    };

                    engine.GetThumbnail(inputFile, outputFile, options);
                    ThumbnailPath = outputFile.Filename;
                    Fps = inputFile.Metadata.VideoData.Fps.ToString();
                    Size = inputFile.Metadata.VideoData.FrameSize;

                    DestPath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), 
                        System.IO.Path.GetFileNameWithoutExtension(inputFile.Filename));

                    if (!System.IO.Directory.Exists(DestPath))
                        System.IO.Directory.CreateDirectory(DestPath);
                    else
                    {
                        var files = System.IO.Directory.GetFiles(DestPath);
                        foreach (string f in files)
                        {
                            try {
                                System.IO.File.Delete(f);
                            }
                            catch (Exception ex) {
                                Console.WriteLine(ex.Message);
                            }
                        }
                    }
                }
            }
        }

        #region Bmp import
        
        [System.Runtime.InteropServices.DllImport("gdi32.dll")]
        public static extern bool DeleteObject(IntPtr hObject);

        public void SaveGif(string GifFile)
        {
            try
            {
                var gEnc = new GifBitmapEncoder();
                string[] Files = Directory.GetFiles(DestPath, "*.bmp");
                foreach (var f in Files)
                {
                    var bmpImage = (System.Drawing.Bitmap)System.Drawing.Bitmap.FromFile(f, true);
                    var bmp = bmpImage.GetHbitmap();
                    var src = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                        bmp,
                        IntPtr.Zero,
                        Int32Rect.Empty,
                        BitmapSizeOptions.FromEmptyOptions());
                    gEnc.Frames.Add(BitmapFrame.Create(src));

                    DeleteObject(bmp); // recommended, handle memory leak
                }

                using (FileStream fs = new FileStream(GifFile, FileMode.Create))
                {
                    gEnc.Save(fs);
                }
            }
            catch (Exception ex)
            {
                if (File.Exists(GifFile))
                    File.Delete(GifFile);
            }
        }

        private byte[] GifAnimation = { 33, 255, 11, 78, 69, 84, 83, 67, 65, 80, 69, 50, 46, 48, 3, 1, 0, 0, 0 };
        private byte[] Delay = { 0, 0 };
        public void WriteGifImg(byte[] B, BinaryWriter BW)
        {
            B[785] = Delay[0]; //5 secs delay
            B[786] = Delay[1];
            B[798] = (byte)(B[798] | 0X87);
            BW.Write(B, 781, 18);
            BW.Write(B, 13, 768);
            BW.Write(B, 799, B.Length - 800);
        }

        //public void SaveGif(string path)
        //{
        //    string[] Files = Directory.GetFiles(DestPath, "*.bmp");
        //    MemoryStream MS = new MemoryStream();
        //    BinaryReader BR = new BinaryReader(MS);
        //    BinaryWriter BW = new BinaryWriter(new FileStream(path, FileMode.Create));
        //    System.Drawing.Image.FromFile(Files[0]).Save(MS, System.Drawing.Imaging.ImageFormat.Gif);
        //    byte[] B = MS.ToArray();
        //    B[10] = (byte)(B[10] & 0X78); //No global color table.
        //    BW.Write(B, 0, 13);
        //    BW.Write(GifAnimation);
        //    WriteGifImg(B, BW);
        //    for (int I = 1; I < Files.Length; I++)
        //    {
        //        MS.SetLength(0);
        //        System.Drawing.Image.FromFile(Files[I]).Save(MS, System.Drawing.Imaging.ImageFormat.Gif);
        //        B = MS.ToArray();
        //        WriteGifImg(B, BW);
        //    }
        //    BW.Write(B[B.Length - 1]);
        //    BW.Close();
        //    MS.Dispose();
        //}
        
        #endregion
        
        public void GetFrames(object sender, DoWorkEventArgs e)
        {
            var inputFile = new MediaFile { Filename = SourcePath };

            int i = 0;
            int interval = 0;

            using (var engine = new Engine())
            {
                engine.GetMetadata(inputFile);
                engine.ConvertProgressEvent += ConvertProgressEvent;
                
                interval = (int)(1000 / inputFile.Metadata.VideoData.Fps);

                int w = 0;
                int h = 0;

                boxProgress.Dispatcher.Invoke((Action) (() => { 
                    boxProgress.Maximum = (int)(inputFile.Metadata.Duration.TotalMilliseconds / interval);

                    if (!string.IsNullOrEmpty(boxWidth.Text) && !string.IsNullOrEmpty(boxHeight.Text))
                    {
                        int.TryParse(boxWidth.Text, out w);
                        int.TryParse(boxHeight.Text, out h);
                    }
                }));
                
                do
                {
                    var outputFile = new MediaFile {
                        Filename = System.IO.Path.Combine(DestPath, "out_" + i.ToString("D4") + ".bmp")
                    };

                    var options = new ConversionOptions { Seek = TimeSpan.FromMilliseconds(interval * i) };
                    if (w > 0 && h > 0)
                    {
                        options.ThumbnailWidth = w;
                        options.ThumbnailHeight = h;
                    }

                    engine.GetThumbnail(inputFile, outputFile, options);
                    i++;
                } while (interval * i < inputFile.Metadata.Duration.TotalMilliseconds);
            }
        }
        
        private void ConvertProgressEvent(object sender, ConvertProgressEventArgs e)
        {
            Dispatcher.Invoke((Action)(() =>
            {
                boxProgress.Value++;
                if (boxProgress.Maximum - 1 == boxProgress.Value)
                {
                    DialogResult = true;
                    Close();
                }
            }));
        }

        BackgroundWorker bw = new BackgroundWorker();

        private void boxSetDest_Click(object sender, RoutedEventArgs e)
        {
            if (bw.IsBusy) return;
            bw.DoWork += GetFrames;
            bw.RunWorkerAsync();
            boxWidth.IsEnabled = false;
            boxHeight.IsEnabled = false;
        }
    }
}
