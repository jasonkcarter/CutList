using System;

namespace CutList
{
    public class CutOrderEventArgs : EventArgs
    {
        public CutOrderEventArgs(CutOrder cutOrder)
        {
            CutOrder = cutOrder;
        }

        public CutOrder CutOrder { get; private set; }
    }
}