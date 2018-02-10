namespace sadx_model_view
{
	public struct DebugLine
	{
		public DebugPoint PointA, PointB;

		public DebugLine(DebugPoint a, DebugPoint b)
		{
			PointA = a;
			PointB = b;
		}
	}
}
