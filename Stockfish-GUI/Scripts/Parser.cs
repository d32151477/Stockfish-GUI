using OpenCvSharp;
using OpenCvSharp.Extensions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using System.Text.RegularExpressions;
using System.Web.UI;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.TextBox;

namespace Stockfish_GUI
{
    internal static class Parser
    {
        private static Process Process { get; set; }
        private static Dictionary<string, Mat> Templates = new();
        private static Dictionary<string, Mat> RedTemplates = new();
        private static Dictionary<string, Mat> BlueTemplates = new();

        static Parser()
        {
            var files = Directory.GetFiles("./Templates/Kakao/", "*.png", SearchOption.TopDirectoryOnly);
            foreach (var file in files)
            {
                var name = Path.GetFileNameWithoutExtension(file);
                var mat = Cv2.ImRead(file, ImreadModes.Grayscale);
                Templates.Add(name, mat);

                if (name.StartsWith("red")) RedTemplates.Add(name, mat);
                else BlueTemplates.Add(name, mat);
            }
        }
        private static IntPtr GetHandle()
        {
            var parent = FindWindow("Qt5154QWindowOwnDCIcon", "BlueStacks App Player");
            var child = FindWindowEx(parent, 0, "Qt5154QWindowIcon", "HD-Player");

            return child;
        }
        private static Rectangle GetRectangleFromHandle(IntPtr handle)
        {
            using var graphics = Graphics.FromHwnd(handle);
            return Rectangle.Round(graphics.VisibleClipBounds);
        }
        private static Bitmap GetBitmap(IntPtr handle, Rectangle rect)
        {
            var bitmap = new Bitmap(rect.Width, rect.Height);
            using var graphics = Graphics.FromImage(bitmap);

            IntPtr hdc = graphics.GetHdc();
            PrintWindow(handle, hdc, 0x2);
            graphics.ReleaseHdc(hdc);

            return bitmap;
        }
        private static OpenCvSharp.Rect GetBoardRect(Mat mat)
        {
            using var hsv = mat.CvtColor(ColorConversionCodes.BGR2HSV);
            using var h = hsv.ExtractChannel(0);
            using var ptr = new MatPtr(h);

            for (int i = 0; i < h.Height; i++)
                if (ptr.At(0, i) > 5)
                    return new OpenCvSharp.Rect(0, i, mat.Width, mat.Width);

            return OpenCvSharp.Rect.Empty;
        }
        public unsafe static void Execute()
        {
            var handle = GetHandle();
            var windowRect = GetRectangleFromHandle(handle);

            var bitmap = GetBitmap(handle, windowRect);
            using var mat = bitmap.ToMat();

            var boardRect = GetBoardRect(mat);

            using var board = mat[boardRect];
            int width = (int)Math.Round(board.Width / (float)Board.kWidth);
            int height = (int)Math.Round(board.Height / (float)Board.kHeight);
            var space = new OpenCvSharp.Size(width, height);

            /* Test Code
            using var r = board.ExtractChannel(0);
            using var m1 = r.InRange(0, 32);
            
            using var b = board.ExtractChannel(2);
            using var m2 = b.InRange(0, 32);
            
            using var expr = m1 | m2;
            using var m3 = expr.ToMat();
            
            for (int y = 0; y < Board.kHeight; y++)
            {
                for (int x = 0; x < Board.kWidth; x++)
                {
                    //Cv2.Rectangle(board, new Rect(width * x, height * y, width, height), Scalar.Black);
            
                    m3[new Rect(width * x, height * y, width, height)].SaveImage($"{x}_{y}.png");
                }
            }
            Cv2.ImShow("test", board);
            */ 

            Dictionary<OpenCvSharp.Point, char> R;
            using (var r = board.ExtractChannel(0))
            using (var inrange = r.InRange(0, 32))
                R = GetPieces(inrange, space, RedTemplates);
            
            Dictionary<OpenCvSharp.Point, char> B;
            using (var b = board.ExtractChannel(2))
            using (var inrange = b.InRange(0, 32))
                B = GetPieces(inrange, space, BlueTemplates);
            
            var map = new char[Board.kWidth, Board.kHeight];
            
            for (int y = 0; y < Board.kHeight; y++)
            {
                for (int x = 0; x < Board.kWidth; x++)
                {
                    var point = new OpenCvSharp.Point(x, y);
                    var type = default(char);
                    if (R.TryGetValue(point, out var t)) type = t;
                    else if (B.TryGetValue(point, out t)) type = t;
                    
                    map[point.X, point.Y] = type;
                }
            }
            if (NeedSwap(map))
            {
                Core.Form.Board.Swapped = true;
                for (int y = 0; y < Board.kHeight; y++)
                {
                    for (int x = 0; x < Board.kWidth; x++)
                    {
                        if (char.IsUpper(map[x, y]))
                            map[x, y] = char.ToLower(map[x, y]);
                        else
                            map[x, y] = char.ToUpper(map[x, y]);
                    }
                }
            }

            Core.Form.Board.Reset();
            Core.Form.Board.Pause();
            
            var fen = Board.GetFen(map, false, 1, 0);
            Core.Form.Board.Create(fen);
            Core.Form.Board.Start();
        }
        private static bool NeedSwap(char[,] map)
        {
            for (int y = 0; y < Board.kHeight; y++)
            {
                for (int x = 0; x < Board.kWidth; x++)
                {
                    if (map[x, y] == 'K')
                        return true;
                    else if (map[x, y] == 'k')
                        return false;
                }
            }
            return false;
        }
        public static Dictionary<OpenCvSharp.Point, char> GetPieces(Mat mat, OpenCvSharp.Size space, IDictionary<string, Mat> templates)
        {
            var pieces = new Dictionary<OpenCvSharp.Point, char>();
            foreach (var pair in templates)
            {
                while (true)
                {
                    var name = pair.Key;
                    var template = pair.Value;
                    using var resized = template.Resize(space);
                    using var cropped = resized[new Rect(resized.Width / 4, resized.Height / 4, resized.Width / 2, resized.Height / 2)];


                    var match = MatchTemplate(mat, cropped, TemplateMatchModes.SqDiffNormed);
                    if (match.Accuracy < 0.3f)
                        break;

                    var center = match.Center;
                    var area = new Rect(center.X - space.Width / 2, center.Y - space.Height / 2, space.Width, space.Height);
                    Cv2.Rectangle(mat, area, Scalar.Black, thickness: -1);

                    int x = match.Center.X / space.Width;
                    int y = match.Center.Y / space.Height;
                    var point = new OpenCvSharp.Point(x, y);
                    pieces.Add(point, Board.PieceTypesByName[name]);
                }
            }
            return pieces;
        }

        public unsafe class MatPtr : IDisposable
        {
            public Mat Mat;
            public byte* Ptr;

            public int Width;
            public int Elements;

            public unsafe MatPtr(Mat mat)
            {
                Mat = mat;
                Ptr = Mat.DataPointer;

                Width = Mat.Cols;
                Elements = Mat.ElemSize();
            }
            public void Dispose()
            {
                Mat?.Dispose();
            }
            public byte At(int x, int y)
            {
                return Ptr[y * Width * Elements + x * Elements];
            }
        }

        public struct MatchTemplateInfo
        {
            public double Accuracy;
            public OpenCvSharp.Rect Area;

            public OpenCvSharp.Point Position => Area.Location;
            public OpenCvSharp.Size Size => Area.Size;
            public OpenCvSharp.Point Center => new OpenCvSharp.Point(Area.X + Area.Width / 2, Area.Y + Area.Height / 2);
        }
        public static MatchTemplateInfo MatchTemplate(Mat source, Mat template, TemplateMatchModes mode = TemplateMatchModes.CCoeffNormed, Mat mask = null)
        {
            double accuracy;
            OpenCvSharp.Point position;

            using var mat = source.MatchTemplate(template, mode, mask);
            switch (mode)
            {
                case TemplateMatchModes.SqDiffNormed:
                    Cv2.MinMaxLoc(mat, out accuracy, out _, out position, out _);
                    return new MatchTemplateInfo()
                    {
                        Accuracy = 1.0f - accuracy,
                        Area = new OpenCvSharp.Rect(position, new OpenCvSharp.Size(template.Width, template.Height)),
                    };
                default:
                    Cv2.MinMaxLoc(mat, out _, out accuracy, out _, out position);
                    return new MatchTemplateInfo()
                    {
                        Accuracy = accuracy,
                        Area = new OpenCvSharp.Rect(position, new OpenCvSharp.Size(template.Width, template.Height)),
                    };
            }
        }

        // RECT 구조체 정의
        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [DllImport("User32", EntryPoint = "FindWindow")]
        private static extern IntPtr FindWindow(string IpClassName, string IpWindowName);

        [DllImport("user32")]
        private static extern IntPtr FindWindowEx(IntPtr hWnd1, int hWnd2, string lp1, string lp2);

        [DllImport("user32.dll")]
        internal static extern bool PrintWindow(IntPtr hwnd, IntPtr hdcblt, int nFlags);

    }
}
