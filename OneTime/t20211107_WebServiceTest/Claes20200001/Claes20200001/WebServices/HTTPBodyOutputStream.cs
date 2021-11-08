using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using Charlotte.Commons;

namespace Charlotte.WebServices
{
	public class HTTPBodyOutputStream : IDisposable
	{
		private WorkingDir WD = null;
		private string BuffFile = null;
		private int WroteSize = 0;

		private string GetBuffFile()
		{
			if (this.WD == null)
			{
				this.WD = new WorkingDir();
				this.BuffFile = this.WD.MakePath();
			}
			return this.BuffFile;
		}

		public void Write(byte[] data, int offset = 0)
		{
			this.Write(data, offset, data.Length - offset);
		}

		public void Write(byte[] data, int offset, int count)
		{
			using (FileStream writer = new FileStream(this.GetBuffFile(), FileMode.Append, FileAccess.Write))
			{
				writer.Write(data, offset, count);
			}
			this.WroteSize += count;
		}

		public int Count
		{
			get
			{
				return this.WroteSize;
			}
		}

		public byte[] ToByteArray()
		{
			byte[] data;

			if (this.WroteSize == 0)
			{
				data = SCommon.EMPTY_BYTES;
			}
			else
			{
				data = File.ReadAllBytes(this.BuffFile);
				File.WriteAllBytes(this.BuffFile, SCommon.EMPTY_BYTES);
				this.WroteSize = 0;
			}
			return data;
		}

		public void Dispose()
		{
			if (this.WD != null)
			{
				this.WD.Dispose();
				this.WD = null;
			}
		}
	}
}
