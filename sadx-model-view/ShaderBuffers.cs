using sadx_model_view.Extensions.SharpDX;
using sadx_model_view.Interfaces;
using SharpDX;

namespace sadx_model_view
{
	public class PerSceneBuffer : CBuffer, IModifiable
	{
		// TODO: do this with code generator?
		public bool Modified => View.Modified ||
		                        Projection.Modified ||
		                        CameraPosition.Modified ||
		                        BufferLength.Modified;

		public readonly Modifiable<Matrix>  View           = new();
		public readonly Modifiable<Matrix>  Projection     = new();
		public readonly Modifiable<Vector3> CameraPosition = new();
		public readonly Modifiable<uint>    BufferLength   = new(initialValue: 0);

		// TODO: do this with code generator?
		public void Clear()
		{
			View.Clear();
			Projection.Clear();
			CameraPosition.Clear();
			BufferLength.Clear();
		}

		// TODO: do this with code generator?
		public void Mark()
		{
			View.Mark();
			Projection.Mark();
			CameraPosition.Mark();
			BufferLength.Mark();
		}

		/// <inheritdoc />
		public override void Write(CBufferWriter writer)
		{
			writer.Add(View);
			writer.Add(Projection);
			writer.Add(CameraPosition);
			writer.Add(BufferLength);
		}
	}

	public class PerModelBuffer : CBuffer, IModifiable
	{
		// TODO: do this with code generator?
		public bool Modified => World.Modified ||
		                        WvMatrixInvT.Modified ||
		                        DrawCall.Modified ||
		                        SourceBlend.Modified ||
		                        DestinationBlend.Modified ||
		                        BlendOperation.Modified ||
		                        IsStandardBlending.Modified;

		public readonly Modifiable<Matrix> World              = new();
		public readonly Modifiable<Matrix> WvMatrixInvT       = new();
		public readonly Modifiable<uint>   DrawCall           = new(initialValue: 0);
		public readonly Modifiable<uint>   SourceBlend        = new(initialValue: 0);
		public readonly Modifiable<uint>   DestinationBlend   = new(initialValue: 0);
		public readonly Modifiable<uint>   BlendOperation     = new(initialValue: 0);
		public readonly Modifiable<bool>   IsStandardBlending = new(initialValue: true);

		// TODO: do this with code generator?
		public void Clear()
		{
			World.Clear();
			WvMatrixInvT.Clear();
			DrawCall.Clear();
			SourceBlend.Clear();
			DestinationBlend.Clear();
			BlendOperation.Clear();
			IsStandardBlending.Clear();
		}

		// TODO: do this with code generator?
		public void Mark()
		{
			World.Mark();
			WvMatrixInvT.Mark();
			DrawCall.Mark();
			SourceBlend.Mark();
			DestinationBlend.Mark();
			BlendOperation.Mark();
			IsStandardBlending.Mark();
		}

		/// <inheritdoc />
		public override void Write(CBufferWriter writer)
		{
			writer.Add(World);
			writer.Add(WvMatrixInvT);
			writer.Add(DrawCall);
			writer.Add(SourceBlend);
			writer.Add(DestinationBlend);
			writer.Add(BlendOperation);
			writer.Add(IsStandardBlending);
		}
	}

	public class MaterialBuffer : CBuffer, IModifiable
	{
		public readonly Modifiable<SceneMaterial> Material   = new();
		public readonly Modifiable<bool>          WriteDepth = new(initialValue: true);

		/// <inheritdoc />
		public override void Write(CBufferWriter writer)
		{
			Material.ValueReference.Write(writer);
			writer.Align();
			writer.Add(WriteDepth.Value);
		}

		/// <inheritdoc />
		public bool Modified => Material.Modified || WriteDepth.Modified;

		/// <inheritdoc />
		public void Clear()
		{
			Material.Clear();
			WriteDepth.Clear();
		}

		public void Mark()
		{
			Material.Mark();
			WriteDepth.Mark();
		}
	}
}