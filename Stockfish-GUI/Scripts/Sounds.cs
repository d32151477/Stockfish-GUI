using Stockfish_GUI.Properties;
using System.Media;

namespace Stockfish_GUI
{
    public static class Sounds
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
