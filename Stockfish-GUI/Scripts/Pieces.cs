using System;
using System.Collections.Generic;
using System.Drawing;
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

        private void OnAnimationEnded(Animation animation)
        {
            if (animation is Animation.Fade)
                Alive = false;
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

        public virtual int Diameter => 32;
        public abstract IEnumerable<Ray.Hit> Raycast(Board board);
    }
    public class Pawn : Piece
    {
        public static readonly Point[] s_ExceptedNormals = new Point[] { new(0, 1), new(0, -1) };
        public override IEnumerable<Ray.Hit> Raycast(Board board)
        {
            int index = Board.IsBlueTeam(Type) ? 0 : 1;

            foreach (var normal in Ray.Normals)
            {
                if (normal == s_ExceptedNormals[index])
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
    }
    public class Advisor : Piece
    {
        public override IEnumerable<Ray.Hit> Raycast(Board board)
        {
            foreach (var normal in Ray.Normals)
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
            foreach (var normal in Ray.Normals)
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
            foreach (var normal in Ray.Normals)
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
            var ax = Math.Abs(normal.X);
            var ay = Math.Abs(normal.Y);

            yield return new Point(normal.X + (1 - ax), normal.Y + (1 - ay));
            yield return new Point(normal.X + (1 - ax) * -1, normal.Y + (1 - ay) * -1);
        }
    }
    public class Elephant : Horse
    {
        public override int Diameter => 48;

        public override IEnumerable<Ray.Hit> Raycast(Board board)
        {
            foreach (var normal in Ray.Normals)
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
            foreach (var normal in Ray.Normals)
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
        }
    }
}
