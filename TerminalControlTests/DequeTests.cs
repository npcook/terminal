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
			test.Insert(3, 7);
			test.Insert(3, 8);

			CollectionAssert.AreEqual(test.ToArray(), new int[] { 0, 1, 2, 8, 7, 3, 4, 5, 6 });
			Assert.AreEqual(test.Count, 9);
		}

		[TestMethod]
		public void TestRemoveAt()
		{
			Deque<int> test = new Deque<int>(10);
			test.Add(0);
			test.Add(1);
			test.Add(2);
			test.Add(3);
			test.Add(4);
			test.Add(5);
			test.Add(6);

			test.RemoveAt(2);

			CollectionAssert.AreEqual(test.ToArray(), new int[] { 0, 1, 3, 4, 5, 6 });
			Assert.AreEqual(test.Count, 6);
		}
	}
}
