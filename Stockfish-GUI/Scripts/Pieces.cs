using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace Stockfish_GUI
{
    public abstract class Piece
    {
        public bool Alive = true;
        public Point Point;
        public char Type;

        public int ZOrder = 0;
        public Animator Animator = new();

        private static int s_ZOrderIndex = 0;

        public Piece() 
        {
            Animator.OnEnded += OnAnimationEnded;
        }

        public IEnumerable<Point> GetNormals(Board board)
        {
            foreach (var normal in Ray.Normals)
                yield return normal;

            if (board.IsOutOfPalace(Point))
                yield break;

            foreach (var normal in Ray.Diagonals)
            {
                var ray = new Ray(Point, normal);
                var hit = ray.Pass(board);

                if (hit.InPalace)
                    yield return normal;
            }
        }

        private void OnAnimationEnded(Animation animation)
        {
            if (animation is Animation.Fade fade)
                Alive = fade.Opacity == 1.0f;
        }

        public void Slide(Point point)
        {
            var from = Board.GetLocationFromPoint(Point);
            var to = Board.GetLocationFromPoint(point);

            var animation = new Animation.Slide(from, to, duration: 250);
            Animator.Sequences.Enqueue(animation);

            ZOrder = ++s_ZOrderIndex;
        }
        public void FadeOut()
        {
            var animation = new Animation.Fade(1.0f, 0.0f, duration: 250);
            Animator.Sequences.Enqueue(animation);
        }
        public void FadeIn()
        {
            var animation = new Animation.Fade(0.0f, 1.0f, duration: 250);
            Animator.Sequences.Enqueue(animation);
        }

        public virtual int Diameter => 32;
        public abstract IEnumerable<Ray.Hit> Raycast(Board board);
    }
    public class Pawn : Piece
    {
        public override IEnumerable<Ray.Hit> Raycast(Board board)
        {
            int exceptedY = Board.IsBlueTeam(Type) ? 1 : -1;

            foreach (var normal in GetNormals(board))
            {
                if (normal.Y == exceptedY)
                    continue;

                var ray = new Ray(Point, normal);
                var hit = ray.Pass(board);

                if (hit.OutOfRange)
                    continue;

                if (hit.Empty)
                    yield return hit;

                else if (Board.IsDifferentTeam(Type, hit.Type))
                    yield return hit;
            }
        }
        protected bool IsDiagonal(Point normal)
        {
            if (Math.Abs(normal.X) == Math.Abs(normal.Y))
                return true;
            return false;
        }
    }
    // TODO: 궁에서는 모두가 대각으로 움직일 수 있게 수정해야합니다.

    public class Advisor : Piece
    {
        public static readonly Point[] Diagonals = { new(1, 1), new(-1, -1), new(-1, 1), new(1, -1) };

        public override IEnumerable<Ray.Hit> Raycast(Board board)
        {
            foreach (var normal in GetNormals(board))
            {
                var ray = new Ray(Point, normal);
                var hit = ray.Pass(board);

                if (hit.OutOfRange)
                    continue;

                if (hit.OutOfPalace)
                    continue;

                if (hit.Empty)
                    yield return hit;

                else if (Board.IsDifferentTeam(Type, hit.Type))
                    yield return hit;
            }
        }
    }
    public class King : Advisor
    {
        public override int Diameter => 64;
    }

    public class Chariot : Piece
    {
        public override int Diameter => 48;

        public override IEnumerable<Ray.Hit> Raycast(Board board)
        {
            foreach (var normal in GetNormals(board))
            {
                var ray = new Ray(Point, normal);
                while (true)
                {
                    var hit = ray.Pass(board);
                    if (hit.OutOfRange)
                        break;

                    if (hit.Empty)
                        yield return hit;

                    else
                    {
                        if (Board.IsDifferentTeam(Type, hit.Type))
                            yield return hit;
                        break;
                    }
                }
            }
        }
    }
    public class Horse : Piece
    {
        public override int Diameter => 48;

        public override IEnumerable<Ray.Hit> Raycast(Board board)
        {
            foreach (var normal in GetNormals(board))
            {
                var ray = new Ray(Point, normal);
                var hit = ray.Pass(board);

                if (hit.OutOfRange)
                    continue;

                if (hit.Collided)
                    continue;

                var waypoint = hit.Point;
                var diagonals = GetDiagonalNormals(normal);

                foreach (var diagonal in diagonals)
                {
                    ray = new Ray(waypoint, diagonal);
                    hit = ray.Pass(board);

                    if (hit.OutOfRange)
                        continue;

                    if (hit.Empty)
                        yield return hit;

                    if (hit.Collided)
                        if (Board.IsDifferentTeam(Type, hit.Type))
                            yield return hit;
                }
            }
        }
        protected IEnumerable<Point> GetDiagonalNormals(Point normal)
        {
            if (Math.Abs(normal.X) == Math.Abs(normal.Y))
            {
                yield return new Point(normal.X, 0);
                yield return new Point(0, normal.Y);
            }
            else
            {
                var ax = Math.Abs(normal.X);
                var ay = Math.Abs(normal.Y);

                yield return new Point(normal.X + (1 - ax), normal.Y + (1 - ay));
                yield return new Point(normal.X + (1 - ax) * -1, normal.Y + (1 - ay) * -1);
            }
        }
    }
    public class Elephant : Horse
    {
        public override int Diameter => 48;

        public override IEnumerable<Ray.Hit> Raycast(Board board)
        {
            foreach (var normal in GetNormals(board))
            {
                var ray = new Ray(Point, normal);
                var hit = ray.Pass(board);

                if (hit.OutOfRange)
                    continue;

                if (hit.Collided)
                    continue;

                var waypoint = hit.Point;
                var diagonals = GetDiagonalNormals(normal);

                foreach (var diagonal in diagonals)
                {
                    ray = new Ray(waypoint, diagonal);
                    hit = ray.Pass(board);

                    if (hit.OutOfRange)
                        continue;

                    if (hit.Collided)
                        continue;

                    hit = ray.Pass(board);

                    if (hit.OutOfRange)
                        continue;

                    if (hit.Empty)
                        yield return hit;

                    if (hit.Collided)
                        if (Board.IsDifferentTeam(Type, hit.Type))
                            yield return hit;
                }
            }
        }
    }
    public class Cannon : Piece
    {
        public override int Diameter => 48;

        public override IEnumerable<Ray.Hit> Raycast(Board board)
        {
            foreach (var normal in GetNormals(board))
            {
                var hit = default(Ray.Hit);
                var ray = new Ray(Point, normal);
                while (true)
                {
                    hit = ray.Pass(board);

                    if (hit.OutOfRange)
                        break;

                    if (hit.Collided)
                        break;
                }
                if (hit.OutOfRange)
                    continue;

                if (Board.IsSameType(Type, hit.Type))
                    continue;

                while (true)
                {
                    hit = ray.Pass(board);
                    if (hit.OutOfRange)
                        break;

                    if (hit.Empty)
                        yield return hit;

                    else if (hit.Collided)
                    {
                        if (Board.IsDifferentType(Type, hit.Type))
                            if (Board.IsDifferentTeam(Type, hit.Type))
                                yield return hit;
                        break;
                    }
                }
            }
        }
    }
    public class Ray
    {
        public static readonly Point[] Normals = { new(0, 1), new(0, -1), new(-1, 0), new(1, 0) };
        public static readonly Point[] Diagonals = { new(1, 1), new(-1, -1), new(-1, 1), new(1, -1) };

        public Point Point;
        public Point Normal;

        public Ray(Point point, Point normal)
        {
            Point = point;
            Normal = normal;
        }
        public Hit Pass(Board board)
        {
            Point.Offset(Normal);

            var outOfRange = board.IsOutOfRange(Point);
            return new Hit()
            {
                Point = Point,
                OutOfRange = outOfRange,
                Type = outOfRange ? default : board.GetType(Point),
                Empty = outOfRange ? default : board.IsEmpty(Point),
                OutOfPalace = outOfRange ? true : board.IsOutOfPalace(Point),
            };
        }
        public struct Hit
        {
            public char Type;

            public Point Point;
            public bool Empty;
            public bool OutOfPalace;

            public bool OutOfRange;

            public bool Collided => Type > 0;
            public bool InPalace => !OutOfPalace;
        }
    }
}
