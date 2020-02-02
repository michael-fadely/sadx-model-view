using sadx_model_view.Interfaces;
using SharpDX;

namespace sadx_model_view
{
	public class PerSceneBuffer : IModifiable
	{
		public static int SizeInBytes => Vector3.SizeInBytes + 2 * Matrix.SizeInBytes;

		public bool Modified => View.Modified || Projection.Modified || CameraPosition.Modified;

		public readonly Modifiable<Matrix>  View           = new Modifiable<Matrix>();
		public readonly Modifiable<Matrix>  Projection     = new Modifiable<Matrix>();
		public readonly Modifiable<Vector3> CameraPosition = new Modifiable<Vector3>();

		public void Clear()
		{
			View.Clear();
			Projection.Clear();
			CameraPosition.Clear();
		}
	}

	public class PerModelBuffer : IModifiable
	{
		public static int SizeInBytes => 2 * Matrix.SizeInBytes;

		public bool Modified => World.Modified || wvMatrixInvT.Modified;

		public readonly Modifiable<Matrix> World        = new Modifiable<Matrix>();
		public readonly Modifiable<Matrix> wvMatrixInvT = new Modifiable<Matrix>();

		public void Clear()
		{
			World.Clear();
			wvMatrixInvT.Clear();
		}
	}
}