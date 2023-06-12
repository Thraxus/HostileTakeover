using System;

namespace HostileTakeover.Common.Interfaces
{
    internal interface IReset
    {
        event Action<IReset> OnReset;
        void Reset();
    }
}
