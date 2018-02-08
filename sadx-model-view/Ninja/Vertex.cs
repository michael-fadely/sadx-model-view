using SharpDX;
using SharpDX.Mathematics.Interop;

namespace sadx_model_view.Ninja
{
	/// <summary>
	/// Custom vertex used for rendering <see cref="NJS_MESHSET"/>.
	/// </summary>
	public struct Vertex
	{
		public static int SizeInBytes => 2 * Vector3.SizeInBytes + sizeof(uint) + Vector2.SizeInBytes;

		public RawVector3    Position;
		public RawVector3    Normal;
		public RawColorBGRA? Diffuse;
		public RawVector2?   UV;
	}
}