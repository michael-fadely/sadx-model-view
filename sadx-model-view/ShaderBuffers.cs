using System.Runtime.InteropServices;
using sadx_model_view.Interfaces;
using SharpDX;

namespace sadx_model_view
{
	public class PerSceneBuffer : IModifiable
	{
		// TODO: do this with reflection
		public static int SizeInBytes => (Vector3.SizeInBytes + 2 * Matrix.SizeInBytes)
		                                 + Marshal.SizeOf<uint>()
		                                 + Marshal.SizeOf<bool>();

		// TODO: do this with reflection
		public bool Modified => View.Modified ||
		                        Projection.Modified ||
		                        CameraPosition.Modified ||
		                        BufferLength.Modified ||
		                        IsStandardBlending.Modified;

		public readonly Modifiable<Matrix>  View               = new Modifiable<Matrix>();
		public readonly Modifiable<Matrix>  Projection         = new Modifiable<Matrix>();
		public readonly Modifiable<Vector3> CameraPosition     = new Modifiable<Vector3>();
		public readonly Modifiable<uint>    BufferLength       = new Modifiable<uint>(0);
		public readonly Modifiable<bool>    IsStandardBlending = new Modifiable<bool>(true);

		public void Clear()
		{
			View.Clear();
			Projection.Clear();
			CameraPosition.Clear();
			BufferLength.Clear();
			IsStandardBlending.Clear();
		}
	}

	public class PerModelBuffer : IModifiable
	{
		// TODO: do this with reflection
		public static int SizeInBytes => (2 * Matrix.SizeInBytes) + Marshal.SizeOf<uint>();

		// TODO: do this with reflection
		public bool Modified => World.Modified || wvMatrixInvT.Modified || DrawCall.Modified;

		public readonly Modifiable<Matrix> World        = new Modifiable<Matrix>();
		public readonly Modifiable<Matrix> wvMatrixInvT = new Modifiable<Matrix>();
		public readonly Modifiable<uint>   DrawCall     = new Modifiable<uint>(0);

		public void Clear()
		{
			World.Clear();
			wvMatrixInvT.Clear();
			DrawCall.Clear();
		}
	}
}