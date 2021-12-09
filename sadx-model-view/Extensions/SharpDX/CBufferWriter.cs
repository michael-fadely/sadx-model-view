using System.Runtime.InteropServices;
using SharpDX;

namespace sadx_model_view.Extensions.SharpDX
{
	// TODO: refactor to be C#-like - this is confusing af even in C++, I'mma be real
	public interface ICBuffer
	{
		void Write(CBufferWriter writer);
	}

	public abstract class CBuffer : ICBuffer
	{
		public const int VectorSize = sizeof(float) * 4;

		public abstract void Write(CBufferWriter writer);

		public static uint CalculateSize(ICBuffer buffer)
		{
			var dummy = new CBufferDummy();
			buffer.Write(dummy);
			dummy.Align();
			return dummy.Offset;
		}
	}

	public abstract class CBufferWriter
	{
		public uint Offset    { get; protected set; }
		public uint Alignment { get; protected set; }

		public bool Align(uint size = CBuffer.VectorSize)
		{
			if (Alignment == 0)
			{
				return false;
			}

			uint delta = CBuffer.VectorSize - Alignment;

			if (delta >= size)
			{
				return false;
			}

			Offset += delta;
			Alignment = 0;
			return true;
		}

		public void AddSize(uint size)
		{
			Offset += size;
			Alignment = (Alignment + size) % CBuffer.VectorSize;
		}

		public void Reset()
		{
			Offset    = 0;
			Alignment = 0;
		}

		public abstract void Add<T>(in T data) where T : struct;

		public void Add(bool value)
		{
			Add(value ? 1 : 0);
		}

		public void Add<T>(Modifiable<T> modifiable) where T : struct
		{
			Add(modifiable.Value);
		}
	}

	public class CBufferStreamWriter : CBufferWriter
	{
		private readonly DataStream stream;

		public CBufferStreamWriter(DataStream stream)
		{
			this.stream = stream;
		}

		/// <inheritdoc />
		public override void Add<T>(in T data) where T : struct
		{
			stream.Write(data);
		}
	}

	internal class CBufferDummy : CBufferWriter
	{
		public override void Add<T>(in T data) where T : struct
		{
			int size = Marshal.SizeOf<T>();
			Align((uint)size);
			AddSize((uint)size);
		}
	}
}