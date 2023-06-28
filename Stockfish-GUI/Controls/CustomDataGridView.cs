using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Stockfish_GUI
{
    public class CustomDataGridView : DataGridView
    {
        public CustomDataGridView()
        {
            DoubleBuffered = true;
        }
        protected override void OnRowPostPaint(DataGridViewRowPostPaintEventArgs e)
        {
            base.OnRowPostPaint(e);

            var bounds = e.RowBounds;
            var screen = RectangleToScreen(bounds);

            if (screen.Contains(MousePosition))
            {
                using var brush = new SolidBrush(Color.FromArgb(50, Color.Green));
                using var pen = new Pen(Color.Green);

                var offset = new Size(1, 1);
                var rect = new Rectangle(bounds.Location, Size.Subtract(bounds.Size, offset));
                e.Graphics.FillRectangle(brush, rect);
                e.Graphics.DrawRectangle(pen, rect);
            }
        }
        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e); 
            Invalidate();
        }
        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e); 
            Invalidate();
        }
        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e); 
            Invalidate();
        }
        protected override void OnScroll(ScrollEventArgs e)
        {
            base.OnScroll(e); 
            Invalidate();
        }
        protected override void OnCellMouseEnter(DataGridViewCellEventArgs e)
        {
            base.OnCellMouseEnter(e);
            Cursor = Cursors.Hand;
        }
        protected override void OnCellMouseLeave(DataGridViewCellEventArgs e)
        {
            base.OnCellMouseLeave(e);
            Cursor = Cursors.Default;
        }
        protected override void SetSelectedRowCore(int rowIndex, bool selected) { }
    }
}
