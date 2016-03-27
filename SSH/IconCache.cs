using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace npcook.Ssh
{
	class IconCache
	{
		Dictionary<string, Dictionary<string, ImageSource>> icons = new Dictionary<string, Dictionary<string, ImageSource>>();
		LinkedList<string> directories = new LinkedList<string>();

		public int DirectoryCount
		{ get { return directories.Count; } }

		public void AddDirectory(string path)
		{
			if (!icons.ContainsKey(path))
			{
				var node = directories.Find(path);
				directories.Remove(node);
				directories.AddFirst(node);
			}
			else
			{
				icons.Add(path, new Dictionary<string, ImageSource>());
				directories.AddFirst(path);
			}
		}

		public void RemoveOldestDirectory()
		{
			if (directories.Last != null)
			{
				string directory = directories.Last.Value;
				directories.RemoveLast();
				icons.Remove(directory);
			}
		}

		public void AddIcon(string directory, string name, ImageSource icon)
		{
			if (!icon.IsFrozen)
				icon.Freeze();

			icons[directory][name] = icon;
		}

		public ImageSource GetIcon(string directory, string name)
		{
			Dictionary<string, ImageSource> directoryIcons;
			if (icons.TryGetValue(directory, out directoryIcons))
			{
				ImageSource icon;
				if (directoryIcons.TryGetValue(name, out icon))
					return icon;
			}
			return null;
		}
	}
}
