using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Windows.Media.Imaging;
//using PixelTuner.Drawing.Properties;

namespace PixelTuner.Drawing
{
    public partial class DrawControl : UserControl, IMessageFilter
    {
        Color GridColorLight = Color.FromArgb(255, 60, 60, 60);
        Image _tempImage = null;
        private Image ImageBuffer = null;
        bool _mouseSelecting = false;
        bool _startSelection = false;
        Rectangle _selection = new Rectangle(0, 0, 0, 0);
        Point _bufferCurrentPosition = new Point(0, 0);
       
        #region Поля
        private ConfigurationItem _item = null;
        /// <summary>
        /// Текущий обрабатываемый узор
        /// </summary>

        public void SetItem(ConfigurationItem item)
        {
            if (item == null) return;
            boxCanvas.Paint -= boxCanvas_Paint;
            boxSplit.SplitterMoved -= boxSplit_SplitterMoved;
            boxSplit.SplitterMoving -= boxSplit_SplitterMoving;

            int ps = _item != null ? _item.PointSize : item.PointSize;
            _item = item;
            if (ImageBuffer != null)
                _item.PointSize = ps;
            boxHeight.Value = _item.PatternLines;
            boxCanvasHolder.Height = boxSplit.Height = _item.PointSize * _item.PatternLines;
            boxSplit.SplitterDistance = _item.PatternLength * _item.PointSize;
            _tempImage = _item.Image;
            undo.Clear();
            redo.Clear();
            

            boxSplit.SplitterIncrement = _item.PointSize;

            boxCanvas.Paint += new PaintEventHandler(boxCanvas_Paint);
            boxSplit.SplitterMoved += new SplitterEventHandler(boxSplit_SplitterMoved);
            boxSplit.SplitterMoving += new SplitterCancelEventHandler(boxSplit_SplitterMoving);
            boxCanvas.Invalidate();
        }        

        public ConfigurationItem Configuration()
        {
            _item.Image = _tempImage.Clone() as Image;
            return _item.Clone() as ConfigurationItem;
        }

        public Color BrushFrontColor
        {
            get { return boxFrontColor.BackColor; }
            set
            {
                boxFrontColor.BackColor = value;
                boxFrontColor.FlatAppearance.MouseDownBackColor = value;
                boxFrontColor.FlatAppearance.MouseOverBackColor = value;
            }
        }

        public Color BrushBackColor
        {
            get { return boxBgColor.BackColor; }
            set
            {
                boxBgColor.BackColor = value;
                boxBgColor.FlatAppearance.MouseDownBackColor = value;
                boxBgColor.FlatAppearance.MouseOverBackColor = value;
            }
        }

        #endregion

        public DrawControl()
            : this(null)
        {
        }

        public DrawControl(ConfigurationItem item = null)
        {
            InitializeComponent();

            var bucket = new System.Windows.Media.Imaging.BitmapImage(
                    new Uri("pack://application:,,,/Images/DrawingTool/paint_bucket.png", UriKind.Absolute));
            var text = new System.Windows.Media.Imaging.BitmapImage(
                    new Uri("pack://application:,,,/Images/DrawingTool/text.png", UriKind.Absolute));
            var copy = new System.Windows.Media.Imaging.BitmapImage(
                    new Uri("pack://application:,,,/Images/DrawingTool/copy.png", UriKind.Absolute));
            var bmp = new System.Windows.Media.Imaging.BitmapImage(
                    new Uri("pack://application:,,,/Images/DrawingTool/bmp.png", UriKind.Absolute));

            boxFillBG.BackgroundImage = BitmapImage2Bitmap<PngBitmapEncoder>(bucket);
            boxTextButton.BackgroundImage = BitmapImage2Bitmap<PngBitmapEncoder>(text);
            buttonSelect.BackgroundImage = BitmapImage2Bitmap<PngBitmapEncoder>(copy);
            boxBmpImport.BackgroundImage = BitmapImage2Bitmap<PngBitmapEncoder>(bmp);

            if (item == null)
            {
                item = new ConfigurationItem("");
            }
            this.DoubleBuffered = true;
            SetItem(item);
        }

        private Bitmap BitmapImage2Bitmap<EncoderType>(BitmapImage bitmapImage)
            where EncoderType : BitmapEncoder, new()
        {
            using (MemoryStream outStream = new MemoryStream())
            {
                var enc = new EncoderType();
                enc.Frames.Add(BitmapFrame.Create(bitmapImage));
                enc.Save(outStream);
                System.Drawing.Bitmap bitmap = new System.Drawing.Bitmap(outStream);
                return new Bitmap(bitmap);
            }
        }

        private Color ChooseColor(Color changingColor)
        {
            ColorDialog cd = new ColorDialog();
            cd.AllowFullOpen = false;
            cd.CustomColors = new int[8] { 
                ColorTranslator.ToOle( Color.FromArgb(0, 0, 0)), 
                ColorTranslator.ToOle( Color.FromArgb(255, 0, 0)), 
                ColorTranslator.ToOle( Color.FromArgb(0, 255, 0)), 
                ColorTranslator.ToOle( Color.FromArgb(255, 255, 0)), 
                ColorTranslator.ToOle( Color.FromArgb(0, 0, 255)), 
                ColorTranslator.ToOle( Color.FromArgb(255, 0, 255)), 
                ColorTranslator.ToOle( Color.FromArgb(0, 255, 255)), 
                ColorTranslator.ToOle( Color.FromArgb(255, 255, 255))
            };
            if (cd.ShowDialog() == DialogResult.OK)
                return cd.Color;
            return changingColor;
        }

        #region Функции рисования

        /// <summary>
        /// Заливка полотна. 
        /// </summary>
        /// <param name="parameters">Graphics, Color</param>
        private void FillCanvas(Color color)
        {
            if (_item == null) return;
            if (_tempImage == null) return;
            Graphics g = Graphics.FromImage(_tempImage);            
            g.FillRectangle(new SolidBrush(color), 0, 0, boxCanvas.Width, boxCanvas.Height);
            boxCanvas.Refresh();
        }

        /// <summary>
        /// Заливка одной ячейки по координатам пиксела
        /// </summary>
        /// <param name="parameters">Graphics, Color, X, Y</param>
        void FillPixel(Color c, int x, int y)
        {
            if (_item == null) return;
            if (_tempImage == null) return;
            Graphics g = Graphics.FromImage(_tempImage);
            Rectangle fillArea = FillPixel(c, g, _item.PointSize, x, y);
            boxCanvas.Invalidate(fillArea);
            boxCanvas.Update();
        }

        Rectangle FillPixel(Color c, Graphics g, int pointSize, int xCoordinate, int yCoordinate)
        {
            Rectangle fillArea = GetSquare(xCoordinate, yCoordinate, pointSize);
            g.FillRectangle(new SolidBrush(c), fillArea);
            return fillArea;
        }

        private Rectangle GetSquare(int x, int y, int pointSize)
        {
            int squaresX = x / _item.PointSize;
            if (x % pointSize == 0) squaresX--;

            int squaresY = y / _item.PointSize;
            if (y % pointSize == 0) squaresY--;

            Rectangle result = new Rectangle(
                squaresX * pointSize,
                squaresY * pointSize,
                pointSize,
                pointSize);

            return result;
        }

        private void DrawGrid(Graphics graphics)
        {
            if (graphics == null) return;
            for (int i = 0; i < boxCanvas.Width; ++i)
                graphics.DrawLine(new Pen(GridColorLight),
                    i * _item.PointSize,
                    0,
                    i * _item.PointSize,
                    boxCanvas.Height);
            for (int i = 0; i < boxCanvas.Width; ++i)
                graphics.DrawLine(new Pen(GridColorLight),
                    0,
                    i * _item.PointSize,
                    boxCanvas.Width,
                    i * _item.PointSize);
        }

        private Rectangle GetSquare(int x, int y)
        {
            Rectangle result = GetSquare(x, y, _item.PointSize);
            return result;
        }

        #endregion

        #region Стек Undo/Redo
        private Stack<ConfigurationItem> undo = new Stack<ConfigurationItem>();
        private Stack<ConfigurationItem> redo = new Stack<ConfigurationItem>();
        public void Undo()
        {
            if (_item == null) return;
            if (undo.Count != 0)
            {
                _item.Image = _tempImage.Clone() as Image;
                redo.Push(_item.Clone() as ConfigurationItem);

                boxSplit.SplitterMoved -= boxSplit_SplitterMoved;
                _item = undo.Pop().Clone() as ConfigurationItem;
                _tempImage = _item.Image.Clone() as Image;
                boxSplit.SplitterDistance = _item.PatternLength * _item.PointSize;
                boxCanvasHolder.Height = boxSplit.Height = _item.PointSize * _item.PatternLines;
                
                boxSplit.SplitterMoved += boxSplit_SplitterMoved;
                boxCanvas.Refresh();
            }
        }
        public void Redo()
        {
            if (_item == null) return;
            if (redo.Count != 0)
            {
                _item.Image = _tempImage.Clone() as Image;
                undo.Push(_item.Clone() as ConfigurationItem);

                boxSplit.SplitterMoved -= boxSplit_SplitterMoved;
                _item = redo.Pop().Clone() as ConfigurationItem;
                _tempImage = _item.Image.Clone() as Image;
                boxSplit.SplitterDistance = _item.PatternLength * _item.PointSize;
                boxCanvasHolder.Height = boxSplit.Height = _item.PointSize * _item.PatternLines;
                
                boxSplit.SplitterMoved += boxSplit_SplitterMoved;
                boxCanvas.Refresh();
            }
        }
        public void Push()
        {
            _item.Image = _tempImage.Clone() as Image;
            undo.Push(_item.Clone() as ConfigurationItem);
            redo.Clear();
        }
        #endregion

        #region Рисование мышью

        private void boxCanvas_MouseDown(object sender, MouseEventArgs e)
        {
            if (_item == null) return;
            Push();

            if (_startSelection)
            {
                _mouseSelecting = true;
                _startSelection = false;
                _selection = new Rectangle(e.X / _item.PointSize * _item.PointSize, 
                    e.Y / _item.PointSize * _item.PointSize, 1, 1);
                return;
            }

            //undo.Push(_tempImage.Clone() as Image);            

            if (ImageBuffer != null)
            {
                isMouseDraws = false;
                Graphics g = Graphics.FromImage(_tempImage);                
                g.DrawImageUnscaled(ImageBuffer, new Point(
                    e.X / _item.PointSize * _item.PointSize - ((ImageBuffer.Width / _item.PointSize) / 2) * _item.PointSize,
                    e.Y / _item.PointSize * _item.PointSize - ((ImageBuffer.Height / _item.PointSize) / 2) * _item.PointSize
                    ));
                g.Dispose();
                return;
            }

            if (e.Button == MouseButtons.Left)
            {
                StartPainting(BrushFrontColor, e.X, e.Y);
            }
            else if (e.Button == MouseButtons.Right)
            {
                StartPainting(BrushBackColor, e.X, e.Y);
            }
        }

        bool isMouseDraws = false;

        private void StartPainting(Color color, int x, int y)
        {
            FillPixel(color, x, y);
            isMouseDraws = true;
        }

        private void boxCanvas_MouseEnter(object sender, EventArgs e)
        {
            isMouseDraws = false;
        }

        private void boxCanvas_MouseLeave(object sender, EventArgs e)
        {
            isMouseDraws = false;
        }

        private void boxCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (_mouseSelecting)
            {
                int w = Math.Abs(e.X / _item.PointSize * _item.PointSize - _selection.X);
                int h = Math.Abs(e.Y / _item.PointSize * _item.PointSize - _selection.Y);
                _selection = new Rectangle(
                    e.X < _selection.X ? e.X : _selection.X,
                    e.Y < _selection.Y ? e.Y : _selection.Y,
                    w,
                    h
                    );
                if (_selection.Width > _invalidationArea.Width || _selection.Height > _invalidationArea.Height)
                {
                    _invalidationArea = _selection;
                }

                boxCanvas.Invalidate(_invalidationArea);
                return;
            }

            if (ImageBuffer != null)
            {
                int newX = e.X / _item.PointSize * _item.PointSize;
                int newY = e.Y / _item.PointSize * _item.PointSize;
                _mousePoint = new Point(newX, newY);
                if (_bufferCurrentPosition != _mousePoint)
                    boxCanvas.Invalidate();
            }

            if (!isMouseDraws) return;
            if (e.Button == MouseButtons.Left)
                FillPixel(BrushFrontColor, e.X, e.Y);
            else if (e.Button == MouseButtons.Right)
                FillPixel(BrushBackColor, e.X, e.Y);
        }

        Rectangle _invalidationArea = new Rectangle(0, 0, 0, 0);
        Point _mousePoint = Point.Empty;

        private void boxCanvas_MouseUp(object sender, MouseEventArgs e)
        {
            if (_mouseSelecting)
            {
                Bitmap buf = new Bitmap(_selection.Width, _selection.Height);
                Graphics gBuf = Graphics.FromImage(buf);
                gBuf.DrawImage(_tempImage, new Rectangle(0, 0, buf.Width, buf.Height), _selection, GraphicsUnit.Pixel);
                ImageBuffer = buf.Clone() as Image;
                gBuf.Dispose();
                buf.Dispose();
            }
            _mouseSelecting = false;
            boxCanvas.Invalidate(_invalidationArea);
            _invalidationArea = new Rectangle(0, 0, 0, 0);
            isMouseDraws = false;
        }
        #endregion

        #region Обработчики
        
        void boxCanvas_Paint(object sender, PaintEventArgs e)
        {
            if (_item == null) return;
            e.Graphics.DrawImage(_tempImage, 0, 0);
            if (ImageBuffer != null)
            {                
                e.Graphics.DrawImage(ImageBuffer, new Point(_mousePoint.X - ((ImageBuffer.Width / _item.PointSize) / 2) * _item.PointSize,
                    _mousePoint.Y - ((ImageBuffer.Height / _item.PointSize) / 2) * _item.PointSize));
                //_bufferCurrentPosition = _mousePoint;
            }
            DrawGrid(e.Graphics);
            if (_mouseSelecting)
            {
                Pen p = new Pen(new SolidBrush(Color.White), 2.2f);
                p.DashStyle = System.Drawing.Drawing2D.DashStyle.Dash;
                e.Graphics.DrawRectangle(p, _selection);
            }
        }

        private void boxFrontColor_Click(object sender, EventArgs e)
        {
            BrushFrontColor = ChooseColor(BrushFrontColor);
        }
        
        private void boxBgColor_Click(object sender, EventArgs e)
        {
            Color bc = BrushBackColor = ChooseColor(BrushBackColor);
            Bitmap b = (Bitmap) boxFillBG.BackgroundImage;
            for(int x=0; x< b.Width; x++)
                for (int y = 0; y < b.Height; y++)
                {
                    if (b.GetPixel(x, y).A > 80) b.SetPixel(x, y, bc);
                }
            boxFillBG.BackgroundImage = b;
            boxFillBG.Refresh();
        }

        void boxSplit_SplitterMoved(object sender, SplitterEventArgs e)
        {
            Push();
            _item.PatternLength = boxCanvas.Width / _item.PointSize;
            _tempImage = _item.Image.Clone() as Image;
            Bitmap b = new Bitmap(_item.PatternLength * _item.PointSize, _item.PatternLines * _item.PointSize);
            var g = Graphics.FromImage(b);
            g.DrawImage(_tempImage, 0, 0);
            _tempImage = b;
            g.Dispose();
        }

        void boxSplit_SplitterMoving(object sender, SplitterCancelEventArgs e)
        {
            //if (_item == null) e.Cancel = true;
            //if (e.MouseCursorX > PatternItem.MaxPatternLength * _item.PointSize
            //    || e.MouseCursorX < PatternItem.MinPatternLength * _item.PointSize) e.Cancel = true;
        }
        
        private void boxFillBG_Click(object sender, EventArgs e)
        {
            Push();
            FillCanvas(BrushBackColor);            
        }

        private void boxTextButton_Click(object sender, EventArgs e)
        {
            //TextDialog td = new TextDialog(_item, Settings.Default.FontStyle, _item.PointSize, BrushFrontColor);
            TextDialog td = new TextDialog(_item, DefaultFont, _item.PointSize, BrushFrontColor);
            Application.RemoveMessageFilter(this);
            if (td.ShowDialog() == DialogResult.OK)
            {
                ImageBuffer = td.TextImage;
            }
            Application.AddMessageFilter(this);
        }
        
        private void DrawControl_Load(object sender, EventArgs e)
        {
            Application.AddMessageFilter(this);
        }

        private void buttonSelect_Click(object sender, EventArgs e)
        {
            ImageBuffer = null;
            _startSelection = true;
        }

        private void boxColumnWidth_ValueChanged(object sender, EventArgs e)
        {
            if (_item == null) return;
            Push();
            
        }

        private void boxBmpImport_Click(object sender, EventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Multiselect = false;
            ofd.Filter = "Bmp files (.bmp)|*.bmp";
            if (ofd.ShowDialog() == DialogResult.OK)
            {
                ImageBuffer = null;
                Bitmap bmp = Bitmap.FromFile(ofd.FileName) as Bitmap;
                ImageBuffer = new Bitmap(_item.PatternLength * _item.PointSize, _item.PointSize * _item.PatternLines);
                Graphics gra = Graphics.FromImage(ImageBuffer);
                if (bmp != null)
                    for (int column = 0; column < bmp.Width && column < _item.PatternLength; column++)
                    {
                        for (int row = 0; row < bmp.Height && row < _item.PatternLines; row++)
                        {
                            Color c = bmp.GetPixel(column, row);
                            int eps = 20;
                            int max = Math.Max(c.R, Math.Max(c.B, c.G));
                            int middle = (c.B + c.G + c.R) / 3;

                            if (Math.Abs(max - middle) > eps)
                            {
                                if (c.R == max) c = Color.Red;
                                else if (c.G == max) c = Color.FromArgb(0, 255, 0);
                                else if (c.B == max) c = Color.Blue;
                            }
                            else
                            {
                                if ((c.B + c.G + c.R) / 3 - 127 > 20) { c = Color.White; }
                                else c = Color.Black;
                            }

                            FillPixel(c, gra, _item.PointSize, column * _item.PointSize, row * _item.PointSize);
                        }
                    }
                gra.Dispose();
                bmp.Dispose();
            }
            ofd.Dispose();
        }

        private void boxHeight_ValueChanged(object sender, EventArgs e)
        {
            _item.PatternLines = (int)(sender as NumericUpDown).Value;
            _item.PatternLength = boxCanvas.Width / _item.PointSize;
            if (_tempImage == null) _tempImage = _item.Image.Clone() as Image;
            Bitmap b = new Bitmap(_item.PatternLength * _item.PointSize, _item.PatternLines * _item.PointSize);
            var g = Graphics.FromImage(b);
            g.DrawImage(_tempImage, 0, 0);
            _tempImage = b;
            g.Dispose();
            this.Height = b.Height;
            boxCanvas.Invalidate();
            if (PatternLinesChanged != null)
                PatternLinesChanged(this, new EventArgs());
        }

        #endregion

        public event EventHandler PatternLinesChanged;

        #region Обработка клавиш
        const int WM_KEYDOWN = 0x100;
        public bool PreFilterMessage(ref Message m)
        {
            Keys keyCode = (Keys)(int)m.WParam & Keys.KeyCode;
            //Keys.Modifiers mod = (Keys.Modifiers)(int)m.WParam & Keys.ModifierKeys;
            if (m.Msg == WM_KEYDOWN && keyCode == Keys.Escape)
            {
                ImageBuffer = null;
                isMouseDraws = false;
                _startSelection = false;
                _mouseSelecting = false;
                boxCanvas.Refresh();
                return true;
            }
            if (m.Msg == WM_KEYDOWN && keyCode == Keys.Z)
            {
                Undo();
                return true;
            }
            if (m.Msg == WM_KEYDOWN && keyCode == Keys.Y)
            {
                Redo();
                return true;
            }
            return false;
        } 
        #endregion
    }
}
