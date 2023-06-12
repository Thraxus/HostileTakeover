using System;

namespace HostileTakeover.Common.Interfaces
{
    public interface ILog
    {
        event Action<string, string> OnWriteToLog;
        void WriteGeneral(string caller, string message);
    }
}
