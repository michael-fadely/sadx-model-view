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

		public readonly Modifiable<Matrix>  View           = new Modifiable<Matrix>();
		public readonly Modifiable<Matrix>  Projection     = new Modifiable<Matrix>();
		public readonly Modifiable<Vector3> CameraPosition = new Modifiable<Vector3>();
		public readonly Modifiable<uint>    BufferLength   = new Modifiable<uint>(0);

		// TODO: do this with code generator?
		public void Clear()
		{
			View.Clear();
			Projection.Clear();
			CameraPosition.Clear();
			BufferLength.Clear();
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
		                        wvMatrixInvT.Modified ||
		                        DrawCall.Modified ||
		                        SourceBlend.Modified ||
		                        DestinationBlend.Modified ||
		                        BlendOperation.Modified ||
		                        IsStandardBlending.Modified;

		public readonly Modifiable<Matrix> World              = new Modifiable<Matrix>();
		public readonly Modifiable<Matrix> wvMatrixInvT       = new Modifiable<Matrix>();
		public readonly Modifiable<uint>   DrawCall           = new Modifiable<uint>(0);
		public readonly Modifiable<uint>   SourceBlend        = new Modifiable<uint>(0);
		public readonly Modifiable<uint>   DestinationBlend   = new Modifiable<uint>(0);
		public readonly Modifiable<uint>   BlendOperation     = new Modifiable<uint>(0);
		public readonly Modifiable<bool>   IsStandardBlending = new Modifiable<bool>(true);

		// TODO: do this with code generator?
		public void Clear()
		{
			World.Clear();
			wvMatrixInvT.Clear();
			DrawCall.Clear();
			SourceBlend.Clear();
			DestinationBlend.Clear();
			BlendOperation.Clear();
			IsStandardBlending.Clear();
		}

		/// <inheritdoc />
		public override void Write(CBufferWriter writer)
		{
			writer.Add(World);
			writer.Add(wvMatrixInvT);
			writer.Add(DrawCall);
			writer.Add(SourceBlend);
			writer.Add(DestinationBlend);
			writer.Add(BlendOperation);
			writer.Add(IsStandardBlending);
		}
	}

	public class MaterialBuffer : CBuffer, IModifiable
	{
		public readonly Modifiable<SceneMaterial> Material = new Modifiable<SceneMaterial>();
		public readonly Modifiable<bool> WriteDepth = new Modifiable<bool>(true);

		/// <inheritdoc />
		public override void Write(CBufferWriter writer)
		{
			Material.Value.Write(writer);
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
	}
}