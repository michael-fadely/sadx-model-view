using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using sadx_model_view.Ninja;
using SharpDX;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using SharpDX.Mathematics.Interop;
using Buffer = SharpDX.Direct3D11.Buffer;
using Color = SharpDX.Color;
using Device = SharpDX.Direct3D11.Device;
using MapFlags = SharpDX.Direct3D11.MapFlags;
using Resource = SharpDX.Direct3D11.Resource;

namespace sadx_model_view
{
	public class Renderer : IDisposable
	{
		// TODO: not this
		public CullMode CullMode = CullMode.Back;
		// TODO: not this
		public readonly List<Texture2D> TexturePool = new List<Texture2D>();

		private readonly Device device;
		private readonly SwapChain swapChain;
		private RenderTargetView backBuffer;
		private Viewport viewPort;
		private Texture2D depthStencilBuffer;
		private DepthStencilStateDescription depthStencilDesc;
		private DepthStencilState depthStencilState;
		private DepthStencilView depthStencil;
		private RasterizerState renderState;
		private RasterizerStateDescription rasterDesc;

		public Renderer(int w, int h, IntPtr sceneHandle)
		{
			var desc = new SwapChainDescription
			{
				BufferCount     = 1,
				ModeDescription = new ModeDescription(w, h, new Rational(1000, 60), Format.R8G8B8A8_UNorm),
				Usage           = Usage.RenderTargetOutput,
				OutputHandle    = sceneHandle,
				IsWindowed      = true
			};

			var levels = new FeatureLevel[]
			{
				FeatureLevel.Level_11_1,
				FeatureLevel.Level_11_0,
				FeatureLevel.Level_10_1,
				FeatureLevel.Level_10_0,
			};

#if DEBUG
			const DeviceCreationFlags flag = DeviceCreationFlags.Debug;
#else
			const DeviceCreationFlags flag = DeviceCreationFlags.None;
#endif
			Device.CreateWithSwapChain(DriverType.Hardware, flag, levels, desc, out device, out swapChain);

			if (device.FeatureLevel < FeatureLevel.Level_10_0)
			{
				throw new InsufficientFeatureLevelException(device.FeatureLevel, FeatureLevel.Level_10_0);
			}

			RefreshDevice(w, h);
		}

		public void Clear()
		{
			device?.ImmediateContext.ClearRenderTargetView(backBuffer, new RawColor4(1.0f, 1.0f, 1.0f, 1.0f));
			device?.ImmediateContext.ClearDepthStencilView(depthStencil, DepthStencilClearFlags.Depth, 1.0f, 0);
		}

		public void Present()
		{
			// Presents v-sync'd
			swapChain.Present(1, 0);
		}

		private void CreateRenderTarget()
		{
			using (var pBackBuffer = Resource.FromSwapChain<Texture2D>(swapChain, 0))
			{
				backBuffer?.Dispose();
				backBuffer = new RenderTargetView(device, pBackBuffer);
			}

			device.ImmediateContext.OutputMerger.SetRenderTargets(backBuffer);
		}

		private void SetViewPort(int x, int y, int width, int height)
		{
			Viewport vp = viewPort;

			vp.X = x;
			vp.Y = y;
			vp.Width = width;
			vp.Height = height;

			if (vp == viewPort)
			{
				return;
			}

			viewPort = vp;
			device.ImmediateContext.Rasterizer.SetViewport(viewPort);
		}

		public void RefreshDevice(int w, int h)
		{
			backBuffer?.Dispose();
			swapChain?.ResizeBuffers(1, w, h, Format.Unknown, 0);
			SetViewPort(0, 0, w, h);

			CreateDepthStencil(w, h);
			CreateRenderTarget();
			CreateRasterizerState();
		}

		private void CreateDepthStencil(int w, int h)
		{
			var depthBufferDesc = new Texture2DDescription
			{
				Width             = w,
				Height            = h,
				MipLevels         = 1,
				ArraySize         = 1,
				Format            = Format.D24_UNorm_S8_UInt,
				SampleDescription = new SampleDescription(1, 0),
				Usage             = ResourceUsage.Default,
				BindFlags         = BindFlags.DepthStencil | BindFlags.ShaderResource,
				CpuAccessFlags    = CpuAccessFlags.None,
				OptionFlags       = ResourceOptionFlags.None
			};

			depthStencilBuffer?.Dispose();
			depthStencilBuffer = new Texture2D(device, depthBufferDesc);

			depthStencilDesc = new DepthStencilStateDescription
			{
				IsDepthEnabled   = true,
				DepthWriteMask   = DepthWriteMask.All,
				DepthComparison  = Comparison.Less,
				IsStencilEnabled = true,
				StencilReadMask  = 0xFF,
				StencilWriteMask = 0xFF,

				FrontFace = new DepthStencilOperationDescription
				{
					FailOperation      = StencilOperation.Keep,
					DepthFailOperation = StencilOperation.Increment,
					PassOperation      = StencilOperation.Keep,
					Comparison         = Comparison.Always
				},

				BackFace = new DepthStencilOperationDescription
				{
					FailOperation      = StencilOperation.Keep,
					DepthFailOperation = StencilOperation.Decrement,
					PassOperation      = StencilOperation.Keep,
					Comparison         = Comparison.Always
				}
			};

			depthStencilState?.Dispose();
			depthStencilState = new DepthStencilState(device, depthStencilDesc);
			device?.ImmediateContext.OutputMerger.SetDepthStencilState(depthStencilState);

			var depthStencilViewDesc = new DepthStencilViewDescription
			{
				Format    = Format.D24_UNorm_S8_UInt,
				Dimension = DepthStencilViewDimension.Texture2D,
				Texture2D = new DepthStencilViewDescription.Texture2DResource
				{
					MipSlice = 0
				}
			};

			depthStencil?.Dispose();
			depthStencil = new DepthStencilView(device, depthStencilBuffer, depthStencilViewDesc);
			device?.ImmediateContext.OutputMerger.SetTargets(depthStencil, backBuffer);
		}

		private void CreateRasterizerState()
		{
			rasterDesc = new RasterizerStateDescription
			{
				IsAntialiasedLineEnabled = false,
				CullMode                 = CullMode,
				DepthBias                = 0,
				DepthBiasClamp           = 0.0f,
				IsDepthClipEnabled       = true,
				FillMode                 = FillMode.Solid,
				IsFrontCounterClockwise  = false,
				IsMultisampleEnabled     = false,
				IsScissorEnabled         = false,
				SlopeScaledDepthBias     = 0.0f
			};

			renderState?.Dispose();
			renderState = new RasterizerState(device, rasterDesc);

			device.ImmediateContext.Rasterizer.State = renderState;
		}

		public void CopyToTexture(Texture2D texture, Bitmap bitmap, int level)
		{
			device.ImmediateContext.MapSubresource(texture, level, MapMode.WriteDiscard, MapFlags.None, out DataStream data);

			BitmapData bmpData = bitmap.LockBits(new System.Drawing.Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);

			var buffer = new byte[bmpData.Stride * bitmap.Height];
			Marshal.Copy(bmpData.Scan0, buffer, 0, buffer.Length);

			bitmap.UnlockBits(bmpData);

			using (var stream = new MemoryStream(buffer))
			{
				while (stream.Position != stream.Length)
				{
					for (int i = 0; i < 4; i++)
					{
						data.WriteByte((byte)stream.ReadByte());
					}
				}
			}

			if (data.RemainingLength > 0)
			{
				throw new ArgumentOutOfRangeException();
			}

			data.Close();
			device.ImmediateContext.UnmapSubresource(texture, level);
		}

		public void CreateTextureFromBitMap(Bitmap bitmap, Bitmap[] mipmaps, int levels)
		{
			var texDesc = new Texture2DDescription
			{
				ArraySize = 1,
				BindFlags = BindFlags.ShaderResource,
				CpuAccessFlags = CpuAccessFlags.Write,
				Format = Format.R8G8B8A8_UNorm,
				Width = bitmap.Width,
				Height = bitmap.Height,
				MipLevels = levels,
				Usage = ResourceUsage.Dynamic,
				SampleDescription = new SampleDescription(1, 0)
			};

			var texture = new Texture2D(device, texDesc);

			if (mipmaps?.Length > 0)
			{
				for (int i = 0; i < levels; i++)
				{
					CopyToTexture(texture, mipmaps[i], i);
				}

			}
			else
			{
				CopyToTexture(texture, bitmap, 0);
			}

			TexturePool.Add(texture);
		}

		public void ClearTexturePool()
		{
			foreach (Texture2D texture in TexturePool)
			{
				texture.Dispose();
			}

			TexturePool.Clear();
		}

		public Buffer CreateVertexBuffer(IReadOnlyCollection<Vertex> vertices)
		{
			int vertexSize = vertices.Count * Vertex.SizeInBytes;

			var result = new Buffer(device, new BufferDescription(vertexSize, BindFlags.VertexBuffer, ResourceUsage.Dynamic));

			device.ImmediateContext.MapSubresource(result, MapMode.WriteDiscard, MapFlags.None, out DataStream stream);

			using (stream)
			{
				foreach (Vertex v in vertices)
				{
					stream.Write(v.position.X);
					stream.Write(v.position.Y);
					stream.Write(v.position.Z);

					stream.Write(v.normal.X);
					stream.Write(v.normal.Y);
					stream.Write(v.normal.Z);

					RawColorBGRA color = v.diffuse == null ? Color.White : v.diffuse.Value;

					stream.Write(color.R);
					stream.Write(color.G);
					stream.Write(color.B);
					stream.Write(color.A);

					RawVector2 uv = v.uv == null ? (RawVector2)Vector2.Zero : v.uv.Value;

					stream.Write(uv.X);
					stream.Write(uv.Y);
				}

				if (stream.RemainingLength != 0)
				{
					throw new Exception("Failed to fill vertex buffer.");
				}
			}

			device.ImmediateContext.UnmapSubresource(result, 0);
			return result;
		}

		public Buffer CreateIndexBuffer(IEnumerable<short> indices, int sizeInBytes)
		{
			var result = new Buffer(device, new BufferDescription(sizeInBytes, BindFlags.IndexBuffer, ResourceUsage.Dynamic));
			device.ImmediateContext.MapSubresource(result, MapMode.WriteDiscard, MapFlags.None, out DataStream stream);

			using (stream)
			{
				foreach (short i in indices)
				{
					stream.Write(i);
				}

				if (stream.RemainingLength != 0)
				{
					throw new Exception("Failed to fill index buffer.");
				}
			}

			device.ImmediateContext.UnmapSubresource(result, 0);
			return result;
		}

		public void Dispose()
		{
			device?.Dispose();
			swapChain?.Dispose();
			backBuffer?.Dispose();
			depthStencilBuffer?.Dispose();
			depthStencilState?.Dispose();
			depthStencil?.Dispose();
			renderState?.Dispose();
		}

		public void SetTransform(TransformState state, ref RawMatrix rawMatrix)
		{
			throw new NotImplementedException();
		}

		public void SetTexture(int sampler, int textureIndex)
		{
			if (textureIndex >= 0 && textureIndex < TexturePool.Count)
			{
				Texture2D texture = TexturePool[textureIndex];
				// TODO: set texture
			}
			else
			{
				// TODO: set null texture
			}

			throw new NotImplementedException();
		}
	}

	internal class InsufficientFeatureLevelException : Exception
	{
		public readonly FeatureLevel SupportedLevel;
		public readonly FeatureLevel TargetLevel;

		public InsufficientFeatureLevelException(FeatureLevel supported, FeatureLevel target)
		{
			SupportedLevel = supported;
			TargetLevel = target;
		}
	}
}
