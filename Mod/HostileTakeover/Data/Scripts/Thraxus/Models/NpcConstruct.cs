using System;
using HostileTakeover.Common.Interfaces;

namespace HostileTakeover.Models
{
    /// <summary>
    /// Contains logic that is specific to NPC owned constructs
    ///     This class should be reset when passed transferred to a Player Construct
    /// </summary>
    internal class NpcConstruct : ILog, IReset
    {
        public NpcConstruct(Construct construct)
        {
            _construct = construct;
        }

        #region Fields

        private readonly Construct _construct;

        #endregion

        #region Events
        public event Action<string, string> OnWriteToLog;
        public event Action<IReset> OnReset;

        #endregion

        #region Local Event Invokers
        public void WriteGeneral(string caller, string message) => OnWriteToLog?.Invoke(caller, message);

        public void Reset()
        {
            OnReset?.Invoke(this);
        }

        #endregion

        #region External Event Registers

        #endregion

        #region External Event Handlers

        #endregion

        #region External Event DeRegisters

        #endregion

        #region Props and Expressions

        #endregion

        #region Initialization

        #endregion

    }
}