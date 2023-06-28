namespace Stockfish_GUI
{
    partial class Form
    {
        /// <summary>
        /// 필수 디자이너 변수입니다.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// 사용 중인 모든 리소스를 정리합니다.
        /// </summary>
        /// <param name="disposing">관리되는 리소스를 삭제해야 하면 true이고, 그렇지 않으면 false입니다.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form 디자이너에서 생성한 코드

        /// <summary>
        /// 디자이너 지원에 필요한 메서드입니다. 
        /// 이 메서드의 내용을 코드 편집기로 수정하지 마세요.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle1 = new System.Windows.Forms.DataGridViewCellStyle();
            this.TimeBar = new System.Windows.Forms.TrackBar();
            this.PVStatus = new System.Windows.Forms.DataGridView();
            this.Depth = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.Nodes = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.Time = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.Hash = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.TBHits = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.Input = new System.Windows.Forms.TextBox();
            this.menuStrip1 = new System.Windows.Forms.MenuStrip();
            this.Run = new System.Windows.Forms.ToolStripMenuItem();
            this.AnalyzeMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.SwapMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator1 = new System.Windows.Forms.ToolStripSeparator();
            this.SkipMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.실행ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.BoardView = new System.Windows.Forms.PictureBox();
            this.Updater = new System.Windows.Forms.Timer(this.components);
            this.Console = new System.Windows.Forms.RichTextBox();
            this.PVView = new Stockfish_GUI.CustomDataGridView();
            this.Score = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.ID = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.PV = new System.Windows.Forms.DataGridViewTextBoxColumn();
            ((System.ComponentModel.ISupportInitialize)(this.TimeBar)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.PVStatus)).BeginInit();
            this.menuStrip1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.BoardView)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.PVView)).BeginInit();
            this.SuspendLayout();
            // 
            // TimeBar
            // 
            this.TimeBar.Enabled = false;
            this.TimeBar.LargeChange = 1;
            this.TimeBar.Location = new System.Drawing.Point(468, 27);
            this.TimeBar.Maximum = 0;
            this.TimeBar.Name = "TimeBar";
            this.TimeBar.Orientation = System.Windows.Forms.Orientation.Vertical;
            this.TimeBar.Size = new System.Drawing.Size(45, 500);
            this.TimeBar.TabIndex = 3;
            this.TimeBar.TickStyle = System.Windows.Forms.TickStyle.Both;
            this.TimeBar.ValueChanged += new System.EventHandler(this.TimeBar_ValueChanged);
            // 
            // PVStatus
            // 
            this.PVStatus.AllowUserToAddRows = false;
            this.PVStatus.AllowUserToDeleteRows = false;
            this.PVStatus.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.PVStatus.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.Depth,
            this.Nodes,
            this.Time,
            this.Hash,
            this.TBHits});
            this.PVStatus.EnableHeadersVisualStyles = false;
            this.PVStatus.Location = new System.Drawing.Point(519, 27);
            this.PVStatus.MultiSelect = false;
            this.PVStatus.Name = "PVStatus";
            this.PVStatus.ReadOnly = true;
            this.PVStatus.RowHeadersVisible = false;
            this.PVStatus.RowTemplate.Height = 23;
            this.PVStatus.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.PVStatus.Size = new System.Drawing.Size(453, 76);
            this.PVStatus.TabIndex = 8;
            // 
            // Depth
            // 
            this.Depth.HeaderText = "Depth";
            this.Depth.Name = "Depth";
            this.Depth.ReadOnly = true;
            this.Depth.Width = 90;
            // 
            // Nodes
            // 
            this.Nodes.HeaderText = "Nodes";
            this.Nodes.Name = "Nodes";
            this.Nodes.ReadOnly = true;
            this.Nodes.Width = 90;
            // 
            // Time
            // 
            this.Time.HeaderText = "Time";
            this.Time.Name = "Time";
            this.Time.ReadOnly = true;
            this.Time.Width = 90;
            // 
            // Hash
            // 
            this.Hash.HeaderText = "Hash";
            this.Hash.Name = "Hash";
            this.Hash.ReadOnly = true;
            this.Hash.Width = 90;
            // 
            // TBHits
            // 
            this.TBHits.HeaderText = "TB Hits";
            this.TBHits.Name = "TBHits";
            this.TBHits.ReadOnly = true;
            this.TBHits.Width = 90;
            // 
            // Input
            // 
            this.Input.BackColor = System.Drawing.SystemColors.AppWorkspace;
            this.Input.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.Input.Font = new System.Drawing.Font("Consolas", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.Input.ForeColor = System.Drawing.SystemColors.WindowText;
            this.Input.Location = new System.Drawing.Point(519, 505);
            this.Input.Name = "Input";
            this.Input.Size = new System.Drawing.Size(453, 22);
            this.Input.TabIndex = 11;
            this.Input.TextChanged += new System.EventHandler(this.Input_TextChanged);
            this.Input.VisibleChanged += new System.EventHandler(this.Input_TextChanged);
            this.Input.KeyDown += new System.Windows.Forms.KeyEventHandler(this.Input_KeyDown);
            // 
            // menuStrip1
            // 
            this.menuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.Run});
            this.menuStrip1.Location = new System.Drawing.Point(0, 0);
            this.menuStrip1.Name = "menuStrip1";
            this.menuStrip1.Size = new System.Drawing.Size(984, 24);
            this.menuStrip1.TabIndex = 12;
            this.menuStrip1.Text = "menuStrip1";
            // 
            // Run
            // 
            this.Run.Checked = true;
            this.Run.CheckState = System.Windows.Forms.CheckState.Checked;
            this.Run.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.AnalyzeMenuItem,
            this.SwapMenuItem,
            this.toolStripSeparator1,
            this.SkipMenuItem});
            this.Run.Name = "Run";
            this.Run.Size = new System.Drawing.Size(43, 20);
            this.Run.Text = "옵션";
            // 
            // AnalyzeMenuItem
            // 
            this.AnalyzeMenuItem.CheckOnClick = true;
            this.AnalyzeMenuItem.Name = "AnalyzeMenuItem";
            this.AnalyzeMenuItem.Size = new System.Drawing.Size(180, 22);
            this.AnalyzeMenuItem.Text = "분석 모드 (F1)";
            this.AnalyzeMenuItem.CheckedChanged += new System.EventHandler(this.AnalyzeMenuItem_CheckedChanged);
            // 
            // SwapMenuItem
            // 
            this.SwapMenuItem.Name = "SwapMenuItem";
            this.SwapMenuItem.Size = new System.Drawing.Size(180, 22);
            this.SwapMenuItem.Text = "색상 변경 (F2)";
            this.SwapMenuItem.Click += new System.EventHandler(this.SwapMenuItem_Click);
            // 
            // toolStripSeparator1
            // 
            this.toolStripSeparator1.Name = "toolStripSeparator1";
            this.toolStripSeparator1.Size = new System.Drawing.Size(177, 6);
            // 
            // SkipMenuItem
            // 
            this.SkipMenuItem.Name = "SkipMenuItem";
            this.SkipMenuItem.Size = new System.Drawing.Size(180, 22);
            this.SkipMenuItem.Text = "무르기 (F5)";
            this.SkipMenuItem.Click += new System.EventHandler(this.SkipMenuItem_Click);
            // 
            // 실행ToolStripMenuItem
            // 
            this.실행ToolStripMenuItem.Name = "실행ToolStripMenuItem";
            this.실행ToolStripMenuItem.Size = new System.Drawing.Size(43, 20);
            this.실행ToolStripMenuItem.Text = "실행";
            // 
            // BoardView
            // 
            this.BoardView.BackgroundImageLayout = System.Windows.Forms.ImageLayout.None;
            this.BoardView.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.BoardView.Image = global::Stockfish_GUI.Properties.Resources.board;
            this.BoardView.InitialImage = null;
            this.BoardView.Location = new System.Drawing.Point(12, 27);
            this.BoardView.Name = "BoardView";
            this.BoardView.Size = new System.Drawing.Size(450, 500);
            this.BoardView.TabIndex = 10;
            this.BoardView.TabStop = false;
            // 
            // Updater
            // 
            this.Updater.Enabled = true;
            this.Updater.Interval = 16;
            this.Updater.Tick += new System.EventHandler(this.Receive);
            // 
            // Console
            // 
            this.Console.BackColor = System.Drawing.SystemColors.AppWorkspace;
            this.Console.DetectUrls = false;
            this.Console.Font = new System.Drawing.Font("Consolas", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.Console.Location = new System.Drawing.Point(519, 262);
            this.Console.Name = "Console";
            this.Console.ReadOnly = true;
            this.Console.ScrollBars = System.Windows.Forms.RichTextBoxScrollBars.Vertical;
            this.Console.Size = new System.Drawing.Size(453, 238);
            this.Console.TabIndex = 13;
            this.Console.Text = "";
            this.Console.WordWrap = false;
            // 
            // PVView
            // 
            this.PVView.AllowUserToAddRows = false;
            this.PVView.AllowUserToDeleteRows = false;
            this.PVView.AllowUserToResizeRows = false;
            this.PVView.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.PVView.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.Score,
            this.ID,
            this.PV});
            this.PVView.Cursor = System.Windows.Forms.Cursors.Hand;
            dataGridViewCellStyle1.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle1.BackColor = System.Drawing.SystemColors.Window;
            dataGridViewCellStyle1.Font = new System.Drawing.Font("굴림", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(129)));
            dataGridViewCellStyle1.ForeColor = System.Drawing.SystemColors.ControlText;
            dataGridViewCellStyle1.SelectionBackColor = System.Drawing.Color.Transparent;
            dataGridViewCellStyle1.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle1.WrapMode = System.Windows.Forms.DataGridViewTriState.False;
            this.PVView.DefaultCellStyle = dataGridViewCellStyle1;
            this.PVView.EnableHeadersVisualStyles = false;
            this.PVView.Location = new System.Drawing.Point(519, 109);
            this.PVView.MultiSelect = false;
            this.PVView.Name = "PVView";
            this.PVView.ReadOnly = true;
            this.PVView.RowHeadersVisible = false;
            this.PVView.RowTemplate.Height = 23;
            this.PVView.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.PVView.Size = new System.Drawing.Size(453, 147);
            this.PVView.TabIndex = 5;
            this.PVView.CellMouseDown += new System.Windows.Forms.DataGridViewCellMouseEventHandler(this.PVView_CellMouseDown);
            // 
            // Score
            // 
            this.Score.HeaderText = "Score";
            this.Score.Name = "Score";
            this.Score.ReadOnly = true;
            this.Score.Width = 90;
            // 
            // ID
            // 
            this.ID.HeaderText = "ID";
            this.ID.Name = "ID";
            this.ID.ReadOnly = true;
            this.ID.Width = 50;
            // 
            // PV
            // 
            this.PV.HeaderText = "PV";
            this.PV.Name = "PV";
            this.PV.ReadOnly = true;
            this.PV.Width = 310;
            // 
            // Form
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(984, 541);
            this.Controls.Add(this.Console);
            this.Controls.Add(this.menuStrip1);
            this.Controls.Add(this.Input);
            this.Controls.Add(this.BoardView);
            this.Controls.Add(this.PVStatus);
            this.Controls.Add(this.PVView);
            this.Controls.Add(this.TimeBar);
            this.MainMenuStrip = this.menuStrip1;
            this.Name = "Form";
            this.Text = "StockFish-GUI";
            this.Load += new System.EventHandler(this.Form_Load);
            ((System.ComponentModel.ISupportInitialize)(this.TimeBar)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.PVStatus)).EndInit();
            this.menuStrip1.ResumeLayout(false);
            this.menuStrip1.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.BoardView)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.PVView)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion
        public System.Windows.Forms.TrackBar TimeBar;
        private System.Windows.Forms.DataGridView PVStatus;
        public System.Windows.Forms.PictureBox BoardView;
        private System.Windows.Forms.TextBox Input;
        private System.Windows.Forms.MenuStrip menuStrip1;
        private System.Windows.Forms.ToolStripMenuItem 실행ToolStripMenuItem;
        private CustomDataGridView PVView;
        private System.Windows.Forms.Timer Updater;
        public System.Windows.Forms.RichTextBox Console;
        private System.Windows.Forms.DataGridViewTextBoxColumn Depth;
        private System.Windows.Forms.DataGridViewTextBoxColumn Nodes;
        private System.Windows.Forms.DataGridViewTextBoxColumn Time;
        private System.Windows.Forms.DataGridViewTextBoxColumn Hash;
        private System.Windows.Forms.DataGridViewTextBoxColumn TBHits;
        private System.Windows.Forms.DataGridViewTextBoxColumn Score;
        private System.Windows.Forms.DataGridViewTextBoxColumn ID;
        private System.Windows.Forms.DataGridViewTextBoxColumn PV;
        private System.Windows.Forms.ToolStripMenuItem Run;
        private System.Windows.Forms.ToolStripMenuItem AnalyzeMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator1;
        private System.Windows.Forms.ToolStripMenuItem SkipMenuItem;
        private System.Windows.Forms.ToolStripMenuItem SwapMenuItem;
    }
}

