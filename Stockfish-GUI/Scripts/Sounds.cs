using Stockfish_GUI.Properties;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Media;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Stockfish_GUI
{
    internal static class Sounds
    {
        public static SoundPlayer Move;
        public static SoundPlayer Capture;

        static Sounds()
        {
            Move = new SoundPlayer(Resources.move);
            Capture = new SoundPlayer(Resources.capture);
        }
    }
}
