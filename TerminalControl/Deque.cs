using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace npcook.Terminal.Controls
{
	class Deque<T> : ICollection, IEnumerable<T>, IReadOnlyCollection<T>
	{
		T[] backing;
		int start;	// Index of first value
		int end;	// Index of value one-past-the-end
		int size;

		public Deque()
		{
			backing = new T[0];
			start = 0;
			end = 0;
			size = 0;
		}

		public Deque(int capacity)
		{
			backing = new T[capacity];
			start = 0;
			end = 0;
			size = 0;
		}

		public void PushFront(T value)
		{
			if (size == backing.Length)
				resize(size * 2);
			start = (start - 1) % backing.Length;
			backing[start] = value;
			size++;
		}

		public void PushBack(T value)
		{
			if (size == backing.Length)
				resize(size * 2);
			backing[end] = value;
			end = (end + 1) % backing.Length;
			size++;
		}

		public T PopFront()
		{
			if (size == 0)
				throw new InvalidOperationException("Cannot pop from an empty deque");

			T value = backing[start];
			backing[start] = default(T);
			start = (start + 1) % backing.Length;
			size--;
			return value;
		}

		public T PopBack()
		{
			if (size == 0)
				throw new InvalidOperationException("Cannot pop from an empty deque");

			end = (end - 1) % backing.Length;
			T value = backing[end];
			backing[end] = default(T);
			size--;
			return value;
		}

		public void Clear()
		{
			if (start > end)
			{
				Array.Clear(backing, start, backing.Length - start);
				Array.Clear(backing, 0, end);
			}
			else
			{
				Array.Clear(backing, start, size);
			}
			start = 0;
			end = 0;
			size = 0;
		}

		void resize(int capacity)
		{
			throw new NotImplementedException();
		}

		public int Count
		{ get { return size; } }

		public object SyncRoot
		{ get { return backing.SyncRoot; } }

		public bool IsSynchronized
		{ get { return false; } }

		IEnumerator IEnumerable.GetEnumerator()
		{
			return new Enumerator(this);
		}

		IEnumerator<T> IEnumerable<T>.GetEnumerator()
		{
			return new Enumerator(this);
		}

		public void CopyTo(Array array, int index)
		{
			Array.Copy(backing, start, array, index, size);
		}

		public T this[int index]
		{
			get
			{
				return backing[(start + index) % backing.Length];
			}
		}

		public struct Enumerator : IEnumerator<T>, IEnumerator
		{
			Deque<T> parent;
			int index;

			internal Enumerator(Deque<T> parent)
			{
				this.parent = parent;
				index = -1;
			}

			public T Current
			{ get { return parent[index]; } }

			object IEnumerator.Current
			{ get { return Current; } }

			public void Dispose()
			{
				index = -2;
			}

			public bool MoveNext()
			{
				if (index == -2)
					return false;

				index++;
				if (index == parent.Count)
				{
					index = -2;
					return false;
				}

				return true;
			}

			public void Reset()
			{
				index = -1;
			}
		}
	}
}
