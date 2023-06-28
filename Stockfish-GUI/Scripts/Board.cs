﻿using Newtonsoft.Json;
using Stockfish_GUI.Properties;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace Stockfish_GUI
{
    public class Board
    {
        public const int kWidth = 9;
        public const int kHeight = 10;

        private const int kBoardMargin = 25;
        private const int kBoardSpace = 50;

        private const int kSpotDiameter = 16;

        public const string kFen = "rnba1abnr/4k4/1c5c1/p1p1p1p1p/9/9/P1P1P1P1P/1C5C1/4K4/RNBA1ABNR w - - 0 1";

        private const int kArrowWidth = 16;
        private static readonly Color[] s_ArrowColors = new []{ Color.Orange, Color.DarkSlateBlue, };

        private static readonly Dictionary<char, Type> s_PieceTypes = new()
        {
            ['r'] = typeof(Chariot),
            ['b'] = typeof(Elephant),
            ['n'] = typeof(Horse),
            ['a'] = typeof(Advisor),
            ['k'] = typeof(King),
            ['c'] = typeof(Cannon),
            ['p'] = typeof(Pawn),

            ['R'] = typeof(Chariot),
            ['B'] = typeof(Elephant),
            ['N'] = typeof(Horse),
            ['A'] = typeof(Advisor),
            ['K'] = typeof(King),
            ['C'] = typeof(Cannon),
            ['P'] = typeof(Pawn),
        };
        private static readonly Dictionary<char, Bitmap> s_PieceBitmaps = new()
        {
            ['R'] = Resources.blue_chariot,
            ['B'] = Resources.blue_elephant,
            ['N'] = Resources.blue_horse,
            ['A'] = Resources.blue_advisor,
            ['K'] = Resources.blue_king,
            ['C'] = Resources.blue_cannon,
            ['P'] = Resources.blue_pawn,

            ['r'] = Resources.red_chariot,
            ['b'] = Resources.red_elephant,
            ['n'] = Resources.red_horse,
            ['a'] = Resources.red_advisor,
            ['k'] = Resources.red_king,
            ['c'] = Resources.red_cannon,
            ['p'] = Resources.red_pawn,
        };

        private bool Turn = true;
        private int Time = 1;
        private int Retry = 0;

        private int RenderOrder = 0;

        private bool Hover;
        private Piece HoverTarget;

        private bool Holding;
        private Point HoldingLocation;

        private Piece CurrentTarget;

        private int m_CurrentArrowIndex;
        public int CurrentArrowIndex 
        {
            get => m_CurrentArrowIndex;
            set
            {
                m_CurrentArrowIndex = value;
                View.Invalidate();
            }
        }

        private readonly Piece[,] Map = new Piece[kWidth, kHeight];
        private readonly Control View;
        private readonly Form Form;

        public readonly List<Piece> Pieces = new();

        private struct Line
        {
            public Color Color;
            public Point From;
            public Point To;
            public int Width;
        }

        private List<Line> Arrows = new();
        private List<Point> Spots = new();
        private List<Point> Footprints = new();

        public Board(Form form)
        {
            Form = form;
            View = form.BoardView;
            View.Paint += OnPaint;
            View.MouseDown += OnMouseDown;
            View.MouseMove += OnMouseMove;
            View.MouseUp += OnMouseUp;

            Animator.Start(View);
        }

        public void Start() => Start(kFen);
        public void Start(string fen)
        {
            int index;
            int x = 0;
            int y = 0;

            for (index = 0; index < fen.Length; index++)
            {
                char c = fen[index];
                if (s_PieceTypes.TryGetValue(c, out var type))
                {
                    var point = new Point(x, y);
                    var piece = Activator.CreateInstance(type) as Piece;

                    piece.Point = point;
                    piece.Type = c;

                    Pieces.Add(piece);
                    this[point] = piece;

                    x += 1;
                }
                else if (c == '/')
                {
                    x = 0;
                    y += 1;
                }
                else if (char.IsNumber(c))
                    x += c - '0';
                else
                    break;
            }

            var strs = fen.Substring(index + 1).Split();
            Turn = char.Parse(strs[0]) == 'w';
            Time = int.Parse(strs[strs.Length - 1]);
            Retry = int.Parse(strs[strs.Length - 2]);
        }
        private void OnPaint(object sender, PaintEventArgs e)
        {
            e.Graphics.DrawImage(Resources.board, Point.Empty);

            foreach (var point in Footprints)
            {
                var color = Color.FromArgb(128, Turn ? Color.Red : Color.Blue);

                using var brush = new SolidBrush(color);

                var size = new Size(kBoardSpace, kBoardSpace);
                var center = GetLocationFromPoint(point);
                var location = GetImageLocation(center, size);

                e.Graphics.FillRectangle(brush, location.X, location.Y, size.Width, size.Height);
            }
            if (Hover)
            {
                const int kSizeOffset = 2;

                var piece = HoverTarget;
                var diameter = piece.Diameter + kSizeOffset;
                var size = new Size(diameter, diameter);

                var center = GetLocationFromPoint(piece.Point);
                var location = GetImageLocation(center, size);

                var color = IsBlueTeam(piece.Type) ? Color.Blue : Color.Red;
                var gradients = new Color[] { Color.Transparent };

                using var path = new GraphicsPath();
                path.AddEllipse(location.X, location.Y, size.Width, size.Height);
                
                using var brush = new PathGradientBrush(path);
                brush.CenterColor = color;
                brush.SurroundColors = gradients; 
                brush.CenterPoint = center;
                brush.FocusScales = new PointF(0.5f, 0.5f);

                e.Graphics.FillEllipse(brush, location.X, location.Y, size.Width, size.Height);
            }
            if (CurrentTarget is not null)
            {
                var point = CurrentTarget.Point;
                var color = Color.FromArgb(128, Color.Green);
                using var brush = new SolidBrush(color);

                var size = new Size(kBoardSpace, kBoardSpace);
                var center = GetLocationFromPoint(point);
                var location = GetImageLocation(center, size);

                e.Graphics.FillRectangle(brush, location.X, location.Y, size.Width, size.Height);
            }
            foreach (var piece in from i in Pieces orderby i.ZOrder select i)
            {
                var image = s_PieceBitmaps[piece.Type];
                var size = image.Size;

                if (piece.Alive)
                {
                    if (piece.Animator.Playing)
                    {
                        var animation = piece.Animator.Sequences.Peek();
                        if (animation is Animation.Slide slide)
                        {
                            var center = slide.Location;
                            var location = GetImageLocation(center, size);

                            e.Graphics.DrawImage(image, location);
                        }
                        else if (animation is Animation.Fade fade)
                        {
                            var point = piece.Point;

                            var center = GetLocationFromPoint(point);
                            var location = GetImageLocation(center, size);
                            var matrix = new ColorMatrix { Matrix33 = fade.Opacity };

                            var attributes = new ImageAttributes();
                            attributes.SetColorMatrix(matrix);

                            e.Graphics.DrawImage(image, new Rectangle(location, size), 0, 0, image.Width, image.Height, GraphicsUnit.Pixel, attributes);
                        }
                    }
                    else
                    {
                        var point = piece.Point;

                        var center = GetLocationFromPoint(point);
                        var location = GetImageLocation(center, size);

                        if (Holding)
                        {
                            if (piece == CurrentTarget)
                            {
                                var matrix = new ColorMatrix { Matrix33 = 0.5f };

                                var attributes = new ImageAttributes();
                                attributes.SetColorMatrix(matrix);

                                e.Graphics.DrawImage(image, new Rectangle(location, size), 0, 0, image.Width, image.Height, GraphicsUnit.Pixel, attributes);
                            }
                            else
                                e.Graphics.DrawImage(image, location);
                        }
                        else
                            e.Graphics.DrawImage(image, location);
                    }
                }
            }
            foreach (var point in Spots)
            {
                var color = Color.FromArgb(128, Color.Green);
                using var brush = new SolidBrush(color);

                var size = new Size(kSpotDiameter, kSpotDiameter);
                var center = GetLocationFromPoint(point);
                var location = GetImageLocation(center, size);

                e.Graphics.FillEllipse(brush, location.X, location.Y, size.Width, size.Height);
            }

            if (Holding)
            {
                var image = s_PieceBitmaps[CurrentTarget.Type];

                var size = image.Size;
                var center = HoldingLocation;
                var location = GetImageLocation(center, size);

                e.Graphics.DrawImage(image, location);
            }

            for (int index = Arrows.Count - 1; index >= 0; index--)
            {
                var arrow = Arrows[index];
                var color = arrow.Color;
                var width = arrow.Width;

                var alpha = CurrentArrowIndex == index ? 192 : 128;

                color = Color.FromArgb(alpha, color);
            
                using var pen = new Pen(color, width);
                pen.EndCap = LineCap.ArrowAnchor;

                var from = GetLocationFromPoint(arrow.From);
                var to = GetLocationFromPoint(arrow.To);

                e.Graphics.DrawLine(pen, from.X, from.Y, to.X, to.Y);
            }

        }
        private bool IsLocateInsideCircle(Point location, Point center, int radius)
        {
            int dx = location.X - center.X;
            int dy = location.Y - center.Y;

            return dx * dx + dy * dy <= radius * radius;
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (Holding)
            {
                HoldingLocation = e.Location;

                View.Invalidate();

                Cursor.Current = Cursors.NoMove2D;
                foreach (var spot in Spots)
                {
                    var location = GetLocationFromPoint(spot);
                    if (IsLocateInsideCircle(e.Location, location, kSpotDiameter))
                    {
                        Cursor.Current = Cursors.Hand;
                        break;
                    }
                }
            }
            else if (CurrentTarget is null)
            {
                if (Hover)
                {
                    Hover = false;

                    View.Invalidate();
                }

                var point = GetPointFromLocation(e.Location);
                if (IsOutOfRange(point))
                    return;

                var piece = this[point];
                if (piece is not null)
                {
                    var location = GetLocationFromPoint(point);
                    if (IsLocateInsideCircle(e.Location, location, piece.Diameter / 2))
                    {
                        if (IsManipulable(piece.Type))
                        {
                            Hover = true;
                            HoverTarget = piece;

                            View.Invalidate();

                            Cursor.Current = Cursors.Hand;
                        }
                        else
                            Cursor.Current = Cursors.No;
                    }
                    else
                        Cursor.Current = Cursors.Default;
                }
                else
                    Cursor.Current = Cursors.Default;
            }
            else
            {
                var point = CurrentTarget.Point;
                var location = GetLocationFromPoint(point);
                if (IsLocateInsideCircle(e.Location, location, CurrentTarget.Diameter / 2))
                    Cursor.Current = Cursors.Hand;
                else
                {
                    foreach (var spot in Spots)
                    {
                        location = GetLocationFromPoint(spot);
                        if (IsLocateInsideCircle(e.Location, location, kSpotDiameter))
                        {
                            Cursor.Current = Cursors.Hand;
                            break;
                        }
                    }
                }
            }
        }
        private void OnMouseDown(object sender, MouseEventArgs e)
        {
            var point = GetPointFromLocation(e.Location);
            var piece = this[point];

            if (piece is not null)
            {
                var location = GetLocationFromPoint(point);
                if (IsLocateInsideCircle(e.Location, location, piece.Diameter / 2))
                {
                    // TODO: 위치 옮기기 기능
                    //if (Control.ModifierKeys.HasFlag(Keys.Alt))
                    //{
                    //}
                    if (piece == CurrentTarget)
                    {
                        CurrentTarget = null;
                        Spots.Clear();

                        View.Invalidate();
                    }
                    else if (IsManipulable(piece.Type))
                    {
                        Hover = false;
                        Holding = true;
                        HoldingLocation = e.Location;

                        CurrentTarget = piece;

                        var hits = piece.Raycast(this);

                        Spots.Clear();
                        if (hits.Any())
                            Spots.AddRange(from hit in hits select hit.Point);

                        View.Invalidate();
                    }
                }
            }
            foreach (var spot in Spots)
            {
                var location = GetLocationFromPoint(spot);
                if (IsLocateInsideCircle(e.Location, location, kSpotDiameter))
                {
                    var from = CurrentTarget.Point;
                    var to = point;

                    MoveTo(from, to, animate: true);
                    break;
                }
            }
        }
        private void OnMouseUp(object sender, MouseEventArgs e)
        {
            if (Holding)
            {
                Holding = false;
                var point = CurrentTarget.Point;
                var location = default(Point);

                foreach (var spot in Spots)
                {
                    location = GetLocationFromPoint(spot);
                    if (IsLocateInsideCircle(e.Location, location, kSpotDiameter ))
                        point = spot;
                }

                location = GetLocationFromPoint(CurrentTarget.Point);
                if (IsLocateInsideCircle(e.Location, location, CurrentTarget.Diameter / 2))
                    View.Invalidate();

                else if (point == CurrentTarget.Point)
                {
                    CurrentTarget = null;

                    Spots.Clear();
                    View.Invalidate();
                }

                else
                {
                    var from = CurrentTarget.Point;
                    var to = point;

                    MoveTo(from, to, animate: false);
                }
            }
        }

        public Piece this[Point point]
        {
            get => Map[point.X, point.Y];
            set => Map[point.X, point.Y] = value;
        }
        public void MoveTo(Point from, Point to, bool animate)
        {
            Turn = !Turn;
            Time += 1;

            // TODO: 폼 기능으로 옮기기
            Form.TimeBar.Maximum = Time;
            Form.TimeBar.Value = Time;
            
            if (animate)
                this[from].Slide(to);

            if (this[to] is Piece piece)
            {
                if (animate)
                    piece.FadeOut();
                Sounds.Capture.Play();
            }
            else
                Sounds.Move.Play();

            if (from != to)
            {
                this[to] = this[from];
                this[from] = null;

                this[to].Point = to;
            }

            CurrentTarget = null;

            Spots.Clear();

            Footprints.Clear();
            Footprints.Add(from);
            Footprints.Add(to);

            View.Invalidate();

            Form.Pause();
            Core.Reset();
            Core.Stop();
            Core.Wait(Core.EventStop, seconds: 10);

            Core.Position(GetFen());
            Core.Go();
            Form.Resume();
        }

        public void DrawArrows(IEnumerable<PVInfo> infos)
        {
            Arrows.Clear();

            int index = 1;
            foreach (var info in infos)
            {
                var raw2 = GetRaw2FromPV(info.pv);
                var (from, to) = GetPoint2FromRaw2(raw2);

                Arrows.Add(new Line()
                {
                    From = from,
                    To = to,
                    Width = kArrowWidth / index,
                    Color = s_ArrowColors[index == 1 ? 0 : 1],
                });

                index++;
            }
        }

        public static string GetRaw2FromPV(string pv)
        {
            var index = pv.IndexOf(' ');
            if (index > 0)
                pv = pv.Substring(0, index);

            return pv;
        }
        public void MoveByRaw2(string raw2)
        {
            var (from, to) = GetPoint2FromRaw2(raw2);
            MoveTo(from, to, animate: true);
        }

        public bool IsManipulable(char type) 
        {
            if (IsBlueTeam(type) == Turn)
                return true;
            return false;
        }
        public static bool IsEnemy(char type1, char type2)
        {
            if (char.IsUpper(type1) == char.IsUpper(type2))
                return false;
            return true;
        }

        public char GetType(Point point)
        {
            var piece = this[point];
            if (piece is null)
                return default;
            return piece.Type;
        }
        public bool IsEmpty(Point point)
        {
            var piece = this[point];
            if (piece is null)
                return true;
            return false;
        }
        public bool IsOutOfRange(Point point)
        {
            if (point.X < 0 || point.Y < 0 || point.X >= kWidth || point.Y >= kHeight)
                return true;
            return false;
        }
        public bool IsOutOfPalace(Point point)
        {
            if (point.X < 3 || point.X >= 6)
                return true;
            if (point.Y >= 3 && point.Y <= 6)
                return true;

            return false;
        }

        public static Point GetLocationFromPoint(Point point)
        {
            int x = point.X * kBoardSpace + kBoardMargin;
            int y = point.Y * kBoardSpace + kBoardMargin;

            return new(x, y);
        }
        public Point GetPointFromLocation(Point location)
        {
            int x = (location.X - kBoardMargin + kBoardSpace / 2) / kBoardSpace;
            int y = (location.Y - kBoardMargin + kBoardSpace / 2) / kBoardSpace;

            return new(x, y);
        }
        public PointF GetPointFromLocation(PointF location)
        {
            float x = (location.X - kBoardMargin + kBoardSpace / 2) / kBoardSpace;
            float y = (location.Y - kBoardMargin + kBoardSpace / 2) / kBoardSpace;

            return new(x, y);
        }
        public static Point GetImageLocation(Point location, Size size)
        {
            return new(location.X - size.Width / 2, location.Y - size.Width / 2);
        }
        public static PointF GetImageLocation(PointF location, Size size)
        {
            return new(location.X - size.Width / 2, location.Y - size.Width / 2);
        }
        public static Point GetPointFromRaw(string raw)
        {
            int y = 0;
            int x = raw[0] - 'a';
            for (int i = 1; i < raw.Length; i++)
            {
                y *= 10;
                y += raw[i] - '0';
            }

            return new(x, kHeight - y);
        }
        public static (Point From, Point To) GetPoint2FromRaw2(string raw2)
        {
            int index;
            for (index = raw2.Length - 1; index >= 0; index--)
                if (char.IsLetter(raw2[index]))
                    break;

            var a = raw2.Substring(0, index);
            var b = raw2.Substring(index);

            var from = GetPointFromRaw(a);
            var to = GetPointFromRaw(b);

            return (from, to);
        }
        static readonly StringBuilder s_StringBuilder = new();

        public string GetFen()
        {
            s_StringBuilder.Clear();

            int indent = 0;
            for (int i = 0; i < kHeight; i++)
            {
                for (int j = 0; j < kWidth; j++)
                {
                    var target = this[new(j, i)];
                    if (target is not null)
                    {
                        if (indent > 0)
                        {
                            s_StringBuilder.Append(indent);
                            indent = 0;
                        }
                        s_StringBuilder.Append(target.Type);
                    }
                    else
                        indent++;
                }
                if (indent > 0)
                {
                    s_StringBuilder.Append(indent);
                    indent = 0;
                }
                if (i < kHeight - 1)
                    s_StringBuilder.Append('/');
            }

            var turn = (Turn ? 'w' : 'b');
            s_StringBuilder.Append($" {turn} - - {Time} {Retry}");

            return s_StringBuilder.ToString();
        }
        internal static bool IsBlueTeam(char type)
        {
            if (char.IsUpper(type))
                return true;
            return false;
        }
        internal static bool IsSameTeam(char type1, char type2)
        {
            if (char.IsUpper(type1) == char.IsUpper(type2))
                return true;
            return false;
        }
        internal static bool IsSameType(char type1, char type2)
        {
            if (char.ToUpper(type1) == char.ToUpper(type2))
                return true;
            return false;
        }
        internal static bool IsDifferentTeam(char type1, char type2) => !IsSameTeam(type1, type2);
        internal static bool IsDifferentType(char type1, char type2) => !IsSameType(type1, type2);
    }
}