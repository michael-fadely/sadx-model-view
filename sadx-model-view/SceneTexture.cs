using System;
using SharpDX.Direct3D11;

namespace sadx_model_view
{
	public class SceneTexture : IDisposable
	{
		public readonly Texture2D          Texture;
		public readonly ShaderResourceView ShaderResource;

		public SceneTexture(Texture2D texture, ShaderResourceView shaderResource)
		{
			Texture        = texture;
			ShaderResource = shaderResource;
		}

		public void Dispose()
		{
			Texture.Dispose();
			ShaderResource.Dispose();
		}
	}
}