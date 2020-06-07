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
using Common.Helpers;
using Common.Imaging;
//using PixelTuner.Drawing.Properties;

namespace PixelTuner.Drawing
{
    public partial class DrawControl : UserControl
    {
        #region Поля

        bool _mouseSelecting = false;
        bool _startSelection = false;
        bool _btnControlPressed = false;
        bool _btnAltPressed = false;
        bool _btnShiftPressed = false;
        private static Image _duplicatedFragment = null;
        Rectangle _selection = new Rectangle(0, 0, 0, 0);
        Rectangle _invalidationArea = new Rectangle(0, 0, 0, 0);
        Cursor _curPipette = null;
        Point _mousePoint = Point.Empty;
        const int MAX_UNDO_STACK_SIZE = 256;

        Color _gridColor = Color.FromArgb(255, 60, 60, 60);

        private static ConfigurationItem _item = null;

        public ConfigurationItem Configuration
        {
            get
            {
                return _item;
            }
            set
            {
                _item = value;
                ResetControls(_item);

                undo = new CircularBuffer<ConfigurationItem>(MAX_UNDO_STACK_SIZE);
                redo = new CircularBuffer<ConfigurationItem>(MAX_UNDO_STACK_SIZE);

                boxCanvas.Invalidate();
            }
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

        public int ToolbarHeight
        {
            get
            {
                return splitContainer.SplitterDistance + splitContainer.SplitterWidth / 2 + 1;
            }
        }

        public double ControlHeight
        {
            get { return Configuration.PatternLines * Configuration.PointSize + this.ToolbarHeight; }
        }

        #endregion

        protected static class ImageOffsetAboutMousePosition
        {
            private static int _deltaX = 0;
            private static int _deltaY = 0;

            public enum Direction { Horizontal, Vertical };

            public static int DeltaX
            {
                get
                {
                    int x = 0;
                    if (_duplicatedFragment == null) { _deltaX = 0; _deltaY = 0; return x; }
                    x = (_duplicatedFragment.Width / 2) - _deltaX;
                    return x;
                }
                set { _deltaX = value; }
            }

            public static int DeltaY
            {
                get
                {
                    int y = 0;
                    if (_duplicatedFragment == null) { _deltaX = 0; _deltaY = 0; return y; }
                    y = (_duplicatedFragment.Height / 2) - _deltaY;
                    return y;
                }
                set { _deltaY = value; }
            }

            public static void IncreaseOffset(Direction d)
            {
                if (d == Direction.Horizontal)
                    _deltaX += _item.PointSize;
                else if (d == Direction.Vertical)
                    _deltaY += _item.PointSize;
            }

            public static void DecreaseOffset(Direction d)
            {
                if (d == Direction.Horizontal)
                    _deltaX -= _item.PointSize;
                else if (d == Direction.Vertical)
                    _deltaY -= _item.PointSize;
            }
        }

        public static void ClearCopyBuffer()
        {
            _duplicatedFragment = null;
        }

        public DrawControl(ConfigurationItem item)
        {
            InitializeComponent();
            Width = boxCanvas.Width = item.PatternLength*item.PointSize;
            boxWidth.Maximum = ConfigurationItem.MaxPatternLength;
            boxWidth.Minimum = ConfigurationItem.MinPatternLength;

            var bucket = new System.Windows.Media.Imaging.BitmapImage(
                    new Uri("pack://application:,,,/Images/DrawingTool/paint_bucket.png", UriKind.Absolute));
            var text = new System.Windows.Media.Imaging.BitmapImage(
                    new Uri("pack://application:,,,/Images/DrawingTool/text.png", UriKind.Absolute));
            var copy = new System.Windows.Media.Imaging.BitmapImage(
                    new Uri("pack://application:,,,/Images/DrawingTool/copy.png", UriKind.Absolute));
            var bmp = new System.Windows.Media.Imaging.BitmapImage(
                    new Uri("pack://application:,,,/Images/DrawingTool/bmp.png", UriKind.Absolute));
            var pip = new System.Windows.Media.Imaging.BitmapImage(
                    new Uri("pack://application:,,,/Images/DrawingTool/pipette.png", UriKind.Absolute));
            var flipVImg = new System.Windows.Media.Imaging.BitmapImage(
                    new Uri("pack://application:,,,/Images/DrawingTool/flip_vertical.png", UriKind.Absolute));
            var flipHImg = new System.Windows.Media.Imaging.BitmapImage(
                    new Uri("pack://application:,,,/Images/DrawingTool/flip_horizontal.png", UriKind.Absolute));
            var pipCursor = new System.Windows.Media.Imaging.BitmapImage(
                    new Uri("pack://application:,,,/Images/pipette_tool.ico", UriKind.Absolute));

            boxFillBG.BackgroundImage = ImageHelper.BitmapImageToBitmap<PngBitmapEncoder>(bucket);
            boxTextButton.BackgroundImage = ImageHelper.BitmapImageToBitmap<PngBitmapEncoder>(text);
            boxSelect.BackgroundImage = ImageHelper.BitmapImageToBitmap<PngBitmapEncoder>(copy);
            boxBmpImport.BackgroundImage = ImageHelper.BitmapImageToBitmap<PngBitmapEncoder>(bmp);
            boxPipette.BackgroundImage = ImageHelper.BitmapImageToBitmap<PngBitmapEncoder>(pip);
            boxFlipV.BackgroundImage = ImageHelper.BitmapImageToBitmap<PngBitmapEncoder>(flipVImg);
            boxFlipH.BackgroundImage = ImageHelper.BitmapImageToBitmap<PngBitmapEncoder>(flipHImg);
            _curPipette = CursorHelper.CreateCursorNoResize(ImageHelper.BitmapImageToBitmap<PngBitmapEncoder>(pipCursor), 1, 15);
            
            this.DoubleBuffered = true;
            this.Configuration = item;
        }
        
        private Color ChooseColor(Color changingColor)
        {
            ColorDialog cd = new ColorDialog();
            cd.AllowFullOpen = true;
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
        
        public void CleanUp()
        {
            undo.Clear();
            redo.Clear();
        }

        /// <summary>
        /// Устанавливает значения полей извлекая их из конфигурации
        /// </summary>
        /// <param name="value">Конфигурационный объект</param>
        private void ResetControls(ConfigurationItem value)
        {
            if (value != null)
            {
                boxPointSize.ValueChanged -= boxPointSize_ValueChanged;
                SetNumericControlValue(boxPointSize, _item.PointSize);
                boxPointSize.ValueChanged += boxPointSize_ValueChanged;

                boxHeight.ValueChanged -= boxHeight_ValueChanged;
                if (_item.PatternLines % 5 != 0)
                    _item.PatternLines = ((_item.PatternLines / 5) + 1) * 5;

                SetNumericControlValue(boxHeight, _item.PatternLines);
                boxHeight.ValueChanged += boxHeight_ValueChanged;

                boxWidth.ValueChanged -= boxWidth_ValueChanged;
                SetNumericControlValue(boxWidth, _item.PatternLength);
                boxWidth.ValueChanged += boxWidth_ValueChanged;
            }
            else
            {
                boxHeight.Value = boxHeight.Minimum;
                boxPointSize.Value = boxPointSize.Minimum;
            }
        }

        /// <summary>
        /// Позволяет безопасно задать значения в диапазоне от минимума до максимума
        /// </summary>
        /// <param name="control">Объект NumericUpDown для задания значения</param>
        /// <param name="val">Желаемое значение</param>
        private void SetNumericControlValue(NumericUpDown control, int val)
        {
            if (val > control.Maximum) { control.Value = control.Maximum; }
            else if (val < control.Minimum) { control.Value = control.Minimum; }
            else { control.Value = val; }
        }

        #region Функции рисования

        /// <summary>
        /// Заливка полотна. 
        /// </summary>
        /// <param name="parameters">Graphics, Color</param>
        private void FillCanvas(Color color)
        {
            try
            {
                if (_item == null || _item.Image == null) return;
                //if (_tempImage == null) return;
                Graphics g = Graphics.FromImage(_item.Image);
                g.FillRectangle(new SolidBrush(color), 0, 0, boxCanvas.Width, boxCanvas.Height);
                boxCanvas.Refresh();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        /// <summary>
        /// Заливка одной ячейки по координатам пиксела
        /// </summary>
        /// <param name="parameters">Graphics, Color, X, Y</param>
        void FillPixel(Color c, int x, int y)
        {
            if (_item == null || _item.Image == null) return;
            //if (_tempImage == null) return;
            Graphics g = Graphics.FromImage(_item.Image);
            Rectangle fillArea = FillPixel(c, g, _item.PointSize, x, y);
            boxCanvas.Invalidate(fillArea);
            boxCanvas.Update();
        }

        Rectangle FillPixel(Color c, Graphics g, int pointSize, int xCoordinate, int yCoordinate)
        {
            Rectangle fillArea = GetSquare(xCoordinate, yCoordinate, pointSize);
            try
            {
                g.FillRectangle(new SolidBrush(c), fillArea);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
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

            var w = _item.PointSize*_item.PatternLength;
            for (int i = 0; i <= _item.PatternLength; ++i)
                graphics.DrawLine(new Pen(_gridColor), i * _item.PointSize, 0, i * _item.PointSize, boxCanvas.Height);

            for (int i = 0; i < _item.PatternLines; ++i)
                graphics.DrawLine(new Pen(_gridColor), 0, i * _item.PointSize, w, i * _item.PointSize);
        }

        private Rectangle GetSquare(int x, int y)
        {
            Rectangle result = GetSquare(x, y, _item.PointSize);
            return result;
        }

        #endregion

        #region Стек Undo/Redo
        private CircularBuffer<ConfigurationItem> undo = new CircularBuffer<ConfigurationItem>(MAX_UNDO_STACK_SIZE);
        private CircularBuffer<ConfigurationItem> redo = new CircularBuffer<ConfigurationItem>(MAX_UNDO_STACK_SIZE);
        public void Undo()
        {
            boxCanvas?.Focus();
            if (_item == null) return;
            if (undo.Count != 0)
            {
                //_item.Image = _tempImage.Clone() as Image;
                redo.Push(_item.Clone() as ConfigurationItem);

                _item = undo.Pop().Clone() as ConfigurationItem;
                //_tempImage = _item.Image.Clone() as Image;
                boxHeight.ValueChanged -= boxHeight_ValueChanged;
                boxWidth.ValueChanged -= boxWidth_ValueChanged;
                boxPointSize.ValueChanged -= boxPointSize_ValueChanged;
                {
                    int newWidth = (int)boxWidth.Value * _item.PointSize;
                    if (newWidth > this.MinimumSize.Width) this.Width = newWidth;
                    SetNumericControlValue(boxHeight, _item.PatternLines);
                    SetNumericControlValue(boxWidth, _item.PatternLength);
                    SetNumericControlValue(boxPointSize, _item.PointSize);
                }
                boxPointSize.ValueChanged += boxPointSize_ValueChanged;
                boxWidth.ValueChanged += boxWidth_ValueChanged;
                boxHeight.ValueChanged += boxHeight_ValueChanged;

                if (PatternLinesChanged != null)
                    PatternLinesChanged(this, new EventArgs());

                boxCanvas.Refresh();
            }
        }
        public void Redo()
        {
            boxCanvas?.Focus();
            if (_item == null) return;
            if (redo.Count != 0)
            {
                undo.Push(_item.Clone() as ConfigurationItem);

                _item = redo.Pop().Clone() as ConfigurationItem;
                boxHeight.ValueChanged -= boxHeight_ValueChanged;
                boxWidth.ValueChanged -= boxWidth_ValueChanged;
                boxPointSize.ValueChanged -= boxPointSize_ValueChanged;
                {
                    int newWidth = (int)boxWidth.Value * _item.PointSize;
                    if (newWidth > this.MinimumSize.Width) this.Width = newWidth;
                    SetNumericControlValue(boxHeight, _item.PatternLines);
                    SetNumericControlValue(boxWidth, _item.PatternLength);
                    SetNumericControlValue(boxPointSize, _item.PointSize);
                }
                boxPointSize.ValueChanged += boxPointSize_ValueChanged;
                boxWidth.ValueChanged += boxWidth_ValueChanged;
                boxHeight.ValueChanged += boxHeight_ValueChanged;
                
                if (PatternLinesChanged != null)
                    PatternLinesChanged(this, new EventArgs());

                boxCanvas.Refresh();
            }
        }
        public void Push()
        {
            //_item.Image = _tempImage.Clone() as Image;
            undo.Push(_item.Clone() as ConfigurationItem);
            redo.Clear();
        }
        #endregion

        #region Рисование мышью

        private void boxCanvas_MouseDown(object sender, MouseEventArgs e)
        {
            if (_item == null || _item.Image == null) return;

            if (boxCanvas.Cursor == _curPipette)
            {
                var b = _item.Image as Bitmap;
                if (e.Button == MouseButtons.Left)
                    BrushFrontColor = b.GetPixel(e.X, e.Y);
                else
                    BrushBackColor = b.GetPixel(e.X, e.Y);
                boxCanvas.Cursor = Cursors.Default;
                return;
            }

            if (_startSelection)
            {
                _mouseSelecting = true;
                _startSelection = false;
                _selection = new Rectangle(e.X / _item.PointSize * _item.PointSize,
                    e.Y / _item.PointSize * _item.PointSize, 1, 1);
                return;
            }

            Push();

            if (_duplicatedFragment != null)
            {
                isMouseDraws = false;
                Graphics g = Graphics.FromImage(_item.Image);
                g.DrawImageUnscaled(_duplicatedFragment, new Point(
                    e.X / _item.PointSize * _item.PointSize - ImageOffsetAboutMousePosition.DeltaX,
                    e.Y / _item.PointSize * _item.PointSize - ImageOffsetAboutMousePosition.DeltaY
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
            if (_btnShiftPressed && boxCanvas.Cursor == Cursors.Default) boxCanvas.Cursor = _curPipette;
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

            if (_duplicatedFragment != null)
            {
                int newX = e.X / _item.PointSize * _item.PointSize;
                int newY = e.Y / _item.PointSize * _item.PointSize;
                _mousePoint = new Point(newX, newY);
                //if (_bufferCurrentPosition != _mousePoint)
                    boxCanvas.Invalidate();
            }

            if (!isMouseDraws) return;
            if (e.Button == MouseButtons.Left)
                FillPixel(BrushFrontColor, e.X, e.Y);
            else if (e.Button == MouseButtons.Right)
                FillPixel(BrushBackColor, e.X, e.Y);
        }
                
        private void boxCanvas_MouseUp(object sender, MouseEventArgs e)
        {
            if (_item != null && _item.Image != null && _mouseSelecting)
            {
                Bitmap buf = new Bitmap(_selection.Width, _selection.Height);
                Graphics gBuf = Graphics.FromImage(buf);
                gBuf.DrawImage(_item.Image, new Rectangle(0, 0, buf.Width, buf.Height), _selection, GraphicsUnit.Pixel);
                _duplicatedFragment = buf.Clone() as Image;
                gBuf.Dispose();
                buf.Dispose();
            }
            _mouseSelecting = false;
            boxCanvas.Invalidate(_invalidationArea);
            _invalidationArea = new Rectangle(0, 0, 0, 0);
            isMouseDraws = false;
        }
        #endregion

        #region Отрисовка
        
        void boxCanvas_Paint(object sender, PaintEventArgs e)
        {
            try
            {
                if (_item == null || _item.Image == null) return;
                e.Graphics.DrawImage(_item.Image, 0, 0);
                if (_duplicatedFragment != null)
                {
                    e.Graphics.DrawImage(_duplicatedFragment, new Point(_mousePoint.X - ImageOffsetAboutMousePosition.DeltaX,
                        _mousePoint.Y - ImageOffsetAboutMousePosition.DeltaY));
                }
                DrawGrid(e.Graphics);
                if (_mouseSelecting)
                {
                    Pen p = new Pen(new SolidBrush(Color.White), 2.2f);
                    p.DashStyle = System.Drawing.Drawing2D.DashStyle.Dash;
                    e.Graphics.DrawRectangle(p, _selection);
                }
            }
            catch(Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        #endregion

        #region Обработка кнопок формы

        private void boxPipette_Click(object sender, EventArgs e)
        {
            boxCanvas.Cursor = _curPipette;
        }

        private void buttonSelect_Click(object sender, EventArgs e)
        {
            _duplicatedFragment = null;
            _startSelection = true;
        }

        private void boxFillBG_Click(object sender, EventArgs e)
        {
            Push();
            FillCanvas(BrushBackColor);
        }

        private void boxTextButton_Click(object sender, EventArgs e)
        {
            TextDialog td = new TextDialog(_item, DefaultFont, BrushFrontColor);
            if (td.ShowDialog() == DialogResult.OK)
            {
                _duplicatedFragment = td.TextImage;
            }
        }

        private void boxBmpImport_Click(object sender, EventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Multiselect = false;
            ofd.Filter = "Image files|*.jpg;*.bmp;*.png|Bmp files (.bmp)|*.bmp|Jpeg files (.jpg)|*.jpg|PNG files (.png)|*.png";
            if (ofd.ShowDialog() == DialogResult.OK)
            {
                _duplicatedFragment = null;
                var bmp = Image.FromFile(ofd.FileName) as Bitmap;
                _duplicatedFragment = new Bitmap(bmp.Width * _item.PointSize, bmp.Height * _item.PointSize);
                Graphics gra = Graphics.FromImage(_duplicatedFragment);
                if (bmp != null)
                    for (int column = 0; column < bmp.Width && column < _item.PatternLength; column++)
                    {
                        for (int row = 0; row < bmp.Height && row < _item.PatternLines; row++)
                        {
                            Color c = bmp.GetPixel(column, row);
                            FillPixel(c, gra, _item.PointSize, column * _item.PointSize, row * _item.PointSize);
                        }
                    }
                gra.Dispose();
                bmp.Dispose();
            }
            ofd.Dispose();
        }

        private void boxFrontColor_Click(object sender, EventArgs e)
        {
            BrushFrontColor = ChooseColor(BrushFrontColor);
        }

        private void boxBgColor_Click(object sender, EventArgs e)
        {
            Color bc = BrushBackColor = ChooseColor(BrushBackColor);
            Bitmap b = (Bitmap)boxFillBG.BackgroundImage;
            for (int x = 0; x < b.Width; x++)
                for (int y = 0; y < b.Height; y++)
                {
                    if (b.GetPixel(x, y).A > 80) b.SetPixel(x, y, bc);
                }
            boxFillBG.BackgroundImage = b;
            boxFillBG.Refresh();
        }
        
        private void boxFlipV_Click(object sender, EventArgs e)
        {
            if (_duplicatedFragment != null)
            {
                Graphics g = Graphics.FromImage(_duplicatedFragment);
                var clone = _duplicatedFragment.Clone() as Image;
                g.Clear(Color.FromArgb(0));
                g.DrawImage(clone, 0, clone.Height, clone.Width, -clone.Height);
                boxCanvas.Invalidate();
            }
            else
            {
                Push();
                Graphics g = Graphics.FromImage(_item.Image);
                g.DrawImage(_item.Image, 0, _item.Image.Height, _item.Image.Width, -_item.Image.Height);
                boxCanvas.Refresh();
            }
        }

        private void boxFlipH_Click(object sender, EventArgs e)
        {
            if (_duplicatedFragment != null)
            {
                Graphics g = Graphics.FromImage(_duplicatedFragment);
                var clone = _duplicatedFragment.Clone() as Image;
                g.Clear(Color.FromArgb(0));
                g.DrawImage(clone, clone.Width, 0, -clone.Width, clone.Height);
                boxCanvas.Invalidate();
            }
            else
            {
                Push();
                Graphics g = Graphics.FromImage(_item.Image);
                g.DrawImage(_item.Image, _item.Image.Width, 0, -_item.Image.Width, _item.Image.Height);
                boxCanvas.Refresh();
            }
        }

        #endregion

        #region Изменение размеров
        
        private void boxHeight_ValueChanged(object sender, EventArgs e)
        {
            if (_item == null || _item.Image == null) return;
            Push();

            _item.PatternLines = (int)boxHeight.Value;

            ResizeImage(_item.PatternLength, _item.PointSize, _item.PatternLines);
            if (PatternLinesChanged != null)
                PatternLinesChanged(this, new EventArgs());
        }

        private void boxWidth_ValueChanged(object sender, EventArgs e)
        {
            try
            {
                if (_item == null || _item.Image == null) return;
                Push();
                _item.PatternLength = (int)boxWidth.Value;
                ResizeImage(_item.PatternLength, _item.PointSize, _item.PatternLines);
                int newWidth = (int)boxWidth.Value * _item.PointSize;
                if (newWidth > this.MinimumSize.Width) this.Width = newWidth;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        private void boxPointSize_ValueChanged(object sender, EventArgs e)
        {
            try
            {
                if (_item == null || _item.Image == null) return;
                _duplicatedFragment = null;
                Push();

                int destPointSize = (int)((NumericUpDown)sender).Value;

                int newWidth = _item.PatternLength * destPointSize;
                int newHeight = _item.PatternLines * destPointSize;

                Bitmap resize = new Bitmap(newWidth, newHeight);

                Graphics g = Graphics.FromImage(resize);
                for (int column = 0; column < _item.PatternLength; column++)
                {
                    for (int row = 0; row < _item.PatternLines; row++)
                    {

                        Color pixColor = (_item.Image as Bitmap).GetPixel(
                            column * _item.PointSize + 1,
                            row * _item.PointSize + 1);

                        g.FillRectangle(new SolidBrush(pixColor), new Rectangle(
                            column * destPointSize,
                            row * destPointSize,
                            destPointSize,
                            destPointSize));

                    }
                }
                _item.Image = resize;
                g.Dispose();

                _item.PointSize = destPointSize;
                ResizeImage(_item.PatternLength, _item.PointSize, _item.PatternLines);

                if (PatternLinesChanged != null)
                    PatternLinesChanged(this, new EventArgs());
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        private void ResizeImage(int patternLength, int pointSize, int patternLines)
        {
            try {
                if (_item == null || _item.Image == null) return;

                Image _tempImageBuffer = _item.Image.Clone() as Image;
                Bitmap b = new Bitmap(patternLength * pointSize, patternLines * pointSize);
                var g = Graphics.FromImage(b);
                g.FillRectangle(new SolidBrush(BrushBackColor), 0, 0, b.Width, b.Height);
                g.DrawImage(_tempImageBuffer, 0, 0);
                _item.Image = (Image)b.Clone();
                g.Dispose();
                Width = boxCanvas.Width = b.Width;
                Height = b.Height + 35;
                boxCanvas.Height = b.Height;
                Invalidate();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        #endregion

        public event EventHandler PatternLinesChanged;

        private void DrawControl_PreviewKeyDown(object sender, PreviewKeyDownEventArgs e)
        {
            if (e.Control && e.KeyData == Keys.Z) { Undo(); return;}
            if (e.Control && e.KeyData == Keys.Y) { Redo(); return;}
        }
    }
}
