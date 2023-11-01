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

        public const string kFen = "rbna1abnr/4k4/1c5c1/p1p1p1p1p/9/9/P1P1P1P1P/1C5C1/4K4/RNBA1ANBR w - - 0 1";

        private const int kArrowWidth = 16;
        private static readonly Color[] s_ArrowColors = new[] { Color.Orange, Color.DarkSlateBlue, };

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
        public static readonly Dictionary<char, Bitmap> PieceBitmapsByType = new()
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
        public static readonly Dictionary<string, char> PieceTypesByName = new()
        {
            ["blue_chariot"] = 'R',
            ["blue_elephant"] = 'B',
            ["blue_horse"] = 'N',
            ["blue_advisor"] = 'A',
            ["blue_king"] = 'K',
            ["blue_cannon"] = 'C',
            ["blue_pawn"] = 'P',

            ["red_chariot"] = 'r',
            ["red_elephant"] = 'b',
            ["red_horse"] = 'n',
            ["red_advisor"] = 'a',
            ["red_king"] = 'k',
            ["red_cannon"] = 'c',
            ["red_pawn"] = 'p',
        };

        private struct Line
        {
            public Color Color;
            public Point From;
            public Point To;
            public int Width;
        }
        public struct Movement
        {
            public int Time;
            public Piece Piece;
            public Piece Captured;
            public Point From;
            public Point To;

            public List<Point> Footprints;
        }

        public Piece this[Point point]
        {
            get => Map[point.X, point.Y];
            set => Map[point.X, point.Y] = value;
        }

        public bool Analyzing;
        public bool Swapped;
        public bool Turn;

        public int Time = 1;

        private bool Modifing;
        private int Retry = 0;

        private bool Hover;
        private Piece HoverTarget;

        private bool Holding;
        private Point HoldingLocation;

        private Piece CurrentTarget;
        public int CurrentArrowIndex;

        public int UndoIndex = 0;
        public List<Movement> Movements = new();

        private readonly Piece[,] Map = new Piece[kWidth, kHeight];
        private readonly Control View;
        private readonly Form Form;

        public readonly List<Piece> Pieces = new();
        public bool Running = false;

        private readonly List<Line> Arrows = new();
        private readonly List<Point> Spots = new();
        private readonly List<Point> Footprints = new();

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

        public void Swap()
        {
            Swapped = !Swapped;
            View.Invalidate();
        }
        public void Create() => Create(kFen);
        public void Create(string fen)
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

            View.Invalidate();
        }
        private void OnPaint(object sender, PaintEventArgs e)
        {
            e.Graphics.DrawImage(Resources.board, Point.Empty);

            foreach (var point in Footprints)
            {
                var color = Turn ? Color.Red : Color.Blue;
                if (Swapped)
                    color = Turn ? Color.Blue : Color.Red;

                color = Color.FromArgb(128, color);

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

                var type = piece.Type;
                if (Swapped)
                    type = char.IsUpper(type) ? char.ToLower(type) : char.ToUpper(type);

                var color = IsBlueTeam(type) ? Color.Blue : Color.Red;
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
                var type = piece.Type;
                if (Swapped)
                    type = char.IsUpper(type) ? char.ToLower(type) : char.ToUpper(type);

                var image = PieceBitmapsByType[type];
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

            if (Holding)
            {
                var type = CurrentTarget.Type;
                if (Swapped)
                    type = char.IsUpper(type) ? char.ToLower(type) : char.ToUpper(type);

                var image = PieceBitmapsByType[type];

                var size = image.Size;
                var center = HoldingLocation;
                var location = GetImageLocation(center, size);

                e.Graphics.DrawImage(image, location);
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
                        else if (Control.ModifierKeys.HasFlag(Keys.Alt))
                            Cursor.Current = Cursors.Hand;
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
                    if (Control.ModifierKeys.HasFlag(Keys.Alt))
                    {
                        Hover = false;
                        Holding = true;
                        HoldingLocation = e.Location;

                        Modifing = true;
                        CurrentTarget = piece;

                        View.Invalidate();
                    }
                    else if (piece == CurrentTarget)
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
            if (Modifing)
            {
                Holding = false;
                Modifing = false;

                var point = GetPointFromLocation(e.Location);
                if (point == CurrentTarget.Point)
                {
                    CurrentTarget = null;

                    Spots.Clear();
                    View.Invalidate();
                }
                else
                {
                    var piece = this[point];

                    var from = CurrentTarget.Point;
                    var to = point;

                    this[from] = piece;
                    this[to] = CurrentTarget;

                    CurrentTarget.Point = to;
                    if (piece is not null)
                        piece.Point = from;
                    View.Invalidate();

                    if (Running)
                    {
                        Pause();
                        Start();
                    }
                }
            }
            else if (Holding)
            {
                Holding = false;
                var point = CurrentTarget.Point;
                var location = default(Point);

                foreach (var spot in Spots)
                {
                    location = GetLocationFromPoint(spot);
                    if (IsLocateInsideCircle(e.Location, location, kSpotDiameter))
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

        public void MoveTo(Point from, Point to, bool animate)
        {
            var captured = this[to] is Piece piece ? piece : null;
            var time = captured is null ? Time + 1 : 1;

            var movement = new Movement()
            {
                Time = time,
                From = from,
                To = to,
                Piece = this[from],
                Captured = this[to],
                Footprints = new(Footprints),
            };

            int length = Movements.Count - UndoIndex;
            int index = Movements.Count - length;

            Movements.RemoveRange(index, length);
            Movements.Add(movement);

            Move(movement, animate);
            if (!animate)
                View.Invalidate();
        }

        public void Move(Movement movement, bool animate)
        {
            var from = movement.From;
            var to = movement.To;
            if (from != to)
            {
                if (animate)
                    movement.Piece.Slide(movement.To);

                if (movement.Captured is not null)
                {
                    if (animate)
                        movement.Captured.FadeOut();
                    else
                        movement.Captured.Alive = false;
                    Sounds.Capture.Play();
                }
                else
                {
                    Sounds.Move.Play();
                }

                this[to] = movement.Piece;
                this[from] = null;

                movement.Piece.Point = to;
            }

            Time = movement.Time;
            UndoIndex += 1;

            Form.TimeBar.Maximum = Movements.Count;
            Form.TimeBar.Value = UndoIndex;

            Switch();

            Footprints.Add(from);
            Footprints.Add(to);
        }

        public void Undo(Movement movement, bool animate)
        {
            var from = movement.From;
            var to = movement.To;

            if (from != to)
            {
                if (animate)
                    movement.Piece.Slide(movement.From);

                if (movement.Captured is not null)
                {
                    if (animate)
                        movement.Captured.FadeIn();
                    movement.Captured.Alive = true;

                    Sounds.Capture.Play();
                }
                else
                    Sounds.Move.Play();

                this[to] = movement.Captured;
                this[from] = movement.Piece;

                movement.Piece.Point = from;
            }

            Time = movement.Time;
            UndoIndex -= 1;

            Switch();

            Footprints.AddRange(movement.Footprints);
        }
        public bool Redo(bool animate = true)
        {
            if (UndoIndex > Movements.Count - 1)
                return false;

            Move(Movements[UndoIndex], animate);
            return true;
        }

        public bool Undo(bool animate = true)
        {
            if (UndoIndex <= 0)
                return false;

            Undo(Movements[UndoIndex - 1], animate);
            return true;    
        }
        public void Switch()
        {
            Turn = !Turn;
            Pause();
            Start();
        }
        public void Pause()
        {
            Spots.Clear();
            Arrows.Clear();
            Footprints.Clear();

            Hover = false;
            Holding = false;
            CurrentTarget = null;

            if (Running)
            {
                Running = false;

                Core.Stop();
                Core.Wait(Core.EventStop, seconds: 5);
                Core.Reset();
                Form.Pause();
            }
        }

        public void Start()
        {
            if (Form.Instance.AnalyzeMenuItem.Checked)
            {
                if (Running)
                    return;

                Core.Position(GetFen());
                if (Core.CurrentSetting.Search_Depth > 0)
                    Core.GoDepth(Core.CurrentSetting.Search_Depth);
                else
                    Core.Go();

                Form.Resume();
                Running = true;
            }
        }
        public void Reset()
        {
            Hover = false;
            Holding = false;
            CurrentTarget = null;
            Footprints.Clear();
            Pieces.Clear();
            for (int x = 0; x < kWidth; x++)
                for (int y = 0; y < kHeight; y++)
                    Map[x, y] = null;

            Time = 1;
            Turn = true;

            UndoIndex = 0;
            Movements.Clear();

            Form.TimeBar.Enabled = false;
            Form.TimeBar.Maximum = 0;
            Form.TimeBar.Value = 0;

            View.Invalidate();
        }

        // PV를 화살표로 그려주기 위해 사용됩니다.
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

        // 현재 장군 상태인지 확인합니다.
        public bool IsChecked()
        {
            var type = (Turn ? 'K' : 'k');
           
            var enumerable = from i in Pieces where i.Type == type select i;
            var king = enumerable.First();

            enumerable = from i in Pieces where IsEnemy(king.Type, i.Type) select i;
            foreach (var piece in enumerable)
            {
                foreach (var hit in piece.Raycast(this))
                    if (hit.Point == king.Point)
                        return true;
            }
            return false;
        }

        // PV 에서 첫번째 좌표 문자열을 가져옵니다.
        public static string GetRaw2FromPV(string pv)
        {
            var index = pv.IndexOf(' ');
            if (index > 0)
                pv = pv.Substring(0, index);

            return pv;
        }

        // 좌표 문자열들을 포인트로 변환하고 이동합니다.
        public void MoveByRaw2(string raw2)
        {
            var (from, to) = GetPoint2FromRaw2(raw2);
            MoveTo(from, to, animate: true);
        }

        // 현재 움직일 수 있는 지 확인합니다.
        public bool IsManipulable(char type) 
        {
            if (IsBlueTeam(type) == Turn)
                return true;
            return false;
        }

        // 서로 다른 팀 말인지 확인합니다. (소문자, 대문자로 구분됩니다.)
        public static bool IsEnemy(char type1, char type2)
        {
            if (char.IsUpper(type1) == char.IsUpper(type2))
                return false;
            return true;
        }

        // 해당 칸이 비어있는지 확인합니다.
        public bool IsEmpty(Point point)
        {
            var piece = this[point];
            if (piece is null)
                return true;
            return false;
        }

        // 해당 좌표가 보드를 벗어났는지 확인합니다.
        public bool IsOutOfRange(Point point)
        {
            if (point.X < 0 || point.Y < 0 || point.X >= kWidth || point.Y >= kHeight)
                return true;
            return false;
        }

        // 해당 좌표가 궁성 안인지 확인합니다.
        public bool IsOutOfPalace(Point point)
        {
            if (point.X < 3 || point.X >= 6)
                return true;
            if (point.Y >= 3 && point.Y <= 6)
                return true;

            return false;
        }

        // 해당 좌표를 보드 이미지 좌표로 변환합니다.
        public static Point GetLocationFromPoint(Point point)
        {
            int x = point.X * kBoardSpace + kBoardMargin;
            int y = point.Y * kBoardSpace + kBoardMargin;

            return new(x, y);
        }

        // 보드 이미지 좌표를 인덱스 좌표로 변환합니다.
        public Point GetPointFromLocation(Point location)
        {
            int x = (location.X - kBoardMargin + kBoardSpace / 2) / kBoardSpace;
            int y = (location.Y - kBoardMargin + kBoardSpace / 2) / kBoardSpace;

            return new(x, y);
        }

        // 해당 이미지를 중심에 위치하도록 변환합니다.
        public static Point GetImageLocation(Point location, Size size)
        {
            return new(location.X - size.Width / 2, location.Y - size.Width / 2);
        }
        public static PointF GetImageLocation(PointF location, Size size)
        {
            return new(location.X - size.Width / 2, location.Y - size.Width / 2);
        }

        // 대수기보표기법으로 작성된 좌표를 인덱스 좌표로 변환합니다.
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

        // 스톡피쉬의 Fen 형식으로 변환합니다.
        public static string GetFen(char[,] map, bool turn, int time, int retry)
        {
            s_StringBuilder.Clear();

            int indent = 0;
            for (int y = 0; y < kHeight; y++)
            {
                for (int x = 0; x < kWidth; x++)
                {
                    var type = map[x, y];
                    if (type > 0)
                    {
                        if (indent > 0)
                        {
                            s_StringBuilder.Append(indent);
                            indent = 0;
                        }
                        s_StringBuilder.Append(type);
                    }
                    else
                        indent++;
                }
                if (indent > 0)
                {
                    s_StringBuilder.Append(indent);
                    indent = 0;
                }
                if (y < kHeight - 1)
                    s_StringBuilder.Append('/');
            }
            s_StringBuilder.Append($" {(turn ? 'w' : 'b')} - - {time} {retry}");

            return s_StringBuilder.ToString();
        }
        public string GetFen()
        {
            var map = new char[kWidth, kHeight];
            for (int y = 0; y < kHeight; y++)
                for (int x = 0; x < kWidth; x++)
                    map[x, y] = this[new Point(x, y)]?.Type ?? default;

            return GetFen(map, Turn, Time, Retry);
        }

        // 초 진형인지 확인합니다. (색상 변경을 하더라도 초 진형은 무조건 아래팀입니다.)
        public static bool IsBlueTeam(char type)
        {
            if (char.IsUpper(type))
                return true;
            return false;
        }

        // 같은 팀인지 확인합니다.
        public static bool IsSameTeam(char type1, char type2)
        {
            if (char.IsUpper(type1) == char.IsUpper(type2))
                return true;
            return false;
        }

        // 같은 유형의 말인지 확인합니다. (포가 포를 확인할 때 사용됩니다.)
        public static bool IsSameType(char type1, char type2)
        {
            if (char.ToUpper(type1) == char.ToUpper(type2))
                return true;
            return false;
        }
        public static bool IsDifferentTeam(char type1, char type2) => !IsSameTeam(type1, type2);
        public static bool IsDifferentType(char type1, char type2) => !IsSameType(type1, type2);

        // 해당 좌표가 원 안에 위치하는지 계산합니다.
        private static bool IsLocateInsideCircle(Point location, Point center, int radius)
        {
            int dx = location.X - center.X;
            int dy = location.Y - center.Y;

            return dx * dx + dy * dy <= radius * radius;
        }
    }
}
