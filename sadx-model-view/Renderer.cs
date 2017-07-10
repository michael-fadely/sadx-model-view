using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using sadx_model_view.Ninja;
using SharpDX;
using SharpDX.D3DCompiler;
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
	public struct MatrixBuffer
	{
		public Matrix World;
		public Matrix View;
		public Matrix Projection;
		public Matrix Texture;

		public override bool Equals(object obj)
		{
			return base.Equals(obj);
		}

		public bool Equals(MatrixBuffer other)
		{
			return World.Equals(other.World)
				&& View.Equals(other.View)
				&& Projection.Equals(other.Projection)
				&& Texture.Equals(other.Texture);
		}

		public override int GetHashCode() => 1;

		public static bool operator ==(MatrixBuffer lhs, MatrixBuffer rhs)
		{
			return lhs.Equals(rhs);
		}

		public static bool operator !=(MatrixBuffer lhs, MatrixBuffer rhs)
		{
			return !(lhs == rhs);
		}
	}

	public class Renderer : IDisposable
	{
		public CullMode DefaultCullMode = CullMode.None;
		private readonly List<SceneTexture> texturePool = new List<SceneTexture>();

		private readonly Device device;
		private readonly SwapChain swapChain;
		private RenderTargetView backBuffer;
		private Viewport viewPort;
		private Texture2D depthTexture;
		private DepthStencilStateDescription depthDesc;
		private DepthStencilState depthStateRW;
		private DepthStencilState depthStateRO;
		private DepthStencilView depthView;
		private RasterizerState rasterizerState;
		private RasterizerStateDescription rasterizerDescription;
		private readonly Buffer matrixBuffer;
		private readonly Buffer materialBuffer;

		private bool matrixDataChanged;
		private MatrixBuffer lastMatrixData;
		private MatrixBuffer matrixData;

		public Renderer(int w, int h, IntPtr sceneHandle)
		{
			var desc = new SwapChainDescription
			{
				BufferCount       = 1,
				ModeDescription   = new ModeDescription(w, h, new Rational(1000, 60), Format.R8G8B8A8_UNorm),
				Usage             = Usage.RenderTargetOutput,
				OutputHandle      = sceneHandle,
				IsWindowed        = true,
				SampleDescription = new SampleDescription(1, 0)
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

			int mtx_size = Matrix.SizeInBytes * 4;
			var bufferDesc = new BufferDescription(mtx_size,
				ResourceUsage.Dynamic, BindFlags.ConstantBuffer, CpuAccessFlags.Write, ResourceOptionFlags.None, mtx_size);

			matrixBuffer = new Buffer(device, bufferDesc);

			// Size must be divisible by 16, so this is just padding.
			int size = Vector4.SizeInBytes * 3;
			int stride = Vector4.SizeInBytes * 2 + sizeof(float);

			bufferDesc = new BufferDescription(size,
				ResourceUsage.Dynamic, BindFlags.ConstantBuffer, CpuAccessFlags.Write, ResourceOptionFlags.None, stride);

			materialBuffer = new Buffer(device, bufferDesc);

			LoadShaders();

			device.ImmediateContext.VertexShader.SetConstantBuffer(0, matrixBuffer);
			device.ImmediateContext.VertexShader.SetConstantBuffer(1, materialBuffer);
			device.ImmediateContext.PixelShader.SetConstantBuffer(1, materialBuffer);

			RefreshDevice(w, h);
		}

		private void LoadShaders()
		{
			using (var includeMan = new DefaultIncludeHandler())
			{
				vertexShader?.Dispose();
				pixelShader?.Dispose();
				inputLayout?.Dispose();

				CompilationResult vs_result = ShaderBytecode.CompileFromFile("Shaders\\scene_vs.hlsl", "main", "vs_4_0", include: includeMan);

				if (vs_result.HasErrors || !string.IsNullOrEmpty(vs_result.Message))
				{
					throw new Exception(vs_result.Message);
				}

				vertexShader = new VertexShader(device, vs_result.Bytecode);

				CompilationResult ps_result = ShaderBytecode.CompileFromFile("Shaders\\scene_ps.hlsl", "main", "ps_4_0", include: includeMan);

				if (ps_result.HasErrors || !string.IsNullOrEmpty(ps_result.Message))
				{
					throw new Exception(ps_result.Message);
				}

				pixelShader = new PixelShader(device, ps_result.Bytecode);

				var layout = new InputElement[]
				{
					new InputElement("POSITION", 0, Format.R32G32B32_Float, InputElement.AppendAligned, 0, InputClassification.PerVertexData, 0),
					new InputElement("NORMAL",   0, Format.R32G32B32_Float, InputElement.AppendAligned, 0, InputClassification.PerVertexData, 0),
					new InputElement("COLOR",    0, Format.R8G8B8A8_UNorm,  InputElement.AppendAligned, 0, InputClassification.PerVertexData, 0),
					new InputElement("TEXCOORD", 0, Format.R32G32_Float,    InputElement.AppendAligned, 0, InputClassification.PerVertexData, 0)
				};

				inputLayout = new InputLayout(device, vs_result.Bytecode, layout);

				device.ImmediateContext.VertexShader.Set(vertexShader);
				device.ImmediateContext.PixelShader.Set(pixelShader);
				device.ImmediateContext.InputAssembler.InputLayout = inputLayout;
			}
		}

		public void Clear()
		{
			if (device == null)
			{
				return;
			}

			device.ImmediateContext.Rasterizer.State = rasterizerState;
			device.ImmediateContext.ClearRenderTargetView(backBuffer, new RawColor4(0.0f, 1.0f, 1.0f, 1.0f));
			device.ImmediateContext.ClearDepthStencilView(depthView, DepthStencilClearFlags.Depth, 1.0f, 0);
		}

		public void Draw(DisplayState state, Buffer vertexBuffer, Buffer indexBuffer, int indexCount)
		{
			if (device == null)
			{
				return;
			}

			if (state.Blend.Description.RenderTarget[0].IsBlendEnabled)
			{
				device.ImmediateContext.OutputMerger.SetDepthStencilState(depthStateRO);
			}
			else
			{
				device.ImmediateContext.OutputMerger.SetDepthStencilState(depthStateRW);
			}

			device.ImmediateContext.PixelShader.SetSampler(0, state.Sampler);
			device.ImmediateContext.Rasterizer.State = state.Raster;
			device.ImmediateContext.OutputMerger.SetBlendState(state.Blend);

			if (matrixDataChanged && lastMatrixData != matrixData)
			{
				device.ImmediateContext.MapSubresource(matrixBuffer, MapMode.WriteDiscard, MapFlags.None, out DataStream stream);
				using (stream)
				{
					Matrix m;
					Matrix.Transpose(ref matrixData.World, out m);
					stream.Write(m);

					Matrix.Transpose(ref matrixData.View, out m);
					stream.Write(m);

					Matrix.Transpose(ref matrixData.Projection, out m);
					stream.Write(m);

					Matrix.Transpose(ref matrixData.Texture, out m);
					stream.Write(m);
				}
				device.ImmediateContext.UnmapSubresource(matrixBuffer, 0);

				lastMatrixData = matrixData;
			}

			device.ImmediateContext.InputAssembler.PrimitiveTopology = PrimitiveTopology.TriangleList;
			device.ImmediateContext.InputAssembler.SetVertexBuffers(0, new Buffer[] { vertexBuffer }, new []{ Vertex.SizeInBytes }, new []{ 0 });
			device.ImmediateContext.InputAssembler.SetIndexBuffer(indexBuffer, Format.R16_UInt, 0);
			device.ImmediateContext.DrawIndexed(indexCount, 0, 0);
		}

		public void Present()
		{
			swapChain.Present(0, 0);
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
			viewPort.MinDepth = 0f;
			viewPort.MaxDepth = 1f;

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

			CreateRenderTarget();
			CreateRasterizerState();
			CreateDepthStencil(w, h);
		}

		private void CreateDepthStencil(int w, int h)
		{
			// TODO: shader resource?

			var depthBufferDesc = new Texture2DDescription
			{
				Width             = w,
				Height            = h,
				MipLevels         = 1,
				ArraySize         = 1,
				Format            = Format.D24_UNorm_S8_UInt,
				SampleDescription = new SampleDescription(1, 0),
				Usage             = ResourceUsage.Default,
				BindFlags         = BindFlags.DepthStencil,
				CpuAccessFlags    = CpuAccessFlags.None,
				OptionFlags       = ResourceOptionFlags.None
			};

			depthTexture?.Dispose();
			depthTexture = new Texture2D(device, depthBufferDesc);

			depthDesc = new DepthStencilStateDescription
			{
				IsDepthEnabled   = true,
				DepthWriteMask   = DepthWriteMask.All,
				DepthComparison  = Comparison.Less,
				
				FrontFace = new DepthStencilOperationDescription
				{
					FailOperation      = StencilOperation.Keep,
					DepthFailOperation = StencilOperation.Increment,
					PassOperation      = StencilOperation.Keep,
					Comparison         = Comparison.Never
				},

				BackFace = new DepthStencilOperationDescription
				{
					FailOperation      = StencilOperation.Keep,
					DepthFailOperation = StencilOperation.Decrement,
					PassOperation      = StencilOperation.Keep,
					Comparison         = Comparison.Never
				}
			};

			depthStateRW?.Dispose();
			depthStateRW = new DepthStencilState(device, depthDesc);
			
			depthDesc.DepthWriteMask = DepthWriteMask.Zero;
			depthStateRO?.Dispose();
			depthStateRO = new DepthStencilState(device, depthDesc);

			var depthViewDesc = new DepthStencilViewDescription
			{
				Format    = Format.D24_UNorm_S8_UInt,
				Dimension = DepthStencilViewDimension.Texture2D,
				Texture2D = new DepthStencilViewDescription.Texture2DResource
				{
					MipSlice = 0
				}
			};

			depthView?.Dispose();
			depthView = new DepthStencilView(device, depthTexture, depthViewDesc);

			device?.ImmediateContext.OutputMerger.SetTargets(depthView, backBuffer);
			device?.ImmediateContext.OutputMerger.SetDepthStencilState(depthStateRW);
		}

		private void CreateRasterizerState()
		{
			rasterizerDescription = new RasterizerStateDescription
			{
				IsAntialiasedLineEnabled = false,
				CullMode                 = DefaultCullMode,
				DepthBias                = 0,
				DepthBiasClamp           = 0.0f,
				IsDepthClipEnabled       = true,
				FillMode                 = FillMode.Solid,
				IsFrontCounterClockwise  = false,
				IsMultisampleEnabled     = false,
				IsScissorEnabled         = false,
				SlopeScaledDepthBias     = 0.0f
			};

			rasterizerState?.Dispose();
			rasterizerState = new RasterizerState(device, rasterizerDescription);

			device.ImmediateContext.Rasterizer.State = rasterizerState;
		}

		private void CopyToTexture(Texture2D texture, Bitmap bitmap, int level)
		{
			// Copy the bitmap into system memory
			BitmapData bmpData = bitmap.LockBits(new System.Drawing.Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
			var buffer = new byte[bmpData.Stride * bitmap.Height];
			Marshal.Copy(bmpData.Scan0, buffer, 0, buffer.Length);
			bitmap.UnlockBits(bmpData);

			// Update the texture sub-resource (where 0 is full size and [n] is mipmap)
			device.ImmediateContext.UpdateSubresource(buffer, texture, level, 4 * bitmap.Width, 4 * bitmap.Height);
		}

		public void CreateTextureFromBitMap(Bitmap bitmap, Bitmap[] mipmaps, int levels)
		{
			var texDesc = new Texture2DDescription
			{
				ArraySize         = 1,
				BindFlags         = BindFlags.ShaderResource,
				CpuAccessFlags    = CpuAccessFlags.Write,
				Format            = Format.B8G8R8A8_UNorm,
				Width             = bitmap.Width,
				Height            = bitmap.Height,
				MipLevels         = levels,
				Usage             = ResourceUsage.Default,
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

			var pair = new SceneTexture(texture, new ShaderResourceView(device, texture));
			texturePool.Add(pair);
		}

		public void ClearTexturePool()
		{
			foreach (SceneTexture texture in texturePool)
			{
				texture.Dispose();
			}

			texturePool.Clear();
		}

		public Buffer CreateVertexBuffer(IReadOnlyCollection<Vertex> vertices)
		{
			int vertexSize = vertices.Count * Vertex.SizeInBytes;

			var desc = new BufferDescription(vertexSize, BindFlags.VertexBuffer, ResourceUsage.Dynamic)
			{
				CpuAccessFlags = CpuAccessFlags.Write
			};

			var result = new Buffer(device, desc);

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

					stream.Write(color.B);
					stream.Write(color.G);
					stream.Write(color.R);
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
			var desc = new BufferDescription(sizeInBytes, BindFlags.IndexBuffer, ResourceUsage.Dynamic)
			{
				CpuAccessFlags = CpuAccessFlags.Write
			};

			var result = new Buffer(device, desc);
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
			depthTexture?.Dispose();
			depthStateRW?.Dispose();
			depthStateRO?.Dispose();
			depthView?.Dispose();
			rasterizerState?.Dispose();
			matrixBuffer?.Dispose();
			materialBuffer?.Dispose();
			vertexShader?.Dispose();
			pixelShader?.Dispose();
			inputLayout?.Dispose();
		}

		public void SetTransform(TransformState state, ref RawMatrix rawMatrix)
		{
			switch (state)
			{
				case TransformState.World:
					matrixData.World = rawMatrix;
					break;
				case TransformState.View:
					matrixData.View = rawMatrix;
					break;
				case TransformState.Projection:
					matrixData.Projection = rawMatrix;
					break;
				case TransformState.Texture:
					matrixData.Texture = rawMatrix;
					break;
				default:
					throw new ArgumentOutOfRangeException(nameof(state), state, null);
			}

			matrixDataChanged = true;
		}

		public void SetTexture(int sampler, int textureIndex)
		{
			if (textureIndex >= 0 && textureIndex < texturePool.Count)
			{
				SceneTexture texture = texturePool[textureIndex];
				device.ImmediateContext.PixelShader.SetShaderResource(sampler, texture.ShaderResource);
			}
			else
			{
				device.ImmediateContext.PixelShader.SetShaderResource(sampler, null);
			}
		}

		// TODO: renderer interface to handle SADX/SA1/SA2 renderers

		private static readonly BlendOption[] blendModes =
		{
			BlendOption.Zero,
			BlendOption.One,
			BlendOption.SourceColor,
			BlendOption.InverseSourceColor,
			BlendOption.SourceAlpha,
			BlendOption.InverseSourceAlpha,
			BlendOption.DestinationAlpha,
			BlendOption.InverseDestinationAlpha,
		};

		public DisplayState CreateSADXDisplayState(NJS_MATERIAL material)
		{
			// TODO: communicate NJD_FLAG.UseAlpha to the shader as this determines if vertex or material diffuse is used.
			// TODO: [shader] specular
			// TODO: [shader] ignore light

			// Not implemented in SADX:
			// - NJD_FLAG.Pick
			// - NJD_FLAG.UseAnisotropic
			// - NJD_FLAG.UseFlat

			NJD_FLAG flags = material.attrflags;

			var samplerDesc = new SamplerStateDescription
			{
				AddressW          = TextureAddressMode.Wrap,
				Filter            = Filter.MinMagMipLinear,
				MinimumLod        = -float.MaxValue,
				MaximumLod        = float.MaxValue,
				MaximumAnisotropy = 1
			};

			// TODO: fix clamp

			/*if ((flags & NJD_FLAG.ClampU) != 0)
			{
				samplerDesc.AddressU = TextureAddressMode.Clamp;
			}
			else*/ if ((flags & NJD_FLAG.FlipU) != 0)
			{
				samplerDesc.AddressU = TextureAddressMode.Mirror;
			}
			else
			{
				samplerDesc.AddressU = TextureAddressMode.Wrap;
			}

			/*if ((flags & NJD_FLAG.ClampV) != 0)
			{
				samplerDesc.AddressV = TextureAddressMode.Clamp;
			}
			else*/ if ((flags & NJD_FLAG.FlipV) != 0)
			{
				samplerDesc.AddressV = TextureAddressMode.Mirror;
			}
			else
			{
				samplerDesc.AddressV = TextureAddressMode.Wrap;
			}

			var sampler = new SamplerState(device, samplerDesc);

			// Base it off of the default rasterizer state.
			RasterizerStateDescription rasterDesc = rasterizerDescription;

			rasterDesc.CullMode = (flags & NJD_FLAG.DoubleSide) != 0 ? CullMode.None : DefaultCullMode;

			var raster = new RasterizerState(device, rasterDesc);

			var blendDesc = new BlendStateDescription();
			ref RenderTargetBlendDescription rt = ref blendDesc.RenderTarget[0];

			rt.IsBlendEnabled   = (material.attrflags & NJD_FLAG.UseAlpha) != 0;
			rt.SourceBlend      = blendModes[material.SourceBlend];
			rt.DestinationBlend = blendModes[material.DestinationBlend];
			rt.BlendOperation   = BlendOperation.Add;

			// crap
			rt.SourceAlphaBlend      = BlendOption.One;
			rt.DestinationAlphaBlend = BlendOption.Zero;
			rt.AlphaBlendOperation   = BlendOperation.Add;
			rt.RenderTargetWriteMask = ColorWriteMaskFlags.All;

			var blend = new BlendState(device, blendDesc);

			return new DisplayState(sampler, raster, blend);
		}

		private ShaderMaterial lastMaterial;
		private VertexShader vertexShader;
		private PixelShader pixelShader;
		private InputLayout inputLayout;

		public void SetShaderMaterial(ref ShaderMaterial material)
		{
			if (material == lastMaterial)
			{
				return;
			}

			lastMaterial = material;

			device.ImmediateContext.MapSubresource(materialBuffer, MapMode.WriteDiscard, MapFlags.None, out DataStream stream);
			using (stream)
			{
				stream.Write(material.Diffuse);
				stream.Write(material.Specular);
				stream.Write(material.Exponent);
			}
			device.ImmediateContext.UnmapSubresource(materialBuffer, 0);
		}
	}

	public struct ShaderMaterial
	{
		public RawColor4 Diffuse;
		public RawColor4 Specular;
		public float Exponent;

		public override bool Equals(object obj)
		{
			return base.Equals(obj);
		}

		public bool Equals(ShaderMaterial other)
		{
			return Diffuse.Equals(other.Diffuse) && Specular.Equals(other.Specular) && Exponent.Equals(other.Exponent);
		}

		public static bool operator==(ShaderMaterial lhs, ShaderMaterial rhs)
		{
			return lhs.Equals(rhs);
		}

		public static bool operator !=(ShaderMaterial lhs, ShaderMaterial rhs)
		{
			return !(lhs == rhs);
		}

		public override int GetHashCode() => 1;
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
