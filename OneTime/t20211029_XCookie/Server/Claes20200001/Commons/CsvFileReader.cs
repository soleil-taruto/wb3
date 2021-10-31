using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace Charlotte.Commons
{
	public class CsvFileReader : IDisposable
	{
		public const char DELIMITER_COMMA = ',';
		public const char DELIMITER_TAB = '\t';

		private char Delimiter = DELIMITER_COMMA;

		private StreamReader Reader;

		public CsvFileReader(string file)
			: this(file, SCommon.ENCODING_SJIS)
		{ }

		public CsvFileReader(string file, Encoding encoding)
			: this(file, encoding, DELIMITER_COMMA)
		{ }

		public CsvFileReader(string file, Encoding encoding, char delimiter)
			: this(new StreamReader(file, encoding))
		{
			this.Delimiter = delimiter;
		}

		public CsvFileReader(StreamReader reader_binding)
		{
			this.Reader = reader_binding;
		}

		private int LastChar;

		private int ReadChar()
		{
			do
			{
				this.LastChar = this.Reader.Read();
			}
			while (this.LastChar == '\r');

			return this.LastChar;
		}

		private bool EnclosedCell;

		private string ReadCell()
		{
			StringBuilder buff = new StringBuilder();

			if (this.ReadChar() == '"')
			{
				while (this.ReadChar() != -1 && (this.LastChar != '"' || this.ReadChar() == '"'))
				{
					buff.Append((char)this.LastChar);
				}
				this.EnclosedCell = true;
			}
			else
			{
				while (this.LastChar != -1 && this.LastChar != '\n' && this.LastChar != this.Delimiter)
				{
					buff.Append((char)this.LastChar);
					this.ReadChar();
				}
				this.EnclosedCell = false;
			}
			return buff.ToString();
		}

		public string[] ReadRow()
		{
			List<string> row = new List<string>();

			do
			{
				row.Add(this.ReadCell());
			}
			while (this.LastChar != -1 && this.LastChar != '\n');

			if (this.LastChar == -1 && row.Count == 1 && row[0] == "" && !this.EnclosedCell)
				return null;

			return row.ToArray();
		}

		public string[][] ReadToEnd()
		{
			List<string[]> rows = new List<string[]>();

			for (; ; )
			{
				string[] row = this.ReadRow();

				if (row == null)
					break;

				rows.Add(row);
			}
			return rows.ToArray();
		}

		public void Dispose()
		{
			if (this.Reader != null)
			{
				this.Reader.Dispose();
				this.Reader = null;
			}
		}
	}
}
