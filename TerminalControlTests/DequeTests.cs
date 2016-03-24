using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Linq;

namespace npcook.Terminal.Controls.Tests
{
	[TestClass]
	public class DequeTests
	{
		[TestMethod]
		public void TestInsert()
		{
			Deque<int> test = new Deque<int>(10);
			test.Add(0);
			test.Add(1);
			test.Add(2);
			test.Add(3);
			test.Add(4);
			test.Add(5);
			test.Add(6);
			test.Add(7);
			test.Add(8);
			test.Add(9);
			test.RemoveAt(0);
			test.RemoveAt(0);
			test.Insert(3, 7);
			test.Insert(3, 8);

			CollectionAssert.AreEqual(test.ToArray(), new int[] { 2, 3, 4, 8, 7, 5, 6, 7, 8, 9 });
			Assert.AreEqual(test.Count, 10);
		}

		[TestMethod]
		public void TestRemoveAt()
		{
			Deque<int> test = new Deque<int>(6);
			test.Add(0);
			test.Add(1);
			test.Add(2);
			test.Add(3);
			test.Add(4);
			test.Add(5);

			test.PopFront();
			test.PopFront();

			test.Add(6);
			test.Add(7);

			test.RemoveAt(2);

			int[] arr = test.ToArray();

			CollectionAssert.AreEqual(arr, new int[] { 2, 3, 5, 6, 7 });
			Assert.AreEqual(test.Count, 5);
		}
	}
}
