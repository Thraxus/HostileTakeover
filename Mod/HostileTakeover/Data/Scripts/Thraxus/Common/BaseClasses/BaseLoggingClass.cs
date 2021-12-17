using System;
using HostileTakeover.Common.Interfaces;

namespace HostileTakeover.Common.BaseClasses
{
	public abstract class BaseLoggingClass : ICommon
	{
		public event Action<string, string> OnWriteToLog;
		public event Action<ICommon> OnClose;

		public bool IsClosed { get; private set; }

		public virtual void Close()
		{
			if (IsClosed) return;
			IsClosed = true;
			OnClose?.Invoke(this);
		}

		public virtual void Update(ulong tick) { }

		public virtual void WriteToLog(string caller, string message)
		{
			OnWriteToLog?.Invoke(caller, message);
		}
	}
}