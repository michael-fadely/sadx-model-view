using System.IO;

namespace sadx_model_view.Extensions
{
	public static class StreamExtensions
	{
		/// <summary>
		/// Reads a null terminated string from <paramref name="stream"/> into <paramref name="buffer"/>.
		/// </summary>
		/// <param name="stream">The stream to read from.</param>
		/// <param name="buffer">The buffer to output to.</param>
		/// <returns>The length of the string.</returns>
		public static int ReadString(this Stream stream, byte[] buffer)
		{
			int i = 0;

			do
			{
				stream.ReadExact(buffer, i, 1);
			} while (buffer[i++] != 0);

			return i > 0 ? i - 1 : i;
		}

		public static void ReadExact(this Stream stream, byte[] buffer, int offset, int count)
		{
			int amountRead = stream.Read(buffer, offset, count);

			if (amountRead != count)
			{
				throw new EndOfStreamException($"Failed to read desired number of bytes. Expected: {count}; Got: {amountRead}");
			}
		}

		public static void ReadExact(this Stream stream, byte[] buffer) => stream.ReadExact(buffer, offset: 0, count: buffer.Length);
	}
}
