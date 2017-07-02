using System;
using System.IO;
using SharpDX.D3DCompiler;

namespace sadx_model_view
{
	internal class DefaultIncludeHandler : Include
	{
		private Stream fileStream;

		public IDisposable Shadow { get; set; }
		public void Dispose()
		{
			fileStream?.Dispose();
		}

		public Stream Open(IncludeType type, string fileName, Stream parentStream)
		{
			fileStream = File.Open(Path.Combine("Shaders", fileName), FileMode.Open);
			return fileStream;
		}

		public void Close(Stream stream)
		{
			fileStream?.Close();
		}
	}
}
