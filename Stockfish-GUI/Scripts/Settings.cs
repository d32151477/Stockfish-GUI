using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.Remoting.Contexts;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;

namespace Stockfish_GUI
{
    internal class Settings
    {
        public int MultiPV = 5;

        public string Protocol = "uci";
        public int Threads = 4;
        public int Hash = 1024;
        public bool Ponder = false;

        [JsonProperty("Skill Level")]
        public int Skill_Level = 20;
        [JsonProperty("Move Overhead")]
        public int Move_Overhead = 30;
        [JsonProperty("Slow Mover")]
        public int Slow_Mover = 84;

        public int nodestime = 0;
        public int UCI_Elo = 1350;
        public bool UCI_AnalyseMode = true;
        public string UCI_Variant = "janggimodern";
        public bool UCI_Chess960 = false;
        public bool UCI_LimitStrength = false;
        public bool UCI_ShowWDL = false;

        public string SyzygyPath = "<empty>";
        public int SyzygyProbeDepth = 1;
        public bool Syzygy50MoveRule = true;
        public int SyzygyProbeLimit = 7;

        [JsonProperty("Use NNUE")]
        public bool Use_NNUE = true;
        public string EvalFile = "janggi-85de3dae670a.nnue";
        public bool TsumeMode = false;
        public string VariantPath = "<empty>";
    }
}
