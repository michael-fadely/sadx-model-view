using System;
using SharpDX.Direct3D11;

namespace sadx_model_view
{
	public class DisplayState : IDisposable
	{
		public SamplerState Sampler { get; private set; }
		public RasterizerState Raster { get; private set; }
		public BlendState Blend { get; private set; }

		public DisplayState(SamplerState sampler, RasterizerState raster, BlendState blend)
		{
			Sampler = sampler;
			Raster = raster;
			Blend = blend;
		}

		~DisplayState()
		{
			Dispose();
		}

		public void Dispose()
		{
			Sampler?.Dispose();
			Raster?.Dispose();
			Blend?.Dispose();
		}
	}
}