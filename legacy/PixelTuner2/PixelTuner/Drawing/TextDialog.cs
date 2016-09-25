using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Text;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Windows.Media;
using Color = System.Drawing.Color;
using FontFamily = System.Drawing.FontFamily;
using Pen = System.Drawing.Pen;

//using PixelTuner.Drawing.Properties;

namespace PixelTuner.Drawing
{
    public partial class TextDialog : Form
    {
        ConfigurationItem _item = null;
        float emSize = 14;
        Color GridColorLight = Color.FromArgb(255, 60, 60, 60);
        Color _drawColor = Color.White;
        Font _font = null;
        bool _cursorVisible = false;
        int _topCorrection = 0;
        Rectangle _cursorPosition = new Rectangle(1, 0, 2, 40);
        const int _cursorDelta = 1;
        Image _tempImage = null;
        Image _result = null;
        
        public Image TextImage
        {
            get { return _result; }
        }
        
        public int CursorStartPosition
        {
            get { return 2 * _item.PointSize; }
        }

        public TextDialog(ConfigurationItem item, Font initialValues, Color c)
        {
            _drawColor = c;
            _item = item;
            
            InitializeComponent();
            MakeImage();
            InitControls();

            _cursorPosition = new Rectangle(CursorStartPosition + _cursorDelta, 0, 2, (int)boxHeight.Value * _item.PointSize);
            
            boxFontFamily.SelectedIndex = boxFontFamily.FindString(initialValues.FontFamily.Name);
            boxFontType.SelectedIndex = boxFontType.FindString(initialValues.Style.ToString());
            boxHeight.Value = (int)emSize;

            boxCanvas.Paint += new PaintEventHandler(boxCanvas_Paint);
            FillCanvas(Color.Black);
            boxCanvas.Invalidate();            
            
            cursorTimer.Start();
        }

        private void cursorTimer_Tick(object sender, EventArgs e)
        {
            if (!_cursorVisible)
                DrawCursor();
            else HideCursor();
            cursorTimer.Start();
        }

        private void HideCursor()
        {
            _cursorVisible = false;
            if (_tempImage == null) return;
            Graphics g = Graphics.FromImage(_tempImage);
            g.FillRectangle(new SolidBrush(Color.Black), _cursorPosition);
            boxCanvas.Invalidate(_cursorPosition);
            g.Dispose();
        }

        private void DrawCursor()
        {
            Graphics g = boxCanvas.CreateGraphics();
            g.FillRectangle(new SolidBrush(GridColorLight), _cursorPosition);
            _cursorVisible = true;
        }

        private void InitControls()
        {
            boxFontFamily.DataSource = FontFamily.Families;
            //Font f = new System.Drawing.Font(
            boxFontFamily.DisplayMember = "Name";
            if (boxFontFamily.SelectedItem == null) return;
            UpdateFontStylesBox();
            Height = _tempImage.Height + 10;
            Width = _tempImage.Width + splitContainer.Panel1.Width + 10;
        }

        private void UpdateFontStylesBox()
        {
            Array styles = Enum.GetValues(typeof(FontStyle));
            List<FontStyle> allowedStyles = new List<FontStyle>();
            foreach (FontStyle fs in styles)
                if ((boxFontFamily.SelectedItem as FontFamily).IsStyleAvailable(fs))
                    allowedStyles.Add(fs);
            boxFontType.DataSource = allowedStyles;
        }

        void boxCanvas_Paint(object sender, PaintEventArgs e)
        {            
            if (_item == null) return;
            e.Graphics.DrawImage(_tempImage, 0, 0);
            DrawGrid(e.Graphics);
        }

        private void MakeImage()
        {
            _tempImage = new Bitmap(_item.PatternLength * _item.PointSize, _item.PatternLines * _item.PointSize);
            Graphics g = Graphics.FromImage(_tempImage);
            g.FillRectangle(new SolidBrush(Color.Black), new Rectangle(0, 0, _tempImage.Width, _tempImage.Height));
            g.Dispose();
        }

        /// <summary>
        /// Заливка полотна. 
        /// </summary>
        /// <param name="parameters">Graphics, Color</param>
        private void FillCanvas(Color color)
        {
            if (_tempImage == null) return;
            Graphics g = Graphics.FromImage(_tempImage);
            g.FillRectangle(new SolidBrush(color), 0, 0, _item.PatternLength * _item.PointSize, _item.PatternLines * _item.PointSize);
            boxCanvas.Invalidate();
            g.Dispose();
        }
        
        private void DrawGrid(Graphics graphics)
        {
            if (graphics == null) return;

            // vertical
            for (int i = 0; i <= _item.PatternLength; ++i)
                graphics.DrawLine(new Pen(GridColorLight), i * _item.PointSize, 0, i * _item.PointSize, _item.PointSize * _item.PatternLines);

            // horizontal
            for (int i = 0; i < _item.PatternLines; ++i)
                graphics.DrawLine(new Pen(GridColorLight), 0, i * _item.PointSize, _item.PatternLength * _item.PointSize, i * _item.PointSize);
        }
        
        private void boxFontFamily_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateFontStylesBox();
            UpdateFontStyle();
            boxLetters.Select();
        }

        private void UpdateFontStyle()
        {
            _font = new Font(boxFontFamily.SelectedItem as FontFamily, emSize, (FontStyle)boxFontType.SelectedItem, GraphicsUnit.Pixel);
        }

        private void boxCanvas_PreviewKeyDown(object sender, PreviewKeyDownEventArgs e)
        {

        }

        private void boxFontType_SelectedIndexChanged(object sender, EventArgs e)
        {
            boxLetters.Select();
            UpdateFontStyle();
        }

        private void boxHeight_ValueChanged(object sender, EventArgs e)
        {
            boxLetters.Select();
            emSize = (float)boxHeight.Value;
            _cursorPosition = new Rectangle(_cursorPosition.X, _cursorPosition.Y, _cursorPosition.Width, _item.PointSize * (int)boxHeight.Value);
            UpdateFontStyle();
        }

        private void boxSpacing_ValueChanged(object sender, EventArgs e)
        {
            boxLetters.Select();
            FillCanvas(Color.Black);
            DrawText(Graphics.FromImage(_tempImage), boxLetters.Text);
        }
        
        private void splitContainer_Panel1_MouseDown(object sender, MouseEventArgs e)
        {
            boxLetters.Select();
        }

        private void TextDialog_MouseDown(object sender, MouseEventArgs e)
        {
            boxLetters.Select();
        }

        private void boxString_TextChanged(object sender, EventArgs e)
        {
            DrawText(Graphics.FromImage(_tempImage), boxLetters.Text);
        }

        private void DrawText(Graphics g, string text)
        {
            FillCanvas(Color.Black);
            var letters = text.ToCharArray();
            var lettersRendered = RenderSmallLetters(letters);
            var columnsCount = 2;
            var r = new Rectangle(0, 0, _item.PointSize, _item.PointSize);

            foreach (var bmp in lettersRendered)
            {
                for (var i = 0; i < bmp.Width; i++)
                {
                    if (!IsEmptyColumn(bmp, i))
                    {
                        for (var j = 0; j < bmp.Height; j++)
                        {
                            var c = bmp.GetPixel(i, j);
                            r.X = columnsCount*_item.PointSize;
                            r.Y = j*_item.PointSize;
                            if (c != Color.FromArgb(0, 0, 0, 0)) c = _drawColor;
                            g.FillRectangle(new SolidBrush(c), r);
                        }
                        columnsCount++;
                    }
                }
                columnsCount += (int) boxSpacing.Value;
            }
            _cursorPosition.X = _item.PointSize * columnsCount;
            boxCanvas.Invalidate();
        }

        private List<Bitmap> RenderSmallLetters(char[] letters)
        {
            var result = new List<Bitmap>();
            for (var i = 0; i < letters.Length; i++)
            {
                var b = new Bitmap((int) emSize*2, (int) emSize);
                using (Graphics srcGraphics = Graphics.FromImage(b))
                {
                    srcGraphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                    srcGraphics.DrawString(letters[i].ToString(), _font, new SolidBrush(_drawColor), 0, 0 - (int)(emSize * 0.2));
                }
                result.Add(b);
            }
            return result;
        }
        
        private bool IsEmptyColumn(Bitmap b, int i)
        {
            bool isEmptyColumn = true;
            for (int j = 0; j < b.Height; j++)
            {
                if (b.GetPixel(i, j) != Color.FromArgb(0, 0, 0, 0)) isEmptyColumn = false;
            }
            return isEmptyColumn;
        }
        
        private void TextDialog_Click(object sender, EventArgs e)
        {
            boxLetters.Select();
        }

        private void TextDialog_FormClosing(object sender, FormClosingEventArgs e)
        {
            //Settings.Default.FontStyle = _font;
        }

        private void boxPlaceButton_Click(object sender, EventArgs e)
        {
            _result = new Bitmap(_tempImage.Width, _tempImage.Height);
            DrawText(Graphics.FromImage(_result), boxLetters.Text);
            CropImage((Bitmap)_result);
        }

        private void CropImage(Bitmap b)
        {
            if (b != null && b.Width != 0 && b.Height != 0)
            {
                int columnLeft = 0;
                int columnRight = _item.PatternLength;
                int rowTop = 0;
                int rowBottom = _item.PatternLines;

                var isEmptyColumn = true;
                for (var i = 1; i < b.Width; i+= _item.PointSize)
                {
                    for (var j = 1; j < b.Height; j+=_item.PointSize)
                    {
                        if (b.GetPixel(i, j) != Color.FromArgb(0, 0, 0, 0))
                        {
                            isEmptyColumn = false;
                            break;
                        }
                    }
                    if (!isEmptyColumn)
                    {
                        break;
                    }
                    else columnLeft++;
                }

                isEmptyColumn = true;
                for (var i = b.Width - 2; i > 0; i -= _item.PointSize)
                {
                    for (var j = 1; j < b.Height; j += _item.PointSize)
                    {
                        if (b.GetPixel(i, j) != Color.FromArgb(0, 0, 0, 0))
                        {
                            isEmptyColumn = false;
                            break;
                        }
                    }
                    if (!isEmptyColumn)
                    {
                        break;
                    }
                    else
                    {
                        columnRight--;
                    }
                }

                var isEmptyRow = true;
                for (var j = 1; j < b.Height; j += _item.PointSize)
                {
                    for (var i = 1; i < b.Width; i += _item.PointSize)
                    {
                        if (b.GetPixel(i, j) != Color.FromArgb(0, 0, 0, 0))
                        {
                            isEmptyRow = false;
                            break;
                        }
                    }
                    if (!isEmptyRow)
                    {
                        break;
                    }
                    else rowTop++;
                }

                isEmptyRow = true;
                for (var j = b.Height - 2; j > 0; j -= _item.PointSize)
                {
                    for (var i = 1; i < b.Width; i += _item.PointSize)
                    {
                        if (b.GetPixel(i, j) != Color.FromArgb(0, 0, 0, 0))
                        {
                            isEmptyRow = false;
                            break;
                        }
                    }
                    if (!isEmptyRow)
                    {
                        break;
                    }
                    else
                    {
                        rowBottom--;
                    }
                }

                var r = new Bitmap((columnRight - columnLeft) * _item.PointSize, (rowBottom - rowTop)*_item.PointSize);
                using (var g = Graphics.FromImage(r))
                {
                    g.DrawImage(b, new Point(-(columnLeft*_item.PointSize), -(rowTop*_item.PointSize)));
                }
                _result = r;
            }
        }
    }
}
