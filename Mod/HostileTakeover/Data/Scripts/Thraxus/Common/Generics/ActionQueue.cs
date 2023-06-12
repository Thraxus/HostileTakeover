using System;
using System.Collections.Generic;

namespace HostileTakeover.Common.Generics
{
    public class ActionQueue
    {
        private readonly Queue<Action> _actionQueue = new Queue<Action>();

        public void Add(int delay, Action action)
        {
            if (delay > 0)
            {
                _actionQueue.Enqueue(() => Add(--delay, action));
                return;
            }
            action?.Invoke();
        }

        public void Execute(int iterationMax = 500)
        {
            ProcessQueue(iterationMax);
        }

        private void ProcessQueue(int iterationMax)
        {
            int queueCount = _actionQueue.Count > iterationMax ? iterationMax : _actionQueue.Count;
            while (queueCount-- > 0)
            {
                _actionQueue.Dequeue()?.Invoke();
            }
        }

        public void Reset()
        {
            _actionQueue.Clear();
        }
    }
}