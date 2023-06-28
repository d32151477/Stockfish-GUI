
using Stockfish_GUI.Properties;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Stockfish_GUI
{
    public partial class Form : System.Windows.Forms.Form
    {
        public static Form Instance;

        public Board Board;
        public Form()
        {
            Instance = this;
            InitializeComponent();
        }

        private static int PVOrder = 0;
        private static PVInfo[] PVInfos;

        public bool CanReceivePVInfos;

        private void ReceiveLogs()
        {
            while (true)
            {
                while (Core.Logs.Count > 0)
                {
                    if (Core.Logs.TryDequeue(out var log))
                    {
                        Invoke(() =>
                        {
                            Console.AppendText(log);
                            Console.AppendText(Environment.NewLine);
                        });
                    }
                }
            }
        }
        private void ReceivePVInfos()
        {
            while (true)
            {
                if (Core.IsPVDirty && CanReceivePVInfos)
                {
                    lock (Core.PVInfos)
                    {
                        Core.IsPVDirty = false;

                        PVInfos ??= new PVInfo[Core.PVInfos.Length];
                        for (int i = 0; i < PVInfos.Length; i++)
                            PVInfos[i] = Core.PVInfos[i];
                    }

                    var infos = PVInfos;
                    var enumerable = from info in infos where info is not null orderby info.mate orderby info.cp descending select info;

                    int index = 0;

                    foreach (var info in enumerable)
                    {
                        var score = info.mate == 0 ? $"{info.cp}" : $"#{info.mate}";

                        if (index < PVView.Rows.Count)
                        {
                            var row = PVView.Rows[index];
                            row.SetValues(score, info.multipv, info.pv);
                        }
                        else
                            Invoke(() => PVView.Rows.Add(info.cp, info.multipv, info.pv));

                        index++;
                    }

                    int order = 0;
                    foreach (var info in enumerable)
                    {
                        order *= 10;
                        order += info.multipv;
                    }
                    if (order != PVOrder)
                    {
                        Board.DrawArrows(enumerable);
                        Invoke(() => BoardView.Invalidate());
                        PVOrder = order;
                    }

                    enumerable = from info in infos where info is not null orderby info.time descending select info;
                    if (enumerable.Any())
                    {
                        var status = enumerable.First();

                        var depth = $"{status.depth} / {status.seldepth}";
                        var span = TimeSpan.FromMilliseconds(status.time);
                        var time = $"{span.Hours:D2}:{span.Minutes:D2}:{span.Seconds:D2}";

                        if (PVStatus.Rows.Count > 0)
                        {
                            var row = PVStatus.Rows[0];
                            row.SetValues(depth, status.nodes, time, status.hashfull, status.tbhits);
                        }
                        else
                            Invoke(() => PVStatus.Rows.Add(depth, status.nodes, time, status.hashfull, status.tbhits));
                    }
                }
            }
        }
        public void Pause()
        {
            CanReceivePVInfos = false;

            PVView.Rows.Clear();
            PVStatus.Rows.Clear();
        }
        public void Resume()
        {
            CanReceivePVInfos = true;
        }

        public void Print(string text)
        {
            Console.AppendText($"{text}{Environment.NewLine}");
        }
        private void Form_Load(object sender, EventArgs e)
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.DoubleBuffer, true);
            DoubleBuffered = true;

            PVView.CellMouseEnter += PVView_CellMouseEnter;
            PVView.CellMouseLeave += PVView_CellMouseLeave;

            Board = new Board(this);
            Board.Start();

            var fens = Board.GetFen();

            Core.Start();

            Core.UCI();
            Core.Wait(Core.EventUCI, 10);
            
            Core.LoadOptions();
            Core.IsReady();
            Core.Wait(Core.EventReady, 10);

            Core.Position(fens);

            Task.Run(ReceiveLogs);
            Task.Run(ReceivePVInfos);
        }

        private void PVView_CellMouseEnter(object sender, DataGridViewCellEventArgs e)
        {
            Board.CurrentArrowIndex = e.RowIndex;
            BoardView.Invalidate();
        }
        private void PVView_CellMouseLeave(object sender, DataGridViewCellEventArgs e)
        {
            Board.CurrentArrowIndex = -1;
            BoardView.Invalidate();
        }
        private void Input_TextChanged(object sender, EventArgs e)
        {
            if (Input.Text.StartsWith("> "))
                return;
            if (Input.Text.StartsWith(">"))
                Input.Text = $"> {Input.Text.Substring(1)}";
            else
                Input.Text = $"> {Input.Text}";
            Input.SelectionStart = Input.Text.Length;
        }

        private void Input_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                const int kInputPrefixLength = 2;
                var command = Input.Text.Substring(kInputPrefixLength);
                Core.Write(command);

                Input.Text = string.Empty;
            }
        }


        private void PVView_CellMouseDown(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.RowIndex < 0)
                return;

            var row = PVView.Rows[e.RowIndex];
            var cell = row.Cells["PV"];
            var pv = cell.Value as string;

            var raw2 = Board.GetRaw2FromPV(pv);
            Board.MoveByRaw2(raw2);
        }

        private void menuStrip1_ItemClicked(object sender, ToolStripItemClickedEventArgs e)
        {
        }
    }
}
