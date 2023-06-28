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
using System.Windows.Forms;
using System.Xml.Linq;
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
                var mat = Cv2.ImRead(file, ImreadModes.Unchanged);
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
            double width = Math.Ceiling(board.Width / (double)Board.kWidth);
            double height = board.Height / (double)Board.kHeight;
            var space = new OpenCvSharp.Size(width, height);

            using var hsv = board.CvtColor(ColorConversionCodes.BGR2HSV);
            using var red = GetRedFromHSV(hsv);
            using var blue = GetBlueFromHSV(hsv);
            using var green = GetGreenFromHSV(hsv);

            var map = new char[Board.kWidth, Board.kHeight];
            var pieces = GetPieces(red, space, RedTemplates, GetRedFromHSV);
            CopyTo(map, pieces);

            pieces = GetPieces(blue, space, BlueTemplates, GetBlueFromHSV);
            CopyTo(map, pieces);

            if (pieces.Count == 0)
                pieces = GetPieces(green, space, BlueTemplates, GetGreenFromHSV);
                CopyTo(map, pieces);

            SwapIfNeed(map);

            Core.Form.Board.Reset();
            Core.Form.Board.Pause();

            var fen = Board.GetFen(map, false, 1, 0);
            Core.Form.Board.Create(fen);
            Core.Form.Board.Start();
        }
        private static Mat GetRedFromHSV(Mat hsv)
        {
            var lower = new Scalar(0, 100, 15);
            var upper = new Scalar(10, 255, 255);

            return hsv.InRange(lower, upper);
        }
        private static Mat GetGreenFromHSV(Mat hsv)
        {
            var lower = new Scalar(40, 100, 15);
            var upper = new Scalar(70, 255, 255);

            return hsv.InRange(lower, upper);
        }
        private static Mat GetBlueFromHSV(Mat hsv)
        {
            var lower = new Scalar(100, 100, 15);
            var upper = new Scalar(130, 255, 255);

            return hsv.InRange(lower, upper);
        }
        private static void CopyTo(char[,] map, Dictionary<OpenCvSharp.Point, char> pieces)
        {
            for (int y = 0; y < Board.kHeight; y++)
            {
                for (int x = 0; x < Board.kWidth; x++)
                {
                    var point = new OpenCvSharp.Point(x, y);
                    if (pieces.TryGetValue(point, out var type)) 
                        map[point.X, point.Y] = type;
                }
            }
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
        private static void SwapIfNeed(char[,] map)
        {
            var swap = NeedSwap(map);
            if (swap)
            {
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
            Core.Form.Board.Swapped = swap;
        }
        private delegate Mat GetPieceHandler(Mat hsv);

        private static Dictionary<OpenCvSharp.Point, char> GetPieces(Mat mat, OpenCvSharp.Size space, IDictionary<string, Mat> templates, GetPieceHandler handler)
        {
            var pieces = new Dictionary<OpenCvSharp.Point, char>();
            foreach (var pair in templates)
            {
                var name = pair.Key;
                var template = pair.Value;

                var size = new OpenCvSharp.Size(space.Height, space.Height);
                using var hsv = template.CvtColor(ColorConversionCodes.BGR2HSV);
                using var binary = handler?.Invoke(hsv);
                using var resize = binary.Resize(size);

                while (true)
                {
                    var match = MatchTemplate(mat, resize, TemplateMatchModes.SqDiffNormed);
                    if (match.Accuracy < 0.5f)
                        break;

                    var center = match.Center;
                    Cv2.Rectangle(mat, match.Area, Scalar.Black, thickness: -1);

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
