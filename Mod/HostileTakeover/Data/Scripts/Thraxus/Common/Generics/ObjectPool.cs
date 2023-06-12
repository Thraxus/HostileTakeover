using System;
using System.Collections.Concurrent;

namespace HostileTakeover.Common.Generics
{
    internal class ObjectPool<T>
    {
        private readonly ConcurrentBag<T> _objects;
        private readonly Func<T> _objectGenerator;

        public ObjectPool(Func<T> objectGenerator)
        {
            _objectGenerator = objectGenerator;
            _objects = new ConcurrentBag<T>();
        }

        public T Get()
        {
            T item;
            return _objects.TryTake(out item) ? item : _objectGenerator();
        }

        public void Return(T item) => _objects.Add(item);
    }
}

/*
 * Example Usage:
 * private GenericObjectPool<GrindOperation> _grindOperations = new GenericObjectPool<GrindOperation>(() => new GrindOperation(_userSettings));
 *
 * GrindOperation op =  _grindOperations.Get();
 *
		private void ClearGrindOperationsPool()
		{
			for (int i = _pooledGrindOperations.Count - 1; i >= 0; i--)
			{
				if (_pooledGrindOperations[i].Tick == TickCounter) continue;
				GrindOperation op = _pooledGrindOperations[i];
				_pooledGrindOperations.RemoveAtImmediately(i);
				op.OnWriteToLog -= WriteToLog;
				op.Reset();
				_grindOperations.Return(op);
			}
			_pooledGrindOperations.ApplyRemovals();
		}
 *
 * Make sure to clean up the object before returning it to the pool, else you may carry over obsolete / incorrect info
 * 
 */