using System;
using System.Drawing;
using System.Windows.Forms;

namespace WpsPasswordManager.UI.Controls
{
    public class AuthTreeView : TreeView
    {
        private const int WM_LBUTTONDBLCLK = 0x0203;

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_LBUTTONDBLCLK)
            {
                var pos = PointToClient(Cursor.Position);
                var info = HitTest(pos);

                if (info.Location == TreeViewHitTestLocations.StateImage)
                {
                    if (info.Node?.Parent != null && info.Node.Parent.Checked)
                    {
                        return;
                    }
                }
            }
            base.WndProc(ref m);
        }
    }
}