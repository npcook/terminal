using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace npcook.Terminal.Controls
{
	public class Deque<T> : ICollection<T>, IEnumerable<T>, ICollection, IEnumerable, IReadOnlyCollection<T>
	{
		const int DefaultInitialSize = 10;
		const int GrowthMultiplier = 2;

		T[] backing;
		int start;	// Index of first value
		int end;	// Index of value one-past-the-end
		int size;	// Actual size of collection

		public Deque()
		{
			backing = new T[DefaultInitialSize];
			start = 0;
			end = 0;
			size = 0;
		}

		public Deque(int capacity)
		{
			if (capacity <= 0)
				throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "Capacity must be a positive integer");

			backing = new T[capacity];
			start = 0;
			end = 0;
			size = 0;
		}

		public void PushFront(T value)
		{
			if (size == backing.Length)
				resize(size * GrowthMultiplier);
			start = (backing.Length + start - 1) % backing.Length;
			backing[start] = value;
			size++;
		}

		public void PushBack(T value)
		{
			if (size == backing.Length)
				resize(size * GrowthMultiplier);
			backing[end] = value;
			end = (end + 1) % backing.Length;
			size++;
		}

		public void Add(T value)
		{
			PushBack(value);
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

		public bool IsReadOnly
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

		public void CopyTo(T[] array, int index)
		{
			Array.Copy(backing, start, array, index, size);
		}

		public T this[int index]
		{
			get
			{
				if (index < 0 || index >= size)
					throw new ArgumentOutOfRangeException(nameof(index), index, "Index has to be within the bounds of the collection");
				return backing[(start + index) % backing.Length];
			}
			set
			{
				if (index < 0 || index >= size)
					throw new ArgumentOutOfRangeException(nameof(index), index, "Index has to be within the bounds of the collection");
				backing[(start + index) % backing.Length] = value;
			}
		}

		public bool Contains(T value)
		{
			foreach (var item in this)
			{
				if (Comparer<T>.Default.Compare(item, value) == 0)
					return true;
			}
			return false;
		}

		public void Insert(int index, T value)
		{
			Array.Copy(backing, index, backing, index + 1, size - index);
			backing[index] = value;
			end++;
			size++;
		}

		public bool Remove(T value)
		{
			int index = 0;
			foreach (var item in this)
			{
				if (Comparer<T>.Default.Compare(item, value) == 0)
					break;
				index++;
			}

			if (index < size)
			{
				RemoveAt(index);
				return true;
			}
			else
				return false;
		}

		public void RemoveAt(int index)
		{
			if (index < 0 || index >= size)
				throw new ArgumentOutOfRangeException(nameof(index), index, "Index has to be within the bounds of the collection");

			int actualIndex = (start + index) % backing.Length;
			int copyCount = Math.Min(size - index - 1, backing.Length - actualIndex - 1);
			Array.Copy(backing, actualIndex + 1, backing, actualIndex, copyCount);
			if (copyCount < size - index - 1)
			{
				backing[backing.Length - 1] = backing[0];
				Array.Copy(backing, 1, backing, 0, size - index - 1 - copyCount);
			}
			size--;
			end--;
			if (end == -1)
				end = backing.Length;
			backing[size] = default(T);
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
