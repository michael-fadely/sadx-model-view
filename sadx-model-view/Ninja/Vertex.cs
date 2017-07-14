using SharpDX;
using SharpDX.Mathematics.Interop;

namespace sadx_model_view.Ninja
{
	/// <summary>
	/// Custom vertex used for rendering <see cref="NJS_MESHSET"/>.
	/// </summary>
	public struct Vertex
	{
		//public const VertexFormat Format = VertexFormat.Position | VertexFormat.Normal | VertexFormat.Diffuse | VertexFormat.Texture1;
		public static int SizeInBytes => 2 * Vector3.SizeInBytes + sizeof(uint) + Vector2.SizeInBytes;
		public RawVector3 position;
		public RawVector3 normal;
		public RawColorBGRA? diffuse;
		public RawVector2? uv;
	}
}