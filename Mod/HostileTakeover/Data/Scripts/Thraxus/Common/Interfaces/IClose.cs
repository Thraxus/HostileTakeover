using System;

namespace HostileTakeover.Common.Interfaces
{
    internal interface IClose
    {
        event Action<IClose> OnClose;
        void Close();
    }
}
