namespace sadx_model_view.Ninja
{
	/// <summary>
	/// A structure defining rotation on 3 axes in BAMS.
	/// </summary>
	public struct Rotation3
	{
		/// <summary>
		/// Native structure size in bytes.
		/// </summary>
		public static int SizeInBytes => sizeof(int) * 3;

		public int X, Y, Z;

		public Rotation3(int x, int y, int z)
		{
			X = x;
			Y = y;
			Z = z;
		}

		public static Rotation3 operator +(Rotation3 lhs, Rotation3 rhs)
		{
			return new Rotation3(lhs.X + rhs.X, lhs.Y + rhs.Y, lhs.Z + rhs.Z);
		}

		public static Rotation3 operator -(Rotation3 lhs, Rotation3 rhs)
		{
			return new Rotation3(lhs.X - rhs.X, lhs.Y - rhs.Y, lhs.Z - rhs.Z);
		}
	}
}