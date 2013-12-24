using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
//using PixelTuner.Drawing.Properties;

namespace PixelTuner.Drawing
{
    public partial class TextDialog : Form
    {
        ConfigurationItem _item = null;
        public static int PointSize = 6;
        int PatternLength = 116;
        float emSize = 14;
        string _displayedText = "";        
        int _targetPointSize = 6;
        int _linesCount = 25;
        Color GridColorLight = Color.FromArgb(255, 60, 60, 60);
        Color _drawColor = Color.White;
        Font _font = new Font("Arial", 24);
        bool _cursorVisible = false;
        int _topCorrection = 0;
        Rectangle _cursorPosition = new Rectangle(1, 0, 2, 40);
        const int _cursorDelta = 1;
        int position = 12;
        Image _tempImage = null;
        Image _result = null;
        public Image TextImage
        {
            get { return _result; }
        }

        Dictionary<int, float> _pointsToEMSize = new Dictionary<int, float>();
        void InitDictionary()
        {
            _pointsToEMSize.Clear();
            _pointsToEMSize.Add(6, 8);
            _pointsToEMSize.Add(7, 10);
            _pointsToEMSize.Add(8, 11);
            _pointsToEMSize.Add(9, 12);
            _pointsToEMSize.Add(10, 14);
            _pointsToEMSize.Add(11, 15);
            _pointsToEMSize.Add(12, 16);
            _pointsToEMSize.Add(13, 18);
            _pointsToEMSize.Add(14, 19);
            _pointsToEMSize.Add(15, 20);
            _pointsToEMSize.Add(16, 22);
            _pointsToEMSize.Add(17, 23);
            _pointsToEMSize.Add(18, 24);
            _pointsToEMSize.Add(19, 26);
            _pointsToEMSize.Add(20, 27);
            _pointsToEMSize.Add(21, 29);
            _pointsToEMSize.Add(22, 30);
            _pointsToEMSize.Add(23, 31);
            _pointsToEMSize.Add(24, 32);
        }

        public static int CursorStartPosition
        {
            get { return 2 * PointSize; }
        }

        public TextDialog(ConfigurationItem item, Font initialValues, int pointSize, int linesCount, Color c)
        {
            _drawColor = c;
            _item = item;
            _linesCount = linesCount;
            //emSize = initialValues.Size;

            InitDictionary();
            InitializeComponent();
            PointSize = pointSize;
            //PointSize = _targetPointSize = (boxCanvas.Height - 20) / linesCount;
            PatternLength = boxCanvas.Width / PointSize;
            MakeImage();
            InitControls();

            _cursorPosition = new Rectangle(CursorStartPosition + _cursorDelta, 0, 2, (int)boxHeight.Value * PointSize);
            position = CursorStartPosition;

            boxFontFamily.SelectedIndex = boxFontFamily.FindString(initialValues.FontFamily.Name);
            boxFontType.SelectedIndex = boxFontType.FindString(initialValues.Style.ToString());
            if (_pointsToEMSize.ContainsValue(emSize))
            {
                foreach (KeyValuePair<int, float> kvp in _pointsToEMSize)
                    if (kvp.Value == emSize) { boxHeight.Value = kvp.Key; break; }
            }
            else boxHeight.Value = (int)emSize - 8;

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
            _tempImage = new Bitmap(PatternLength * PointSize, _item.PatternLines * PointSize);
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
            g.FillRectangle(new SolidBrush(color), 0, 0, PatternLength * PointSize, _item.PatternLines * PointSize);
            boxCanvas.Invalidate();
            g.Dispose();
        }

        /// <summary>
        /// Заливка одной ячейки по координатам пиксела
        /// </summary>
        /// <param name="parameters">Graphics, Color, X, Y</param>
        void FillPixel(Color c, int x, int y)
        {
            if (_tempImage == null) return;
            Graphics g = Graphics.FromImage(_tempImage);
            Rectangle fillArea = GetSquare(x, y);
            g.FillRectangle(new SolidBrush(c), fillArea);
            boxCanvas.Invalidate(fillArea);
            boxCanvas.Update();
        }

        private void DrawGrid(Graphics graphics)
        {
            if (graphics == null) return;

            // vertical
            for (int i = 0; i < boxCanvas.Width; ++i)
                graphics.DrawLine(new Pen(GridColorLight),
                    i * PointSize,
                    0,
                    i * PointSize,
                    PointSize * _linesCount);

            // horizontal
            for (int i = 0; i < _linesCount; ++i)
                graphics.DrawLine(new Pen(GridColorLight),
                    0,
                    i * PointSize,
                    boxCanvas.Width,
                    i * PointSize);
        }

        private Rectangle GetSquare(int x, int y)
        {
            int squaresX = x / PointSize;
            if (x % PointSize == 0) squaresX--;

            int squaresY = y / _item.PointSize;
            if (y % PointSize == 0) squaresY--;

            Rectangle result = new Rectangle(
                squaresX * PointSize + 1,
                squaresY * PointSize + 1,
                PointSize - 1,
                PointSize - 1);

            return result;
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

            Bitmap b = new Bitmap(50, 50);
            Graphics g = Graphics.FromImage(b);
            g.DrawString("Q", _font, new SolidBrush(Color.White), new PointF(0, 0));
            g.Dispose();
            int min = b.Height;
            for (int i = 0; i < b.Width; i++)
                for (int j = 0; j < b.Height; j++)
                {
                    if (b.GetPixel(i, j).A > 125)
                        if (j < min) min = j;
                }
            _topCorrection = min;
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
            if ((int)boxHeight.Value > 5 && (int)boxHeight.Value < 25)
                emSize = _pointsToEMSize[(int)boxHeight.Value];
            else emSize = (int)boxHeight.Value + 8;
            _cursorPosition = new Rectangle(_cursorPosition.X, _cursorPosition.Y, _cursorPosition.Width, PointSize * (int)boxHeight.Value);
            UpdateFontStyle();
        }

        private void boxSpacing_ValueChanged(object sender, EventArgs e)
        {
            boxLetters.Select();
            position = CursorStartPosition;
            FillCanvas(Color.Black);
            DrawText(Graphics.FromImage(_tempImage), boxLetters.Text, PointSize);
        }

        private void boxHScale_ValueChanged(object sender, EventArgs e)
        {
            boxLetters.Select();
        }

        private void boxVScale_ValueChanged(object sender, EventArgs e)
        {
            boxLetters.Select();
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
            if (boxLetters.Text.Length < _displayedText.Length)
            {
                position = CursorStartPosition;
                FillCanvas(Color.Black);
                DrawText(Graphics.FromImage(_tempImage), boxLetters.Text, PointSize);
            }
            else
            {
                string s = boxLetters.Text.Substring(_displayedText.Length);
                DrawText(Graphics.FromImage(_tempImage), s, PointSize);
            }
        }

        private void DrawText(Graphics g, string text, int targetPointSize)
        {
            for (int i = 0; i < text.Length; i++)
            {
                position += DrawLetter(g, text[i], _font, targetPointSize,
                    (int)boxHeight.Value, position) + (int)boxSpacing.Value * targetPointSize;
                
                _cursorPosition = new Rectangle(position + _cursorDelta, _cursorPosition.Y, 
                    _cursorPosition.Width, _cursorPosition.Height);
            }
            _displayedText = boxLetters.Text;
            boxCanvas.Invalidate();
        }

        private int DrawLetter(Graphics g, char letter, Font font, int pointSize, int height, int position)
        {
            Bitmap uncuttedLetterPic = new Bitmap(200, 200);
            //g.DrawString(letter.ToString(), font, new SolidBrush(Color.White), new PointF(0, 0));
            //return 50;

            Graphics tmpG = Graphics.FromImage(uncuttedLetterPic);
            tmpG.DrawString(letter.ToString(), font, new SolidBrush(_drawColor), new PointF(0, 0));
            tmpG.Dispose();

            float xScale = (float)boxHScale.Value / (float)100;
            float yScale = (float)boxVScale.Value / (float)100;

            Bitmap pixelated = Pexelate(uncuttedLetterPic, pointSize);
            Rectangle sourceRect = FindSourceRect(pixelated);
            sourceRect = new Rectangle(sourceRect.X, sourceRect.Y, (sourceRect.Width / pointSize + 1) * pointSize, sourceRect.Height);

            g.DrawImage(pixelated, new Point[] {
                    new Point(position, 0), 
                    new Point(position + sourceRect.Width), 
                    new Point(position, sourceRect.Height)}, 
                sourceRect, GraphicsUnit.Pixel);

            return sourceRect.Width;
        }

        private Bitmap Pexelate(Bitmap b, int pointSize)
        {
            int min = b.Width;
            int max = 0;

            Rectangle pix = new Rectangle(0, 0, pointSize, pointSize);

            for (int i = 0; i < b.Width; i++)
                for (int j = 0; j < b.Height; j++)                
                    if (b.GetPixel(i, j).A > 125)
                    {
                        if (j < min) min = j;
                        if (j > max) max = j;
                    }

            int width = max > min ? max - min : 24;

            // уточнить коэффициэнт или коррекцию при вычислении ширины: 4 * width * pointSize
            // влияет на правую границу
            float k = 1 / emSize * 240; // придуман навскидку, весьма примерен
            Bitmap result = new Bitmap( (int)k * width * pointSize, b.Height * pointSize );
            Graphics g = Graphics.FromImage(result);

            for (int i = 0; i < b.Width; i++)
                for (int j = 0; j < b.Height; j++)                
                    if (b.GetPixel(i, j).A > 125)
                    {
                        // выяснить, почему надо добавлять +5, и почему величина всегда разная, влияет на левую границу
                        pix.X = (i - min + 25) * pointSize;
                        pix.Y = (j - _topCorrection) * pointSize;
                        g.FillRectangle(new SolidBrush(_drawColor), pix);
                    }

            g.Dispose();
            return result;
        }

        /// <summary>
        /// Самое тупое и долгое место, где делается оценка размеров пикселизованной буквы
        /// </summary>
        /// <param name="b">Буква для вычисления размеров</param>
        /// <returns>Прямоугольник заключающий букву без полей</returns>
        private Rectangle FindSourceRect(Bitmap b)
        {
            int minLeft = b.Width;
            int minRight = b.Width;
            int minBottom = b.Height;

            for (int r = 0; r < b.Height; r++)
                for (int c = 0; c < b.Width; c++)
                {
                    if (b.GetPixel(c, r).A > 125)
                    {
                        if (c < minLeft) minLeft = c;
                        break;
                    }
                }

            // если подсунули пустой битмап:
            if (minLeft == b.Width) return new Rectangle(0, 0, 4 * PointSize, b.Height);

            for (int r = 0; r < b.Height; r++)
                for (int c = b.Width - 1; c >= 0; c--)
                {
                    if (b.GetPixel(c, r).A > 125)
                    {
                        if (minRight > b.Width - c) minRight = b.Width - c;
                        break;
                    }
                }

            Rectangle rect = new Rectangle(minLeft, 0, b.Width - minLeft - minRight, b.Height);
            //Rectangle rect = new Rectangle(0, 0, b.Width, b.Height);
            return rect;
        }

        private Rectangle CalcDestinationRect(Bitmap b, Rectangle sourceRect, int position, int pointSize, float xScale, float yScale)
        {
            Rectangle rect = new Rectangle(position, 0, (int)(sourceRect.Width * pointSize * xScale), (int)(sourceRect.Height * pointSize * yScale));
            return rect;
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
            _result = new Bitmap(position / PointSize * _targetPointSize, _tempImage.Height / PointSize * _targetPointSize);
            position = 0;
            DrawText(Graphics.FromImage(_result), boxLetters.Text, _targetPointSize);
        }
    }
}
