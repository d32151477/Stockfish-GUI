
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
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

        private static PVInfo[] PVInfos;
        public bool CanReceivePVInfos = true;


        private void Receive(object sender, EventArgs e)
        {
            lock (Core.Logs)
            {
                if (Core.Logs.Length > 0)
                {
                    Console.AppendText($"{Core.Logs}");
                    Core.Logs.Clear();
                }
            }
            if (CanReceivePVInfos)
            {
                if (Core.IsPVDirty)
                {
                    PVInfos ??= new PVInfo[Core.PVInfos.Length];

                    var latest = Core.Latest;
                    lock (Core.PVInfos)
                    {
                        Core.IsPVDirty = false;
                        for (int i = 0; i < PVInfos.Length; i++)
                            PVInfos[i] = Core.PVInfos[i];
                    }

                    var depth = $"{latest.depth} / {latest.seldepth}";
                    var span = TimeSpan.FromMilliseconds(latest.time);
                    var time = $"{span.Hours:D2}:{span.Minutes:D2}:{span.Seconds:D2}";

                    var infos = PVInfos;
                    var enumerable = from info in infos where info.depth > 0 orderby info.cp descending select info;
                    var count = enumerable.Count();

                    if (PVStatus.Rows.Count > 0)
                    {
                        var row = PVStatus.Rows[0];
                        row.SetValues(depth, latest.nodes, time, latest.hashfull, latest.tbhits);
                    }
                    else
                        PVStatus.Rows.Add(depth, latest.nodes, time, latest.hashfull, latest.tbhits);

                    int index = 0;
                    int difference = Math.Abs(PVView.Rows.Count - count);
                    if (count < PVView.Rows.Count)
                        for (int i = 0; i < difference; i++)
                            PVView.Rows.RemoveAt(PVView.Rows.Count - 1);

                    else if (count > PVView.Rows.Count)
                        for (int i = 0; i < difference; i++)
                            PVView.Rows.Add();

                    foreach (var info in enumerable)
                    {
                        var score = info.mate == 0 ? $"{info.cp}" : $"#{info.mate}";
                        var row = PVView.Rows[index];
                        row.SetValues(score, info.multipv, info.pv);

                        index++;
                    }

                    Board.DrawArrows(enumerable);
                    PVView.Invalidate();
                    BoardView.Invalidate();
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
            KeyPreview = true;

            PVView.CellMouseEnter += PVView_CellMouseEnter;
            PVView.CellMouseLeave += PVView_CellMouseLeave;
            
            Core.Start();
            
            Core.UCI();
            Core.Wait(Core.EventUCI, seconds: 5);
            
            Core.LoadOptions();
            Core.IsReady();
            Core.Wait(Core.EventReady, seconds: 5);
            
            Board = new Board(this);
            Board.Create();
            Board.Start();
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
        private void TimeBar_ValueChanged(object sender, EventArgs e)
        {
            TimeBar.Enabled = TimeBar.Maximum > 0;

            while (TimeBar.Value < Board.MovementFocus)
                Board.Undo();
            while (TimeBar.Value > Board.MovementFocus)
                Board.Redo();
        }
        private void SkipMenuItem_Click(object sender, EventArgs e)
        {
            Board.Switch();
        }

        private void AnalyzeMenuItem_CheckedChanged(object sender, EventArgs e)
        {
            if (AnalyzeMenuItem.Checked)
                Board.Start();
            else
                Board.Pause();
        }

        private void SwapMenuItem_Click(object sender, EventArgs e)
        {
            Board.Swap();
        }
        private void AnalyzeMenuItem_Click(object sender, EventArgs e)
        {
            AnalyzeMenuItem_CheckedChanged(sender, e);
        }

        private void Form_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.KeyCode)
            {
                case Keys.F1:
                    AnalyzeMenuItem.Checked = !AnalyzeMenuItem.Checked;
                    AnalyzeMenuItem_Click(sender, e);
                    break;
                case Keys.F2:
                    SwapMenuItem_Click(sender, e);
                    break;
                case Keys.F5:
                    SkipMenuItem_Click(sender, e);
                    break;
                case Keys.N when e.Modifiers.HasFlag(Keys.Control):
                    NewGameMenuItem_Click(sender, e);
                    break;
                case Keys.Space when e.Modifiers.HasFlag(Keys.Control):
                    ParseMenuItem_Click(sender, e);
                    break;
            }
        }
        private void NewGameMenuItem_Click(object sender, EventArgs e)
        {
            TimeBar.Enabled = false;
            TimeBar.Maximum = 0;

            Board.Reset();
            Board.Create();
            Board.Pause();
            if (AnalyzeMenuItem.Checked)
                Board.Start();
        }

        private void ParseMenuItem_Click(object sender, EventArgs e)
        {
            Parser.Execute();
        }

        private void 통계창ToolStripMenuItem_Click(object sender, EventArgs e)
        {

        }
    }
}
