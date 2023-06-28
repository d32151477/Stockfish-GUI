using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Stockfish_GUI
{
    public class PVInfo
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

    internal static class Core
    {
        private const string kEnginePath = "./Engines/fairy-stockfish.exe";

        private static Settings s_Settings;

        public static bool IsPVDirty;
        public static PVInfo[] PVInfos;
        public static readonly ConcurrentQueue<string> Logs = new();

        public static Form Form;
        public static Process Process;

        public static AutoResetEvent EventStop = new(false);
        public static AutoResetEvent EventReady = new(false);
        public static AutoResetEvent EventUCI = new(false);

        public static void Reset()
        {
            for (int i = 0; i < PVInfos.Length; i++)
                PVInfos[i] = null;
        }
        public static void Start()
        {
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
        }
        public static void LoadOptions()
        {
            if (File.Exists("./Settings.ini"))
            {
                var json = File.ReadAllText("./Settings.ini");
                s_Settings = JsonConvert.DeserializeObject<Settings>(json);
            }
            else
            {
                s_Settings = new Settings();
                File.AppendAllText("./Settings.ini", JsonConvert.SerializeObject(s_Settings, Formatting.Indented));
            }
            PVInfos = new PVInfo[s_Settings.MultiPV];

            var fields = typeof(Settings).GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            foreach (var field in fields)
            {
                var attribute = field.GetCustomAttribute<JsonPropertyAttribute>();

                var name = attribute?.PropertyName ?? field.Name;
                var value = field.GetValue(s_Settings).ToString();

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
                if (detail.StartsWith ("currmove"))
                    return;

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

                    if (!cp.Success)
                    {
                        int sign = Math.Sign(info.mate);
                        if (sign != 0)
                            info.cp = (int.MaxValue - info.mate) * sign;
                    }

                    var pv = Regex.Match(detail, @"\spv\s(.+)$");
                    if (pv.Success)
                        info.pv = pv.Groups[1].Value;

                    lock (PVInfos)
                    {
                        PVInfos[info.multipv - 1] = info;
                        IsPVDirty = true;
                    }
                }
            }
            else if (log == "uciok")
                EventUCI.Reset();
            else if (log == "readyok")
                EventReady.Reset();
            else if (log.StartsWith("bestmove"))
                EventStop.Reset();

            Logs.Enqueue(log);
        }

        public static void Go() => Write("go infinite");
        public static void Go(int mileseconds) => Write($"go movetime {mileseconds}");
        public static void SetOption(string name, string value) => Write($"setoption name {name} value {value}");
        public static void Position(string fen) => Write($"position fen {fen}");

        public static void Stop() { EventStop.Set(); Write("stop"); }
        public static void IsReady() { EventReady.Set(); Write("isready"); }
        public static void UCI() { EventUCI.Set(); Write("uci"); }

        public static void Wait(AutoResetEvent @event, int seconds)
        {
            var mileseconds = seconds * 1000;
            @event.WaitOne(mileseconds);
        }

        public static void Display() => Write("d");
        public static void Write(string command)
        {
            Process.StandardInput.WriteLine(command);
            Process.StandardInput.Flush();
            Logs.Enqueue($"> {command}");
        }
    }
}