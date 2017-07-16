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
		public static int ReadString(this Stream stream, ref byte[] buffer)
		{
			int i = 0;
			do
			{
				stream.Read(buffer, i, 1);
			} while (buffer[i++] != 0);
			return i > 0 ? i - 1 : i;
		}
	}
}
