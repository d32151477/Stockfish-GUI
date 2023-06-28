using OpenCvSharp;
using OpenCvSharp.Extensions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Runtime.InteropServices;
using System.Timers;
using System.Xml.Linq;

namespace Stockfish_GUI
{
    internal static class Parser
    {
        private static Mat TurnTemplate = new();
        private static Dictionary<string, Mat> Templates = new();
        private static Dictionary<string, Mat> RedTemplates = new();
        private static Dictionary<string, Mat> BlueTemplates = new();

        private static bool Turn;
        private static bool PauseTracing;
        private static DateTime PausedTime;
        private static System.Windows.Forms.Timer Tracer = new();
        private static Board Board => Core.Form.Board;
        private static Dictionary<OpenCvSharp.Point, char> Pieces = new();
        static Parser()
        {
            Tracer.Interval = 100;
            Tracer.Enabled = true;
            Tracer.Tick += (sender, e) => Trace();

            TurnTemplate = Cv2.ImRead("./Templates/turn.grayscale.png", ImreadModes.Grayscale);
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
        private static Rect GetBoardArea(Mat mat)
        {
            using var hsv = mat.CvtColor(ColorConversionCodes.BGR2HSV);
            using var h = hsv.ExtractChannel(0);
            using var ptr = new MatPtr(h);

            for (int i = 0; i < h.Height; i++)
                if (ptr.At(0, i) > 5)
                    return new Rect(0, i, mat.Width, mat.Width);

            return Rect.Empty;
        }
        public static void Execute()
        {
            Init();
            Tracer.Start();
        }
        public static void Trace()
        {
            if (PauseTracing)
            {
                var elapsed = DateTime.Now - PausedTime;
                if (elapsed.Seconds < 1.0)
                    return;
                PauseTracing = false;
            }

            using var mat = Capture();

            var statusArea = GetStatusArea(mat, BoardArea);
            using var status = mat[statusArea];

            if (IsOverlayVisible(status))
                return;

            var turn = GetTurn(status);
            if (Turn == turn)
                return;

            using var board = mat[BoardArea];
            var F = GetFocus(board);
            if (F is null)
                return;

            var focus = F.Value;

            var previous = Pieces;
            var current = GetPieces(board);

            foreach (var pair in previous)
            {
                var point = pair.Key;
                var type = pair.Value;

                if (current.ContainsKey(point))
                    continue;

                var from = new System.Drawing.Point(point.X, point.Y);
                var to = new System.Drawing.Point(focus.X, focus.Y);

                Board.MoveTo(from, to, animate: true);
                Pieces = current;
                Turn = turn;
                break;
            }
            if (Board.IsChecked())
            {
                PauseTracing = true;
                PausedTime = DateTime.Now;
            }
        }
        private static bool IsOverlayVisible(Mat status)
        {
            using var hsv = status.CvtColor(ColorConversionCodes.BGR2HSV);
            using var v = status.ExtractChannel(2);

            using var ptr = new MatPtr(v);

            const int kOffset = 20;
            const int kValue = 41;
            int value = ptr.At(0, kOffset);
            if (value == kValue)
                return false;
            return true;
        }
        private static bool GetTurn(Mat status)
        {
            using var hsv = status.CvtColor(ColorConversionCodes.BGR2HSV);
            
            var lower = new Scalar(10, 225, 200);
            var upper = new Scalar(20, 255, 255);
            using var inrange = hsv.InRange(lower, upper);

            var match = MatchTemplate(inrange, TurnTemplate, TemplateMatchModes.SqDiffNormed);
            if (match.Accuracy > 0.8)
                return true;
            return false;
        }
        private static Rect GetStatusArea(Mat mat, Rect boardArea)
        {
            return new Rect(boardArea.X, boardArea.Bottom, boardArea.Width, mat.Height - boardArea.Bottom - 1);
        }
        public static Mat Capture()
        {
            var handle = GetHandle();
            var windowRect = GetRectangleFromHandle(handle);
            if (windowRect.Size == System.Drawing.Size.Empty)
                throw new Exception();

            var bitmap = GetBitmap(handle, windowRect);
            var mat = bitmap.ToMat();

            return mat;
        }
        public static OpenCvSharp.Point? GetFocus(Mat board)
        {
            using var hsv = board.CvtColor(ColorConversionCodes.BGR2HSV);
            var lower = new Scalar(20, 0, 250);
            var upper = new Scalar(30, 255, 255);

            using var inrange = hsv.InRange(lower, upper);
            
            using var kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new OpenCvSharp.Size(10, 10));
            using var dilate = inrange.Dilate(kernel, iterations: 1);

            Cv2.FindContours(dilate, out var contours, out var hierarchy, RetrievalModes.List, ContourApproximationModes.ApproxSimple);
            
            double maxArea = 0;
            int maxAreaContourIndex = -1;
            for (int i = 0; i < contours.Length; i++)
            {
                double area = Cv2.ContourArea(contours[i]);
                if (area > maxArea)
                {
                    maxArea = area;
                    maxAreaContourIndex = i;
                }
            }
            if (maxAreaContourIndex < 0)
                return null;
            
            var moments = Cv2.Moments(contours[maxAreaContourIndex]);
            var center = new OpenCvSharp.Point(moments.M10 / moments.M00, moments.M01 / moments.M00);
            
            double width = Math.Ceiling(board.Width / (double)Board.kWidth);
            double height = board.Height / (double)Board.kHeight;

            int x = (int)(center.X / width);
            int y = (int)(center.Y / height);

            return new OpenCvSharp.Point(x, y);
        }
        public static Dictionary<OpenCvSharp.Point, char> GetPieces(Mat board)
        {
            double width = Math.Ceiling(board.Width / (double)Board.kWidth);
            double height = board.Height / (double)Board.kHeight;

            using var hsv = board.CvtColor(ColorConversionCodes.BGR2HSV);
            
            var space = new OpenCvSharp.Size(width, height);

            var pieces = new Dictionary<OpenCvSharp.Point, char>();
            using var R = GetRedFromHSV(hsv);
            GetPiecesFromMat(pieces, R, TemplateInfosByColor[Color.Red], space);

            using var blue = GetBlueFromHSV(hsv);
            using var green = GetGreenFromHSV(hsv);
            var expr = blue | green;

            using var BG = expr.ToMat();
            GetPiecesFromMat(pieces, BG, TemplateInfosByColor[Color.Blue], space);

            SwapIfNeed(ref pieces);

            return pieces;
        }
        public static Rect BoardArea;
        public static void InitTemplates(Mat board)
        {
            if (TemplateInfos == null)
            {
                TemplateInfos = new List<PieceTemplateInfo>();
                var files = Directory.GetFiles("./Templates/Pieces/", "*.png", SearchOption.TopDirectoryOnly);
                foreach (var file in files)
                {
                    var name = Path.GetFileNameWithoutExtension(file);
                    var color = name.StartsWith("red") ? Color.Red : Color.Blue;
                    var mat = Cv2.ImRead(file, ImreadModes.Unchanged);

                    TemplateInfos.Add(new PieceTemplateInfo()
                    {
                        Name = name,
                        Color = color,
                        Mat = mat,
                    });
                }
            }
            TemplateInfosByColor.Clear();

            double height = board.Height / (double)Board.kHeight;
            var size = new OpenCvSharp.Size(height, height);

            foreach (var info in TemplateInfos)
            {
                using var resize = info.Mat.Resize(size);
                using var hsv = resize.CvtColor(ColorConversionCodes.BGR2HSV);
                using var binary = info.Color == Color.Red ? GetRedFromHSV(hsv) : GetBlueFromHSV(hsv);

                var croppedArea = new Rect(binary.Width / 4, binary.Height / 4, binary.Width / 2, binary.Height / 2);

                if (!TemplateInfosByColor.TryGetValue(info.Color, out var container))
                    TemplateInfosByColor[info.Color] = container = new List<PieceTemplateInfo>();

                container.Add(new PieceTemplateInfo()
                {
                    Name = info.Name,
                    Color = info.Color,
                    Mat = binary[croppedArea],
                });
            }
        }
        public static void Init()
        {
            using var mat = Capture();

            BoardArea = GetBoardArea(mat);
            if (BoardArea == OpenCvSharp.Rect.Empty)
                return; 

            using var board = mat[BoardArea];
            InitTemplates(board);

            var statusArea = GetStatusArea(mat, BoardArea);
            using var status = mat[statusArea];

            Turn = GetTurn(status);
            Pieces = GetPieces(board);

            var map = new char[Board.kWidth, Board.kHeight];
            CopyTo(map, Pieces);

            Board.Reset();
            Board.Pause();

            var fen = Board.GetFen(map, Turn, 1, 0);
            Board.Create(fen);
            Board.Start();
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
        private static bool NeedSwap(Dictionary<OpenCvSharp.Point, char> pieces)
        {
            var enumerable = from i in pieces
                             let p = i.Key
                             let t = i.Value
                             where char.ToLower(t) == 'k'
                             orderby p.Y
                             select i;

            var pair = enumerable.First();
            var type = pair.Value;
            if (type == 'K')
                return true;
            return false;
        }
        private static void SwapIfNeed(ref Dictionary<OpenCvSharp.Point, char> pieces)
        {
            var swap = NeedSwap(pieces);
            if (swap)
                pieces = pieces.ToDictionary(i => i.Key, i => char.IsLower(i.Value) ? char.ToUpper(i.Value) : char.ToLower(i.Value));
            Board.Swapped = swap;
        }
        private delegate Mat GetPieceHandler(Mat hsv);

        public enum PieceTemplateColor
        {
            Red, Blue
        }
        public struct PieceTemplateInfo
        {
            public Color Color;
            public string Name;
            public Mat Mat;
        }
        public struct PieceMatchTemplateInfo
        {
            public PieceTemplateInfo Info;
            public double Accuracy;
        }
        private static List<PieceTemplateInfo> TemplateInfos;
        private static Dictionary<Color, List<PieceTemplateInfo>> TemplateInfosByColor = new();
        private static void GetPiecesFromMat(Dictionary<OpenCvSharp.Point, char> pieces, Mat mat, List<PieceTemplateInfo> templateInfos, OpenCvSharp.Size space)
        {
            for (int y = 0; y < Board.kHeight; y++)
            {
                for (int x = 0; x < Board.kWidth; x++)
                {
                    var blockArea = new Rect(space.Width * x, space.Height * y, space.Width, space.Height);
                    using var block = mat[blockArea];

                    var similar = default(PieceMatchTemplateInfo);
                    foreach (var info in templateInfos)
                    {
                        var match = MatchTemplate(block, info.Mat, TemplateMatchModes.SqDiffNormed);
                        if (match.Accuracy > similar.Accuracy)
                        {
                            similar = new PieceMatchTemplateInfo()
                            {
                                Accuracy = match.Accuracy,
                                Info = info,
                            };
                        }
                    }
                    if (similar.Accuracy < 0.2f)
                        continue;

                    var point = new OpenCvSharp.Point(x, y);
                    var type = Board.PieceTypesByName[similar.Info.Name];

                    pieces[point] = type;
                }
            }
        }
        //private static void AppendToPieces(Mat hsv, Dictionary<OpenCvSharp.Point, char> pieces, OpenCvSharp.Size space, List<PieceTemplateInfo> templateInfos)
        //{
            //foreach (var pair in templates)
            //{
            //    var name = pair.Key;
            //    var template = pair.Value;
            //
            //    var size = new OpenCvSharp.Size(space.Height, space.Height);
            //    using var resize = template.Resize(size);
            //    using var hsv = resize.CvtColor(ColorConversionCodes.BGR2HSV);
            //    using var binary = handler?.Invoke(hsv);
            //    using var cropped = binary[new Rect(binary.Width / 4, binary.Height / 4, binary.Width / 2, binary.Height / 2)];
            //
            //    while (true)
            //    {
            //        var match = MatchTemplate(mat, cropped, TemplateMatchModes.SqDiffNormed);
            //        if (match.Accuracy < 0.5f)
            //            break;
            //
            //        var center = match.Center;
            //
            //        var area = new OpenCvSharp.Rect(center.X - binary.Width / 2, center.Y - binary.Height / 2, binary.Width, binary.Height);
            //        Cv2.Rectangle(mat, area, Scalar.Black, thickness: -1);
            //
            //        int x = center.X / space.Width;
            //        int y = center.Y / space.Height;
            //        var point = new OpenCvSharp.Point(x, y);
            //        pieces.Add(point, Board.PieceTypesByName[name]);
            //    }
            //}

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
