using OpenCvSharp;
using OpenCvSharp.Extensions;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace Stockfish_GUI
{
    // 블루스택 카카오 장기의 보드를 폼 보드와 실시간으로 동기화 하기 위해 구현되었습니다.
    public static class Parser
    {
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
        public struct MatchTemplateInfo
        {
            public double Accuracy;
            public Rect Area;

            public OpenCvSharp.Point Position => Area.Location;
            public OpenCvSharp.Size Size => Area.Size;
            public OpenCvSharp.Point Center => new(Area.X + Area.Width / 2, Area.Y + Area.Height / 2);
        }

        private static List<PieceTemplateInfo> TemplateInfos;
        private static readonly Dictionary<Color, List<PieceTemplateInfo>> TemplateInfosByColor = new();

        private static readonly Mat TemplateTurn = new();
        private static readonly System.Windows.Forms.Timer Tracer = new();
        private static Board Board => Core.Form.Board;

        private static bool Turn;
        private static bool NeedSwapping;
        private static Rect BoardArea;
        private static Dictionary<OpenCvSharp.Point, char> Pieces = new();

        private delegate Mat GetPieceHandler(Mat hsv);

        public static readonly Dictionary<string, char> PieceTypesByName = new()
        {
            ["blue_chariot"] = 'R',
            ["blue_elephant"] = 'B',
            ["blue_horse"] = 'N',
            ["blue_advisor"] = 'A',
            ["blue_king"] = 'K',
            ["blue_king_small"] = 'K',
            ["blue_cannon"] = 'C',
            ["blue_pawn"] = 'P',

            ["red_chariot"] = 'r',
            ["red_elephant"] = 'b',
            ["red_horse"] = 'n',
            ["red_advisor"] = 'a',
            ["red_king"] = 'k',
            ["red_king_small"] = 'k',
            ["red_cannon"] = 'c',
            ["red_pawn"] = 'p',
        };

        static Parser()
        {
            Tracer.Interval = 100;
            Tracer.Enabled = true;
            Tracer.Tick += (sender, e) => Trace();

            TemplateTurn = Cv2.ImRead("./Templates/turn.grayscale.png", ImreadModes.Grayscale);
        }

        public static void Execute()
        {
            if (Init())
                Tracer.Start();
        }

        // 실시간으로 보드에서 턴이 변경되었을 경우에 이동된 말의 좌표들 추적하여 폼의 보드와 동기화합니다.
        public static void Trace()
        {
            try
            {
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
                var focusType = GetPieceType(board, focus);
                if (focusType == default)
                    return;

                if (!Board.IsManipulable(focusType))
                    return;

                var previous = Pieces;
                var current = GetPieces(board);

                foreach (var pair in previous)
                {
                    var point = pair.Key;
                    var type = pair.Value;

                    if (current.ContainsKey(point))
                        continue;

                    if (!Board.IsManipulable(type))
                        continue;

                    if (Board.IsDifferentType(type, focusType))
                        continue;

                    var from = new System.Drawing.Point(point.X, point.Y);
                    var to = new System.Drawing.Point(focus.X, focus.Y);

                    while (Board.Redo()) ;
                    try { Board.MoveTo(from, to, animate: true); }
                    catch { return; }

                    Pieces = current;
                    Turn = turn;
                    break;
                }
            }
            catch
            {
                Tracer.Stop();
            }
        }

        private static bool IsOverlayVisible(Mat status)
        {
            using var hsv = status.CvtColor(ColorConversionCodes.BGR2HSV);
            using var v = status.ExtractChannel(2);

            using var ptr = new MatPtr(v);

            const int kOffset = 20;
            const int kValueDark = 41;
            int value = ptr.At(0, kOffset);
            if (value == kValueDark)
                return false;
            return true;
        }

        // 현재 플레이어의 턴인지 확인합니다.
        private static bool GetTurn(Mat status)
        {
            using var hsv = status.CvtColor(ColorConversionCodes.BGR2HSV);
            
            var lower = new Scalar(10, 225, 200);
            var upper = new Scalar(20, 255, 255);

            using var inrange = hsv.InRange(lower, upper);
            
            var partArea = new Rect(inrange.Width / 4, 0, inrange.Width / 2, inrange.Height);
            using var part = inrange[partArea];

            var match = MatchTemplate(part, TemplateTurn, TemplateMatchModes.SqDiffNormed);
            if (match.Accuracy > 0.8)
                return true;
            return false;
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
        public static bool Init()
        {
            try
            {
                using var mat = Capture();

                BoardArea = GetBoardArea(mat);
                if (BoardArea == OpenCvSharp.Rect.Empty)
                    return false;

                using var board = mat[BoardArea];
                InitTemplates(board);

                var statusArea = GetStatusArea(mat, BoardArea);
                using var status = mat[statusArea];

                Pieces = GetPieces(board);
                Turn = GetTurn(status);

                var map = new char[Board.kWidth, Board.kHeight];
                CopyTo(map, Pieces);
                Board.Reset();
                Board.Pause();

                var fen = Board.GetFen(map, Turn, 1, 0);
                Board.Create(fen);
                Board.Start();

                return true;
            }
            catch { return false; }
        }
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

                Mat binary;
                if (info.Color == Color.Red)
                    binary = GetRedFromHSV(hsv);
                else
                {
                    using var b = GetBlueFromHSV(hsv);
                    using var g = GetGreenFromHSV(hsv);

                    using var expr = b | g;

                    binary = expr.ToMat();
                }
                using (binary)
                {
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


        // 카카오 장기에서 움직인 말의 강조 표시가 있을 경우에 해당 좌표를 가져옵니다.
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

        // 해당 좌표의 말 유형을 가져옵니다.
        public static char GetPieceType(Mat board, OpenCvSharp.Point point)
        {
            double width = Math.Ceiling(board.Width / (double)Board.kWidth);
            double height = board.Height / (double)Board.kHeight;
            var size = new OpenCvSharp.Size(width, height);

            var blockArea = new Rect(size.Width * point.X, size.Height * point.Y, size.Height, size.Height);
            using var block = board[blockArea];
            using var hsv = block.CvtColor(ColorConversionCodes.BGR2HSV);

            using var R = GetRedFromHSV(hsv);

            var type = GetPieceTypeFromMat(R, TemplateInfosByColor[Color.Red]);
            if (type == default)
            {
                using var B = GetBlueFromHSV(hsv);
                using var G = GetGreenFromHSV(hsv);
                using var expr = B | G;
                using var BG = expr.ToMat();

                type = GetPieceTypeFromMat(BG, TemplateInfosByColor[Color.Blue]);
                if (type == default)
                {
                    using var W = GetWhiteFromHSV(hsv);
                    type = GetPieceTypeFromMat(W, TemplateInfosByColor[Color.Blue]);

                    if (type == default)
                        type = GetPieceTypeFromMat(W, TemplateInfosByColor[Color.Red]);
                }

            }

            if (NeedSwapping)
                return char.IsUpper(type) ? char.ToLower(type) : char.ToUpper(type);
            return type;
        }

        // 모든 좌표의 말 유형을 가져옵니다.
        public static Dictionary<OpenCvSharp.Point, char> GetPieces(Mat board)
        {
            double width = Math.Ceiling(board.Width / (double)Board.kWidth);
            double height = board.Height / (double)Board.kHeight;
            var size = new OpenCvSharp.Size(width, height);

            using var hsv = board.CvtColor(ColorConversionCodes.BGR2HSV);
            using var R = GetRedFromHSV(hsv);

            var reds = GetPiecesFromMat(R, TemplateInfosByColor[Color.Red], size);
            if (reds.Count == 0)
            {
                using var W = GetWhiteFromHSV(hsv);
                reds = GetPiecesFromMat(W, TemplateInfosByColor[Color.Red], size);
            }

            using var B = GetBlueFromHSV(hsv);
            using var G = GetGreenFromHSV(hsv);
            using var expr = B | G;
            using var BG = expr.ToMat();

            var blues = GetPiecesFromMat(BG, TemplateInfosByColor[Color.Blue], size);
            if (blues.Count == 0)
            {
                using var W = GetWhiteFromHSV(hsv);
                blues = GetPiecesFromMat(W, TemplateInfosByColor[Color.Blue], size);
            }

            var pieces = Enumerable.Concat(reds, blues).ToDictionary(i => i.Key, i => i.Value);
            SwapIfNeed(ref pieces);

            return pieces;
        }

        // 색상 변경이 필요한 지 확인합니다.
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
                return NeedSwapping = true;
            return NeedSwapping = false;
        }

        // 카카오 장기에서 상대 팀이 초 진형(파란색 혹은 초록색)이라면 한 진형으로 변경하고 색상 변경합니다.
        private static void SwapIfNeed(ref Dictionary<OpenCvSharp.Point, char> pieces)
        {
            var swap = NeedSwap(pieces);
            if (swap)
                pieces = pieces.ToDictionary(i => i.Key, i => char.IsLower(i.Value) ? char.ToUpper(i.Value) : char.ToLower(i.Value));
            Board.Swapped = swap;
        }

        private static Mat GetRedFromHSV(Mat hsv)
        {
            var lower = new Scalar(0, 100, 0);
            var upper = new Scalar(10, 255, 255);

            return hsv.InRange(lower, upper);
        }
        private static Mat GetGreenFromHSV(Mat hsv)
        {
            var lower = new Scalar(40, 100, 0);
            var upper = new Scalar(60, 255, 255);

            return hsv.InRange(lower, upper);
        }
        private static Mat GetBlueFromHSV(Mat hsv)
        {
            var lower = new Scalar(100, 100, 0);
            var upper = new Scalar(130, 255, 255);

            return hsv.InRange(lower, upper);
        }
        private static Mat GetWhiteFromHSV(Mat hsv)
        {
            var lower = new Scalar(0, 0, 0);
            var upper = new Scalar(255, 30, 255);

            return hsv.InRange(lower, upper);
        }

        // 템플릿 이미지와 비교하여 제일 닮은 꼴의 말 유형을 가져옵니다.
        private static char GetPieceTypeFromMat(Mat mat, List<PieceTemplateInfo> templateInfos)
        {
            var similar = default(PieceMatchTemplateInfo);
            foreach (var info in templateInfos)
            {
                var match = MatchTemplate(mat, info.Mat, TemplateMatchModes.CCoeffNormed);
                if (match.Accuracy > similar.Accuracy)
                {
                    similar = new PieceMatchTemplateInfo()
                    {
                        Accuracy = match.Accuracy,
                        Info = info,
                    };
                }
                if (match.Accuracy >= 0.5f)
                    break;
            }

            if (similar.Accuracy < 0.5f)
                return default;

            var type = PieceTypesByName[similar.Info.Name];
            return type;
        }
        private static Dictionary<OpenCvSharp.Point, char> GetPiecesFromMat(Mat mat, List<PieceTemplateInfo> templateInfos, OpenCvSharp.Size size)
        {
            var pieces = new Dictionary<OpenCvSharp.Point, char>();
            for (int y = 0; y < Board.kHeight; y++)
            {
                for (int x = 0; x < Board.kWidth; x++)
                {
                    var blockArea = new Rect(size.Width * x, size.Height * y, size.Height, size.Height);
                    using var block = mat[blockArea];

                    var type = GetPieceTypeFromMat(block, templateInfos);
                    if (type == default)
                        continue;
                    
                    var point = new OpenCvSharp.Point(x, y);
                    pieces[point] = type;
                }
            }
            return pieces;
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
                        Area = new Rect(position, new OpenCvSharp.Size(template.Width, template.Height)),
                    };
                default:
                    Cv2.MinMaxLoc(mat, out _, out accuracy, out _, out position);
                    return new MatchTemplateInfo()
                    {
                        Accuracy = accuracy,
                        Area = new Rect(position, new OpenCvSharp.Size(template.Width, template.Height)),
                    };
            }
        }

        // 블루스택 핸들을 가져옵니다.
        private static IntPtr GetHandle()
        {
            var parent = FindWindow("Qt5154QWindowOwnDCIcon", "BlueStacks App Player");
            var child = FindWindowEx(parent, 0, "Qt5154QWindowIcon", "HD-Player");

            return child;
        }

        // 블루스택 이미지 크기를 가져옵니다.
        private static Rectangle GetRectangleFromHandle(IntPtr handle)
        {
            using var graphics = Graphics.FromHwnd(handle);
            return Rectangle.Round(graphics.VisibleClipBounds);
        }

        // 블루스택 이미지 비트맵을 가져옵니다.
        private static Bitmap GetBitmap(IntPtr handle, Rectangle rect)
        {
            var bitmap = new Bitmap(rect.Width, rect.Height);
            using var graphics = Graphics.FromImage(bitmap);

            IntPtr hdc = graphics.GetHdc();
            PrintWindow(handle, hdc, 0x2);
            graphics.ReleaseHdc(hdc);

            return bitmap;
        }

        // 보드 이미지 영역을 계산합니다.
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

        private static Rect GetStatusArea(Mat mat, Rect boardArea)
        {
            return new Rect(boardArea.X, boardArea.Bottom, boardArea.Width, mat.Height - boardArea.Bottom - 1);
        }


        [DllImport("User32", EntryPoint = "FindWindow")]
        private static extern IntPtr FindWindow(string IpClassName, string IpWindowName);

        [DllImport("user32")]
        private static extern IntPtr FindWindowEx(IntPtr hWnd1, int hWnd2, string lp1, string lp2);

        [DllImport("user32.dll")]
        public static extern bool PrintWindow(IntPtr hwnd, IntPtr hdcblt, int nFlags);

        [StructLayout(LayoutKind.Sequential)]
        private struct lpRect
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }
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
}
