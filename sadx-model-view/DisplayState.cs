using System;
using SharpDX.Direct3D11;

namespace sadx_model_view
{
	public class DisplayState : IDisposable
	{
		private readonly SamplerState sampler;
		private readonly RasterizerState raster;
		private readonly BlendState blend;

		public DisplayState(SamplerState sampler, RasterizerState raster, BlendState blend)
		{
			this.sampler = sampler;
			this.raster = raster;
			this.blend = blend;
		}

		~DisplayState()
		{
			Dispose();
		}

		public void Dispose()
		{
			sampler?.Dispose();
			raster?.Dispose();
			blend?.Dispose();
		}
	}
}