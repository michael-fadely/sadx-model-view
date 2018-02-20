using SharpDX;

namespace sadx_model_view
{
	public struct DebugWireCube
	{
		public static int SizeInBytes => DebugLine.SizeInBytes * 12;

		public DebugLine[] Lines;

		public DebugWireCube(in BoundingBox box)
		{
			var corners = box.GetCorners();

			Lines = new DebugLine[]
			{
				// first plane
				new DebugLine(new DebugPoint(corners[0], Color4.White), new DebugPoint(corners[1], Color4.White)),
				new DebugLine(new DebugPoint(corners[1], Color4.White), new DebugPoint(corners[2], Color4.White)),
				new DebugLine(new DebugPoint(corners[2], Color4.White), new DebugPoint(corners[3], Color4.White)),
				new DebugLine(new DebugPoint(corners[3], Color4.White), new DebugPoint(corners[0], Color4.White)),

				// second plane
				new DebugLine(new DebugPoint(corners[4], Color4.White), new DebugPoint(corners[5], Color4.White)),
				new DebugLine(new DebugPoint(corners[5], Color4.White), new DebugPoint(corners[6], Color4.White)),
				new DebugLine(new DebugPoint(corners[6], Color4.White), new DebugPoint(corners[7], Color4.White)),
				new DebugLine(new DebugPoint(corners[7], Color4.White), new DebugPoint(corners[4], Color4.White)),

				// last two (four lines)
				new DebugLine(new DebugPoint(corners[0], Color4.White), new DebugPoint(corners[4], Color4.White)),
				new DebugLine(new DebugPoint(corners[1], Color4.White), new DebugPoint(corners[5], Color4.White)),
				new DebugLine(new DebugPoint(corners[2], Color4.White), new DebugPoint(corners[6], Color4.White)),
				new DebugLine(new DebugPoint(corners[3], Color4.White), new DebugPoint(corners[7], Color4.White)),
			};
		}
	}
}
