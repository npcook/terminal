using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Renci.SshNet.Sftp;

namespace Sftp
{
    public class RemoteDirectory
    {
		List<SftpFile> files;
		public IEnumerable<SftpFile> Files
		{ get { return files; } }


    }
}
