using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace Stockfish_GUI
{
    // 폼 보드에 있는 말의 움직임을 애니메이팅합니다.
    public class Animator
    {
        private static Timer s_Timer;
        private static Control s_Control;
        private static readonly List<Animator> s_Instances = new();

        public bool Playing => Sequences.Count > 0;
        public readonly Queue<Animation> Sequences = new();

        public event EventHandler OnEnded;
        public delegate void EventHandler(Animation animation);

        public Animator()
        {
            s_Instances.Add(this);
        }
        public static void Start(Control control)
        {
            s_Control = control;
            s_Timer = new();
            s_Timer.Interval = 16;
            s_Timer.Enabled = true;
            s_Timer.Tick += Tick;
        }
        private static void Tick(object sender, EventArgs e)
        {
            bool invalidated = false;
            foreach (var animator in s_Instances)
            {
                if (animator.Sequences.Count > 0)
                {
                    var animation = animator.Sequences.Peek();
                    if (animation.Play(s_Timer.Interval))
                    {
                        invalidated = true;
                        continue;
                    }
                    animator.OnEnded?.Invoke(animator.Sequences.Dequeue());
                }
            }
            if (invalidated)
                s_Control.Invalidate();
        }
    }
    public abstract partial class Animation
    {
        public float Duration;
        private float Elapsed;

        public abstract bool Play(int interval);

        protected static float EaseOut(float t) => 1 - (1 - t) * (1 - t);
        protected static float Lerp(float from, float to, float t) => from + (to - from) * t;

    }
    public partial class Animation
    {
        public class Slide : Animation
        {
            public PointF Location;
            public PointF From;
            public PointF To;

            public Slide(Point from, Point to, int duration)
            {
                Location = from;
                From = from;
                To = to;
                Duration = duration;
            }
            public override bool Play(int interval)
            {
                Elapsed += interval;
                if (Elapsed > Duration)
                    Elapsed = Duration;

                float t = EaseOut(Elapsed / Duration);
                float x = Lerp(From.X, To.X, t);
                float y = Lerp(From.Y, To.Y, t);
                Location = new(x, y);

                if (Elapsed == Duration)
                    return false;
                return true;
            }

        }
        public class Fade : Animation
        {
            public float Opacity;
            public float From;
            public float To;

            public Fade(float from, float to, float duration)
            {
                From = from;
                To = to;
                Opacity = from;
                Duration = duration;
            }

            public override bool Play(int interval)
            {
                Elapsed += interval;
                if (Elapsed > Duration)
                    Elapsed = Duration;

                float t = EaseOut(Elapsed / Duration);
                float o = Lerp(From, To, t);
                Opacity = o;

                if (Elapsed == Duration)
                    return false;
                return true;
            }
        }
    }
}
