using Newtonsoft.Json;
using Stockfish_GUI.Properties;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace Stockfish_GUI
{
    public struct PVInfo
    {
        public int depth;
        public int seldepth;
        public int multipv;
        public float cp;
        public int mate;
        public int tbhits;
        public int nodes;
        public int nps;
        public int hashfull;
        public int time;
        public string pv;
    }

    public static class Core
    {
        private const string kEnginePath = "./Engines/fairy-stockfish.exe";

        public static bool IsPVDirty;
        public static PVInfo[] PVInfos;
        public static PVInfo Latest;
        public static readonly StringBuilder Logs = new();

        public static Form Form;
        public static Process Process;
        public static IReadOnlyList<Setting> Settings;
        public static Setting CurrentSetting;

        public static AutoResetEvent EventStop = new(false);
        public static AutoResetEvent EventReady = new(false);
        public static AutoResetEvent EventUCI = new(false);

        public static void Reset()
        {
            lock(PVInfos)
                for (int i = 0; i < PVInfos.Length; i++)
                    PVInfos[i] = default;
        }
        public static void Start()
        {
            if (!File.Exists(kEnginePath))
                throw new Exception($"해당 위치({kEnginePath})에서 엔진 파일을 찾을 수 없습니다.");

            var processInfo = new ProcessStartInfo
            {
                FileName = kEnginePath,
                
                RedirectStandardInput = true,
                RedirectStandardOutput = true,

                UseShellExecute = false,
                CreateNoWindow = true
            };

            Form = Form.Instance;
            Process = new Process() { StartInfo = processInfo };
            Process.Start();
            Process.OutputDataReceived += OnDataReceived;
            Process.BeginOutputReadLine();

            Settings = Setting.Load();
        }
        public static void SetOptions(Setting setting)
        {
            CurrentSetting = setting;
            if (PVInfos is not null)
                lock (PVInfos)
                    PVInfos = new PVInfo[setting.MultiPV];
            else
                PVInfos = new PVInfo[setting.MultiPV];

            var fields = typeof(Setting).GetFields(BindingFlags.Public | BindingFlags.Instance);
            foreach (var field in fields)
            {
                var attribute = field.GetCustomAttribute<JsonPropertyAttribute>();
                
                var name = attribute?.PropertyName ?? field.Name;
                var value = field.GetValue(setting).ToString().ToLower();

                SetOption(name, value);
            }
        }
        private static void OnDataReceived(object sender, DataReceivedEventArgs e)
        {
            var log = e.Data;
            if (log.StartsWith("info"))
            {
                var info = new PVInfo();
                var detail = log.Substring(5);

                var seldepth = Regex.Match(detail, @"seldepth\s+(\d+)");
                if (seldepth.Success)
                {
                    info.seldepth = int.Parse(seldepth.Groups[1].Value);

                    var depth = Regex.Match(detail, @"depth\s+(\d+)");
                    if (depth.Success)
                    {
                        info.depth = int.Parse(depth.Groups[1].Value);
                        detail = detail.Substring(depth.Index);
                    }

                    var multipv = Regex.Match(detail, @"multipv\s+(\d+)");
                    if (multipv.Success)
                    {
                        info.multipv = int.Parse(multipv.Groups[1].Value);
                        detail = detail.Substring(multipv.Index);
                    }

                    var cp = Regex.Match(detail, @"cp\s+(-?\d+)");
                    if (cp.Success)
                    {
                        info.cp = int.Parse(cp.Groups[1].Value) / 100.0f;
                        detail = detail.Substring(cp.Index);
                    }

                    var mate = Regex.Match(detail, @"mate\s+(-?\d+)");
                    if (mate.Success)
                    {
                        info.mate = int.Parse(mate.Groups[1].Value);
                        detail = detail.Substring(mate.Index);

                        int sign = Math.Sign(info.mate);
                        if (sign != 0)
                            info.cp = int.MaxValue * sign - info.mate;
                        else
                        {
                            Stop();
                            return;
                        }
                    }
                    var nodes = Regex.Match(detail, @"nodes\s+(\d+)");
                    if (nodes.Success)
                    {
                        info.nodes = int.Parse(nodes.Groups[1].Value);
                        detail = detail.Substring(nodes.Index);
                    }

                    var nps = Regex.Match(detail, @"nps\s+(\d+)");
                    if (nps.Success)
                    {
                        info.nps = int.Parse(nps.Groups[1].Value);
                        detail = detail.Substring(nps.Index);
                    }
                    var tbhits = Regex.Match(detail, @"tbhits\s+(\d+)");
                    if (tbhits.Success)
                    {
                        info.tbhits = int.Parse(tbhits.Groups[1].Value);
                        detail = detail.Substring(tbhits.Index);
                    }
                    var time = Regex.Match(detail, @"time\s+(\d+)");
                    if (time.Success)
                    {
                        info.time = int.Parse(time.Groups[1].Value);
                        detail = detail.Substring(time.Index);
                    }

                    var pv = Regex.Match(detail, @"\spv\s(.+)$");
                    if (pv.Success)
                        info.pv = pv.Groups[1].Value;

                    lock (PVInfos)
                    {
                        PVInfos[info.multipv - 1] = info;
                        Latest = info;
                        IsPVDirty = true;
                    }
                }
            }
            else if (log == "uciok")
                EventUCI.Set();
            else if (log == "readyok")
                EventReady.Set();
            else if (log.StartsWith("bestmove"))
                EventStop.Set();

            lock (Logs)
                Logs.AppendLine(log);
        }
        
        public static void Wait(AutoResetEvent @event, int seconds)
        {
            var mileseconds = seconds * 1000;
            @event.WaitOne(mileseconds);
        }

        public static void Go()
        {
            EventStop.Reset();
            Write("go infinite");
        }
        public static void GoDepth(int depth)
        {
            EventStop.Reset();
            Write($"go depth {depth}");
        }
        public static void Go(int mileseconds)
        {
            EventStop.Reset();
            Write($"go movetime {mileseconds}");
        }
        public static void SetOption(string name, string value) => Write($"setoption name {name} value {value}");
        public static void Position(string fen) => Write($"position fen {fen}");
        public static void Stop() => Write("stop");
        public static void IsReady() { EventReady.Reset(); Write("isready"); }
        public static void UCI() { EventUCI.Reset(); Write("uci"); }

        public static void Write(string command)
        {
            Process.StandardInput.WriteLine(command);
            Process.StandardInput.Flush();

            Logs.AppendLine($"> {command}");
        }
        public class Setting
        {
            public string Name { get; set; }

            public int MultiPV = 3;

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
            public string EvalFile = "janggi-4d3de2fee245.nnue";
            public bool TsumeMode = false;
            public string VariantPath = "<empty>";

            public int Search_Depth { get; set; }

            public const string kFolderPath = "./Settings/";
            public const string kDefaultFileName = "DefaultSettings.ini";

            public static IReadOnlyList<Setting> Load()
            {
                if (!Directory.Exists(kFolderPath))
                    Directory.CreateDirectory(kFolderPath);

                var files = Directory.GetFiles(kFolderPath, "*.ini");
                var settings = new List<Setting>();

                if (files.Length == 0)
                {
                    var setting = new Setting() { Name = kDefaultFileName };
                    File.AppendAllText(Path.Combine(kFolderPath, kDefaultFileName), JsonConvert.SerializeObject(setting, Formatting.Indented));
                    settings.Add(setting);
                }
                else
                {
                    foreach (var file in files)
                    {
                        var json = File.ReadAllText(file);
                        var setting = JsonConvert.DeserializeObject<Setting>(json);
                        setting.Name = Path.GetFileName(file);

                        settings.Add(setting);
                    }
                }
                return settings;
            }
        }

    }
}