using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace npcook.Terminal
{
	public interface IStreamNotifier
	{
		Stream Stream
		{ get; }

		event EventHandler DataAvailable;
	}
}
