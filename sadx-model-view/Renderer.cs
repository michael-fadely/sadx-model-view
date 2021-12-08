using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.InteropServices;
using sadx_model_view.Extensions;
using sadx_model_view.Extensions.SharpDX;
using sadx_model_view.Ninja;
using SharpDX;
using SharpDX.D3DCompiler;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using SharpDX.Mathematics.Interop;
using SharpDX.WIC;
using Bitmap = System.Drawing.Bitmap;
using Buffer = SharpDX.Direct3D11.Buffer;
using Color = SharpDX.Color;
using Device = SharpDX.Direct3D11.Device;
using MapFlags = SharpDX.Direct3D11.MapFlags;
using PixelFormat = System.Drawing.Imaging.PixelFormat;
using Resource = SharpDX.Direct3D11.Resource;

namespace sadx_model_view
{
	// TODO: renderer interface to handle SADX/SA1/SA2 renderers

	public class Renderer : IDisposable
	{
		static readonly NJS_MATERIAL nullMaterial = new NJS_MATERIAL();

		static readonly BlendOption[] blendModes =
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

		int lastVisibleCount;

		public FlowControl FlowControl;
		public bool EnableAlpha = true;

		CullMode defaultCullMode = CullMode.None;

		public CullMode DefaultCullMode
		{
			get => defaultCullMode;
			set
			{
				defaultCullMode = value;
				ClearDisplayStates();
			}
		}

		readonly List<SceneTexture> texturePool = new List<SceneTexture>();

		readonly Device              device;
		readonly SwapChain           swapChain;
		RenderTargetView             backBuffer;
		Viewport                     viewport;
		Texture2D                    depthTexture;
		DepthStencilStateDescription depthDesc;
		DepthStencilState            depthStateRW;
		DepthStencilState            depthStateRO;
		DepthStencilView             depthView;
		ShaderResourceView           depthShaderResource;
		RasterizerState              rasterizerState;
		RasterizerStateDescription   rasterizerDescription;

		Texture2D          compositeTexture;
		RenderTargetView   compositeView;
		ShaderResourceView compositeSRV;

		readonly Buffer debugHelperVertexBuffer;
		readonly Buffer perSceneBuffer;
		readonly Buffer perModelBuffer;
		readonly Buffer materialBuffer;

		readonly PerSceneBuffer perSceneData = new PerSceneBuffer();
		readonly PerModelBuffer perModelData = new PerModelBuffer();
		readonly MaterialBuffer materialData = new MaterialBuffer();

		readonly MeshsetQueue meshQueue = new MeshsetQueue();
		readonly Dictionary<NJD_FLAG, DisplayState> displayStates = new Dictionary<NJD_FLAG, DisplayState>();

		readonly List<DebugLine>     debugLines     = new List<DebugLine>();
		readonly List<DebugWireCube> debugWireCubes = new List<DebugWireCube>();

		VertexShader   sceneVertexShader;
		PixelShader    scenePixelShader;
		InputLayout    sceneInputLayout;

		VertexShader oitCompositeVertexShader;
		PixelShader  oitCompositePixelShader;

		VertexShader debugVertexShader;
		PixelShader  debugPixelShader;
		InputLayout  debugInputLayout;

		Buffer          lastVertexBuffer;
		BlendState      lastBlend;
		RasterizerState lastRasterizerState;
		SamplerState    lastSamplerState;
		SceneTexture    lastTexture;

		public bool OITCapable { get; private set; }

		private bool _oitEnabled;

		public bool OITEnabled
		{
			get => _oitEnabled;
			set
			{
				if (value && !OITCapable)
				{
					throw new Exception("Device not OIT-capable!");
				}

				if (value == _oitEnabled)
				{
					return;
				}

				_oitEnabled = value;
				LoadShaders();
			}
		}

		public Renderer(int w, int h, IntPtr sceneHandle)
		{
			FlowControl.Reset();

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

			OITCapable = device.FeatureLevel >= FeatureLevel.Level_11_0;

			int bufferSize = (int)CBuffer.CalculateSize(perSceneData);

			var bufferDesc = new BufferDescription(bufferSize,
			                                       ResourceUsage.Dynamic, BindFlags.ConstantBuffer, CpuAccessFlags.Write,
			                                       ResourceOptionFlags.None, bufferSize);

			perSceneBuffer = new Buffer(device, bufferDesc);

			bufferSize = (int)CBuffer.CalculateSize(perModelData);

			bufferDesc = new BufferDescription(bufferSize,
			                                   ResourceUsage.Dynamic, BindFlags.ConstantBuffer, CpuAccessFlags.Write,
			                                   ResourceOptionFlags.None, bufferSize);

			perModelBuffer = new Buffer(device, bufferDesc);

			int size = (int)CBuffer.CalculateSize(materialData);

			bufferDesc = new BufferDescription(size, ResourceUsage.Dynamic, BindFlags.ConstantBuffer,
			                                   CpuAccessFlags.Write, ResourceOptionFlags.None, structureByteStride: size);

			materialBuffer = new Buffer(device, bufferDesc);

			RefreshDevice(w, h);

			LoadShaders();

			device.ImmediateContext.VertexShader.SetConstantBuffer(0, perSceneBuffer);
			device.ImmediateContext.PixelShader.SetConstantBuffer(0, perSceneBuffer);

			device.ImmediateContext.VertexShader.SetConstantBuffer(1, materialBuffer);
			device.ImmediateContext.PixelShader.SetConstantBuffer(1, materialBuffer);

			device.ImmediateContext.VertexShader.SetConstantBuffer(2, perModelBuffer);
			device.ImmediateContext.PixelShader.SetConstantBuffer(2, perModelBuffer);

			int helperBufferSize = DebugWireCube.SizeInBytes.AlignUp(16);

			var debugHelperDescription = new BufferDescription(helperBufferSize, BindFlags.VertexBuffer, ResourceUsage.Dynamic)
			{
				CpuAccessFlags      = CpuAccessFlags.Write,
				StructureByteStride = DebugPoint.SizeInBytes
			};

			debugHelperVertexBuffer = new Buffer(device, debugHelperDescription);

		}

		void SetShaderToScene()
		{
			device.ImmediateContext.VertexShader.Set(sceneVertexShader);
			device.ImmediateContext.PixelShader.Set(scenePixelShader);
			device.ImmediateContext.InputAssembler.InputLayout = sceneInputLayout;

			device.ImmediateContext.InputAssembler.PrimitiveTopology = PrimitiveTopology.TriangleList;
		}

		void SetShaderToOITComposite()
		{
			if (!OITCapable)
			{
				return;
			}

			device.ImmediateContext.VertexShader.Set(oitCompositeVertexShader);
			device.ImmediateContext.PixelShader.Set(oitCompositePixelShader);

			device.ImmediateContext.InputAssembler.InputLayout = null;
			device.ImmediateContext.InputAssembler.SetIndexBuffer(null, Format.Unknown, 0);
			device.ImmediateContext.InputAssembler.SetVertexBuffers(0, 0, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);

			lastVertexBuffer = null;
		}

		void SetShaderToDebug()
		{
			device.ImmediateContext.VertexShader.Set(debugVertexShader);
			device.ImmediateContext.PixelShader.Set(debugPixelShader);
			device.ImmediateContext.InputAssembler.InputLayout = debugInputLayout;

			var binding = new VertexBufferBinding(debugHelperVertexBuffer, DebugPoint.SizeInBytes, 0);

			device.ImmediateContext.InputAssembler.SetVertexBuffers(0, binding);
			device.ImmediateContext.InputAssembler.PrimitiveTopology = PrimitiveTopology.LineList;
		}

		void LoadOITCompositeShader()
		{
			CoreExtensions.DisposeAndNullify(ref oitCompositeVertexShader);
			CoreExtensions.DisposeAndNullify(ref oitCompositePixelShader);

			if (!OITCapable || !_oitEnabled)
			{
				return;
			}

			var macros = new ShaderMacro[]
			{
				new ShaderMacro("RS_OIT", (OITCapable && _oitEnabled) ? "1" : "0")
			};

			using var includeMan = new DefaultIncludeHandler();

			CompilationResult vs_result = ShaderBytecode.CompileFromFile("Shaders\\oit_composite.hlsl", "vs_main", "vs_5_0", include: includeMan, defines: macros);

			if (vs_result.HasErrors || !string.IsNullOrEmpty(vs_result.Message))
			{
				throw new Exception(vs_result.Message);
			}

			oitCompositeVertexShader = new VertexShader(device, vs_result.Bytecode);

			CompilationResult ps_result = ShaderBytecode.CompileFromFile("Shaders\\oit_composite.hlsl", "ps_main", "ps_5_0", include: includeMan, defines: macros);

			if (ps_result.HasErrors || !string.IsNullOrEmpty(ps_result.Message))
			{
				throw new Exception(ps_result.Message);
			}

			oitCompositePixelShader = new PixelShader(device, ps_result.Bytecode);
		}

		void LoadSceneShaders()
		{
			using var includeMan = new DefaultIncludeHandler();

			sceneVertexShader?.Dispose();
			scenePixelShader?.Dispose();
			sceneInputLayout?.Dispose();

			var macros = new ShaderMacro[]
			{
				new ShaderMacro("RS_OIT", (OITCapable && _oitEnabled) ? "1" : "0")
			};

			CompilationResult vs_result = ShaderBytecode.CompileFromFile("Shaders\\scene_vs.hlsl", "main", "vs_5_0",
			                                                             include: includeMan, defines: macros);

			if (vs_result.HasErrors || !string.IsNullOrEmpty(vs_result.Message))
			{
				throw new Exception(vs_result.Message);
			}

			sceneVertexShader = new VertexShader(device, vs_result.Bytecode);

			CompilationResult ps_result = ShaderBytecode.CompileFromFile("Shaders\\scene_ps.hlsl", "main", "ps_5_0",
			                                                             include: includeMan, defines: macros);

			if (ps_result.HasErrors || !string.IsNullOrEmpty(ps_result.Message))
			{
				throw new Exception(ps_result.Message);
			}

			scenePixelShader = new PixelShader(device, ps_result.Bytecode);

			var layout = new InputElement[]
			{
				new InputElement("POSITION", 0, Format.R32G32B32_Float, InputElement.AppendAligned, 0, InputClassification.PerVertexData, 0),
				new InputElement("NORMAL",   0, Format.R32G32B32_Float, InputElement.AppendAligned, 0, InputClassification.PerVertexData, 0),
				new InputElement("COLOR",    0, Format.R8G8B8A8_UNorm,  InputElement.AppendAligned, 0, InputClassification.PerVertexData, 0),
				new InputElement("TEXCOORD", 0, Format.R32G32_Float,    InputElement.AppendAligned, 0, InputClassification.PerVertexData, 0)
			};

			sceneInputLayout = new InputLayout(device, vs_result.Bytecode, layout);

			SetShaderToScene();
		}

		void LoadDebugShaders()
		{
			using var includeMan = new DefaultIncludeHandler();
			debugVertexShader?.Dispose();
			debugPixelShader?.Dispose();
			debugInputLayout?.Dispose();

			var macros = new ShaderMacro[]
			{
				new ShaderMacro("RS_OIT", (OITCapable && _oitEnabled) ? "1" : "0")
			};

			CompilationResult vs_result = ShaderBytecode.CompileFromFile("Shaders\\debug_vs.hlsl", "main", "vs_5_0", include: includeMan, defines: macros);

			if (vs_result.HasErrors || !string.IsNullOrEmpty(vs_result.Message))
			{
				throw new Exception(vs_result.Message);
			}

			debugVertexShader = new VertexShader(device, vs_result.Bytecode);

			CompilationResult ps_result = ShaderBytecode.CompileFromFile("Shaders\\debug_ps.hlsl", "main", "ps_5_0", include: includeMan, defines: macros);

			if (ps_result.HasErrors || !string.IsNullOrEmpty(ps_result.Message))
			{
				throw new Exception(ps_result.Message);
			}

			debugPixelShader = new PixelShader(device, ps_result.Bytecode);

			var layout = new InputElement[]
			{
				new InputElement("POSITION", 0, Format.R32G32B32_Float,    InputElement.AppendAligned, 0, InputClassification.PerVertexData, 0),
				new InputElement("COLOR",    0, Format.R32G32B32A32_Float, InputElement.AppendAligned, 0, InputClassification.PerVertexData, 0)
			};

			debugInputLayout = new InputLayout(device, vs_result.Bytecode, layout);
		}

		public void LoadShaders()
		{
			OITInitialize();
			LoadOITCompositeShader();

			LoadSceneShaders();
			LoadDebugShaders();
		}

		ShaderResourceView  FragListHeadSRV;
		Texture2D           FragListHead;
		UnorderedAccessView FragListHeadUAV;

		Texture2D           FragListCount;
		ShaderResourceView  FragListCountSRV;
		UnorderedAccessView FragListCountUAV;

		Buffer              FragListNodes;
		ShaderResourceView  FragListNodesSRV;
		UnorderedAccessView FragListNodesUAV;

		void FragListHead_Init()
		{
			CoreExtensions.DisposeAndNullify(ref FragListHead);
			CoreExtensions.DisposeAndNullify(ref FragListHeadSRV);
			CoreExtensions.DisposeAndNullify(ref FragListHeadUAV);

			if (!_oitEnabled)
			{
				return;
			}

			var textureDescription = new Texture2DDescription
			{
				ArraySize         = 1,
				BindFlags         = BindFlags.UnorderedAccess | BindFlags.ShaderResource,
				Usage             = ResourceUsage.Default,
				Format            = Format.R32_UInt,
				Width             = viewport.Width,
				Height            = viewport.Height,
				MipLevels         = 1,
				SampleDescription = { Count = 1, Quality = 0 }
			};

			FragListHead = new Texture2D(device, textureDescription);

			var resourceViewDescription = new ShaderResourceViewDescription
			{
				Format    = textureDescription.Format,
				Dimension = ShaderResourceViewDimension.Texture2D,
				Texture2D = { MipLevels = 1, MostDetailedMip = 0 }
			};

			FragListHeadSRV = new ShaderResourceView(device, FragListHead, resourceViewDescription);

			var uavDescription = new UnorderedAccessViewDescription
			{
				Format    = textureDescription.Format,
				Dimension = UnorderedAccessViewDimension.Texture2D,
				Buffer =
				{
					FirstElement = 0,
					ElementCount = viewport.Width * viewport.Height,
					Flags        = 0
				}
			};

			FragListHeadUAV = new UnorderedAccessView(device, FragListHead, uavDescription);
		}

		void FragListCount_Init()
		{
			CoreExtensions.DisposeAndNullify(ref FragListCount);
			CoreExtensions.DisposeAndNullify(ref FragListCountSRV);
			CoreExtensions.DisposeAndNullify(ref FragListCountUAV);

			if (!_oitEnabled)
			{
				return;
			}

			var textureDescription = new Texture2DDescription
			{
				ArraySize         = 1,
				BindFlags         = BindFlags.UnorderedAccess | BindFlags.ShaderResource,
				Usage             = ResourceUsage.Default,
				Format            = Format.R32_UInt,
				Width             = viewport.Width,
				Height            = viewport.Height,
				MipLevels         = 1,
				SampleDescription = { Count = 1, Quality = 0 }
			};

			FragListCount = new Texture2D(device, textureDescription);

			var resourceViewDescription = new ShaderResourceViewDescription
			{
				Format    = textureDescription.Format,
				Dimension = ShaderResourceViewDimension.Texture2D,
				Texture2D =
				{
					MipLevels       = 1,
					MostDetailedMip = 0
				}
			};

			FragListCountSRV = new ShaderResourceView(device, FragListCount, resourceViewDescription);

			var uavDescription = new UnorderedAccessViewDescription
			{
				Format    = textureDescription.Format,
				Dimension = UnorderedAccessViewDimension.Texture2D,
				Buffer =
				{
					FirstElement = 0,
					ElementCount = viewport.Width * viewport.Height,
					Flags        = 0
				}
			};

			FragListCountUAV = new UnorderedAccessView(device, FragListCount, uavDescription);
		}

		void FragListNodes_Init()
		{
			CoreExtensions.DisposeAndNullify(ref FragListNodes);
			CoreExtensions.DisposeAndNullify(ref FragListNodesSRV);
			CoreExtensions.DisposeAndNullify(ref FragListNodesUAV);

			if (!_oitEnabled)
			{
				return;
			}

			const int maxFragments  = 32;    // TODO: configurable maxFragments
			const int sizeOfOITNode = 4 * 4; // TODO: sizeof(OITNode)

			perSceneData.BufferLength.Value = (uint)viewport.Width * (uint)viewport.Height * maxFragments;

			var bufferDescription = new BufferDescription
			{
				OptionFlags         = ResourceOptionFlags.BufferStructured,
				BindFlags           = BindFlags.UnorderedAccess | BindFlags.ShaderResource,
				SizeInBytes         = sizeOfOITNode * viewport.Width * viewport.Height * maxFragments,
				StructureByteStride = sizeOfOITNode
			};

			FragListNodes = new Buffer(device, bufferDescription);

			var resourceViewDescription = new ShaderResourceViewDescription
			{
				Format    = Format.Unknown,
				Dimension = ShaderResourceViewDimension.Buffer,
				Buffer =
				{
					ElementCount = viewport.Width * viewport.Height * maxFragments
				}
			};

			FragListNodesSRV = new ShaderResourceView(device, FragListNodes, resourceViewDescription);

			var uavDescription = new UnorderedAccessViewDescription
			{
				Format    = Format.Unknown,
				Dimension = UnorderedAccessViewDimension.Buffer,
				Buffer =
				{
					FirstElement = 0,
					ElementCount = viewport.Width * viewport.Height * maxFragments,
					Flags        = UnorderedAccessViewBufferFlags.Counter
				}
			};

			FragListNodesUAV = new UnorderedAccessView(device, FragListNodes, uavDescription);
		}

		readonly UnorderedAccessView[] nullUAVs = new UnorderedAccessView[3];

		void OITRead()
		{
			if (!OITCapable || !_oitEnabled)
			{
				return;
			}

			DeviceContext context = device.ImmediateContext;

			// SharpDX does not have SetRenderTargetsAndUnorderedAccessViews
			context.OutputMerger.SetRenderTargets(depthStencilView: null, renderTargetView: backBuffer);
			context.OutputMerger.SetUnorderedAccessViews(startSlot: 1, unorderedAccessViews: nullUAVs);

			ShaderResourceView[] srvs =
			{
				FragListHeadSRV,
				FragListCountSRV,
				FragListNodesSRV,
				compositeSRV,
				depthShaderResource
			};

			context.PixelShader.SetShaderResources(0, srvs);
		}

		void OITWrite()
		{
			if (!OITCapable)
			{
				return;
			}

			DeviceContext context = device.ImmediateContext;

			// Unbinds the shader resource views for our fragment list and list head.
			// UAVs cannot be bound as standard resource views and UAVs simultaneously.
			var resourceViews = new ShaderResourceView[5];
			context.PixelShader.SetShaderResources(0, resourceViews);

			var unorderedViews = new UnorderedAccessView[]
			{
				FragListHeadUAV,
				FragListCountUAV,
				FragListNodesUAV
			};

			// This is used to set the hidden counter of FragListNodes to 0.
			// It only works on FragListNodes, but the number of elements here
			// must match the number of UAVs given.
			int[] zero = { 0, 0, 0 };

			// Bind our fragment list & list head UAVs for read/write operations.
			context.OutputMerger.SetRenderTargets(depthView, _oitEnabled ? compositeView : backBuffer);
			context.OutputMerger.SetUnorderedAccessViews(1, unorderedViews, zero);

			// Resets the list head indices to OIT_FRAGMENT_LIST_NULL.
			// 4 elements are required as this can be used to clear a texture
			// with 4 color channels, even though our list head only has one.
			context.ClearUnorderedAccessView(FragListHeadUAV, new RawInt4(-1, -1, -1, -1));

			context.ClearUnorderedAccessView(FragListCountUAV, new RawInt4(0, 0, 0, 0));
		}

		void OITInitialize()
		{
			if (!OITCapable)
			{
				return;
			}

			OITRelease();

			if (!_oitEnabled)
			{
				return;
			}

			FragListHead_Init();
			FragListCount_Init();
			FragListNodes_Init();

			OITWrite();
		}

		void OITComposite()
		{
			if (!OITCapable || !_oitEnabled)
			{
				return;
			}

			using DepthStencilState depthState = device.ImmediateContext.OutputMerger.GetDepthStencilState(out int stencilRefRef);
			// TODO: disable culling

			using BlendState blendState = device.ImmediateContext.OutputMerger.BlendState;
			device.ImmediateContext.OutputMerger.BlendState = null;

			SetShaderToOITComposite();
			OITRead();

			// Draw 3 points. The composite shader will use SV_VertexID to
			// automatically produce a triangle that fills the whole screen.
			device.ImmediateContext.Draw(3, 0);

			device.ImmediateContext.OutputMerger.SetDepthStencilState(depthState, stencilRefRef);
			// TODO: restore culling
			device.ImmediateContext.OutputMerger.BlendState = blendState;

			SetShaderToScene();
		}

		void OITRelease()
		{
			if (!OITCapable)
			{
				return;
			}

			UnorderedAccessView[] nullViews = { null, null, null, null, null };

			DeviceContext context = device.ImmediateContext;

			context.OutputMerger.SetRenderTargets(depthView, backBuffer);
			context.OutputMerger.SetUnorderedAccessViews(1, nullViews);

			CoreExtensions.DisposeAndNullify(ref FragListHeadSRV);
			CoreExtensions.DisposeAndNullify(ref FragListHead);
			CoreExtensions.DisposeAndNullify(ref FragListHeadUAV);
			CoreExtensions.DisposeAndNullify(ref FragListCount);
			CoreExtensions.DisposeAndNullify(ref FragListCountSRV);
			CoreExtensions.DisposeAndNullify(ref FragListCountUAV);
			CoreExtensions.DisposeAndNullify(ref FragListNodes);
			CoreExtensions.DisposeAndNullify(ref FragListNodesSRV);
			CoreExtensions.DisposeAndNullify(ref FragListNodesUAV);
		}

		public void Clear()
		{
			if (device == null)
			{
				return;
			}

			meshQueue.Clear();

			device.ImmediateContext.Rasterizer.State = rasterizerState;
			device.ImmediateContext.ClearRenderTargetView(_oitEnabled ? compositeView : backBuffer, new RawColor4(0.0f, 1.0f, 1.0f, 1.0f));

#if REVERSE_Z
			device.ImmediateContext.ClearDepthStencilView(depthView, DepthStencilClearFlags.Depth, 0.0f, 0);
#else
			device.ImmediateContext.ClearDepthStencilView(depthView, DepthStencilClearFlags.Depth, 1.0f, 0);
#endif
		}

		public void Draw(Camera camera, NJS_OBJECT @object)
		{
			while (!(@object is null))
			{
				MatrixStack.Push();
				@object.PushTransform();
				Matrix m = MatrixStack.Peek();
				SetTransform(TransformState.World, in m);

				if (!@object.SkipDraw && @object.Model != null)
				{
					Draw(camera, @object, @object.Model);
				}

				if (!@object.SkipChildren)
				{
					Draw(camera, @object.Child);
				}

				MatrixStack.Pop();
				@object = @object.Sibling;
			}
		}

		public void Draw(Camera camera, NJS_OBJECT @object, NJS_MODEL model)
		{
			foreach (NJS_MESHSET set in model.meshsets)
			{
				meshQueue.Enqueue(this, camera, @object, model, set);
			}
		}

		void DrawSet(NJS_MODEL parent, NJS_MESHSET set)
		{
			List<NJS_MATERIAL> mats = parent.mats;

			ushort materialId = set.MaterialId;
			NJS_MATERIAL njMaterial = mats.Count > 0 && materialId < mats.Count ? mats[materialId] : nullMaterial;

			FlowControl flowControl = FlowControl;

			if (texturePool.Count < 1)
			{
				if (!FlowControl.UseMaterialFlags)
				{
					FlowControl.Reset();
					FlowControl.UseMaterialFlags = true;
				}

				FlowControl.Set(FlowControl.AndFlags & ~NJD_FLAG.UseTexture, FlowControl.OrFlags);
			}

			SceneMaterial sceneMaterial = NJS_MODEL.GetSADXMaterial(this, njMaterial);
			SetSceneMaterial(in sceneMaterial);

			DisplayState state = GetSADXDisplayState(njMaterial);

			FlowControl = flowControl;

			if (state.Blend != lastBlend)
			{
				device.ImmediateContext.OutputMerger.SetBlendState(state.Blend);
				lastBlend = state.Blend;
			}

			if (state.Sampler != lastSamplerState)
			{
				device.ImmediateContext.PixelShader.SetSampler(0, state.Sampler);
				lastSamplerState = state.Sampler;
			}

			if (state.Raster != lastRasterizerState)
			{
				device.ImmediateContext.Rasterizer.State = state.Raster;
				lastRasterizerState = state.Raster;
			}

			RenderTargetBlendDescription desc = state.Blend.Description.RenderTarget[0];

			perModelData.DrawCall.Value += 1;
			perModelData.DrawCall.Value %= ushort.MaxValue + 1;

			perModelData.SourceBlend.Value      = (uint)desc.SourceBlend;
			perModelData.DestinationBlend.Value = (uint)desc.DestinationBlend;
			perModelData.BlendOperation.Value   = (uint)desc.BlendOperation;

			perModelData.IsStandardBlending.Value = (desc.SourceBlend == BlendOption.SourceAlpha || desc.SourceBlend == BlendOption.One) &&
			                                        (desc.DestinationBlend == BlendOption.InverseSourceAlpha || desc.DestinationBlend == BlendOption.Zero);

			CommitPerModelData();

			if (parent.VertexBuffer != lastVertexBuffer)
			{
				var binding = new VertexBufferBinding(parent.VertexBuffer, Vertex.SizeInBytes, 0);
				device.ImmediateContext.InputAssembler.SetVertexBuffers(0, binding);
				lastVertexBuffer = parent.VertexBuffer;
			}

			device.ImmediateContext.InputAssembler.SetIndexBuffer(set.IndexBuffer, Format.R16_UInt, 0);
			device.ImmediateContext.DrawIndexed(set.IndexCount, 0, 0);
		}

		void CommitPerModelData()
		{
			if (!perModelData.Modified)
			{
				return;
			}

			device.ImmediateContext.MapSubresource(perModelBuffer, MapMode.WriteDiscard,
			                                       MapFlags.None, out DataStream stream);

			Matrix wvMatrixInvT = perModelData.World.Value * perSceneData.View.Value;
			Matrix.Invert(ref wvMatrixInvT, out wvMatrixInvT);

			perModelData.wvMatrixInvT.Value = wvMatrixInvT;

			using (stream)
			{
				var writer = new CBufferStreamWriter(stream);
				perModelData.Write(writer);
				//Debug.Assert(stream.RemainingLength == 0);
			}

			device.ImmediateContext.UnmapSubresource(perModelBuffer, 0);
			perModelData.Clear();
		}

		void CommitPerSceneData()
		{
			if (!perSceneData.Modified)
			{
				return;
			}

			device.ImmediateContext.MapSubresource(perSceneBuffer, MapMode.WriteDiscard,
			                                       MapFlags.None, out DataStream stream);

			using (stream)
			{
				var writer = new CBufferStreamWriter(stream);
				perSceneData.Write(writer);
				Debug.Assert(stream.RemainingLength == 0);
			}

			perSceneData.Clear();
			device.ImmediateContext.UnmapSubresource(perSceneBuffer, 0);
		}

		public void Present(Camera camera) // TODO: don't pass camera to present - maybe store the camera as part of the draw queue
		{
			int visibleCount = 0;
			materialData.WriteDepth.Value = true;

			perSceneData.CameraPosition.Value = camera.Position;
			CommitPerSceneData();

			//meshTree.SortOpaque();

			foreach (MeshsetQueueElement e in meshQueue.OpaqueSets)
			{
				++visibleCount;
				DrawMeshsetQueueElement(e);
			}

			if (EnableAlpha && meshQueue.AlphaSets.Count > 1)
			{
				if (_oitEnabled)
				{
					// Enable z-write so that if we find "transparent" geometry which
					// is actually opaque, we can force it into the opaque back buffer
					// to avoid extra work in the composite stage.
					device.ImmediateContext.OutputMerger.SetDepthStencilState(depthStateRW);
					materialData.WriteDepth.Value = true;

					// Now simply draw
					foreach (MeshsetQueueElement e in meshQueue.AlphaSets)
					{
						DrawMeshsetQueueElement(e);
					}
				}
				else
				{
					meshQueue.SortAlpha();

					// First draw with depth writes enabled & alpha threshold (in shader)
					foreach (MeshsetQueueElement e in meshQueue.AlphaSets)
					{
						DrawMeshsetQueueElement(e);
					}

					// Now draw with depth writes disabled
					device.ImmediateContext.OutputMerger.SetDepthStencilState(depthStateRO);
					materialData.WriteDepth.Value = false;

					foreach (MeshsetQueueElement e in meshQueue.AlphaSets)
					{
						++visibleCount;
						DrawMeshsetQueueElement(e);
					}

					device.ImmediateContext.OutputMerger.SetDepthStencilState(depthStateRW);
					materialData.WriteDepth.Value = true;
				}
			}

			DrawDebugHelpers();
			meshQueue.Clear();

			if (_oitEnabled)
			{
				OITComposite();
				swapChain.Present(0, 0);
				OITWrite();
			}
			else
			{
				swapChain.Present(0, 0);
			}

			if (!MatrixStack.Empty)
			{
				throw new Exception("Matrix stack still contains data");
			}

			// Used for debugging.
			// ReSharper disable once RedundantCheckBeforeAssignment
			if (visibleCount != lastVisibleCount)
			{
				lastVisibleCount = visibleCount;
				Debug.WriteLine($"Visible: {visibleCount}");
			}

			lastVertexBuffer = null;
		}

		static void WriteToStream(in DebugPoint point, DataStream stream)
		{
			stream.Write(point.Point);

			Color4 color = point.Color;

			stream.Write(color.Red);
			stream.Write(color.Green);
			stream.Write(color.Blue);
			stream.Write(1.0f);
		}

		static void WriteToStream(in DebugLine line, DataStream stream)
		{
			WriteToStream(in line.PointA, stream);
			WriteToStream(in line.PointB, stream);
		}

		void WriteToStream(in DebugWireCube cube, DataStream stream)
		{
			foreach (DebugLine line in cube.Lines)
			{
				WriteToStream(in line, stream);
			}
		}

		void DrawDebugHelpers()
		{
			if (debugLines.Count == 0 && debugWireCubes.Count == 0)
			{
				return;
			}

			CommitPerSceneData();

			SetShaderToDebug();

			// TODO: make debug z-writes (and tests) configurable
			device.ImmediateContext.OutputMerger.SetDepthStencilState(depthStateRW);
			materialData.WriteDepth.Value = true;

			// using these variables we're able to batch lines into
			// the cube-sized vertex buffer to reduce draw calls.
			int lineCount = debugLines.Count;
			int lineIndex = 0;

			while (lineCount > 0)
			{
				int n = Math.Min(lineCount, 12);
				device.ImmediateContext.MapSubresource(debugHelperVertexBuffer, MapMode.WriteDiscard, MapFlags.None, out DataStream stream);

				using (stream)
				{
					for (int i = 0; i < n; i++)
					{
						DebugLine line = debugLines[lineIndex++];
						WriteToStream(in line, stream);
						--lineCount;
					}
				}

				device.ImmediateContext.UnmapSubresource(debugHelperVertexBuffer, 0);
				device.ImmediateContext.Draw(2 * n, 0);
			}

			foreach (DebugWireCube cube in debugWireCubes)
			{
				device.ImmediateContext.MapSubresource(debugHelperVertexBuffer, MapMode.WriteDiscard, MapFlags.None, out DataStream stream);

				using (stream)
				{
					WriteToStream(in cube, stream);
				}

				device.ImmediateContext.UnmapSubresource(debugHelperVertexBuffer, 0);
				device.ImmediateContext.Draw(24, 0);
			}

			debugWireCubes.Clear();
			debugLines.Clear();

			SetShaderToScene();
		}

		void DrawMeshsetQueueElement(MeshsetQueueElement e)
		{
			FlowControl = e.FlowControl;

			Matrix m = e.Transform;
			SetTransform(TransformState.World, in m);

			if (!e.Object.SkipDraw)
			{
				DrawSet(e.Model, e.Set);
			}
		}

		void CreateOITCompositeTexture(Texture2DDescription textureDescription)
		{
			CoreExtensions.DisposeAndNullify(ref compositeTexture);
			CoreExtensions.DisposeAndNullify(ref compositeView);
			CoreExtensions.DisposeAndNullify(ref compositeSRV);

			textureDescription.Usage     = ResourceUsage.Default;
			textureDescription.BindFlags = BindFlags.RenderTarget | BindFlags.ShaderResource;

			compositeTexture = new Texture2D(device, textureDescription);

			var viewDescription = new RenderTargetViewDescription
			{
				Format    = textureDescription.Format,
				Dimension = RenderTargetViewDimension.Texture2D,
				Texture2D = { MipSlice = 0 }
			};

			compositeView = new RenderTargetView(device, compositeTexture, viewDescription);

			var srvDescription = new ShaderResourceViewDescription
			{
				Format    = textureDescription.Format,
				Dimension = ShaderResourceViewDimension.Texture2D,
				Texture2D =
				{
					MostDetailedMip = 0,
					MipLevels       = 1
				}
			};

			compositeSRV = new ShaderResourceView(device, compositeTexture, srvDescription);
		}

		void CreateRenderTarget()
		{
			Texture2DDescription description;

			using (var pBackBuffer = Resource.FromSwapChain<Texture2D>(swapChain, 0))
			{
				CoreExtensions.DisposeAndNullify(ref backBuffer);
				backBuffer = new RenderTargetView(device, pBackBuffer);

				description = pBackBuffer.Description;
			}

			CreateOITCompositeTexture(description);

			device.ImmediateContext.OutputMerger.SetRenderTargets(backBuffer);
		}

		void SetViewPort(int x, int y, int width, int height)
		{
			viewport.MinDepth = 0f;
			viewport.MaxDepth = 1f;

			Viewport vp = viewport;

			vp.X      = x;
			vp.Y      = y;
			vp.Width  = width;
			vp.Height = height;

			if (vp == viewport)
			{
				return;
			}

			viewport = vp;
			device.ImmediateContext.Rasterizer.SetViewport(viewport);
		}

		public void Draw(IEnumerable<MeshsetQueueElementBase> visible, Camera camera)
		{
			foreach (MeshsetQueueElement e in visible.Select(x => new MeshsetQueueElement(x, camera)))
			{
				meshQueue.Enqueue(e);
			}
		}

		public void RefreshDevice(int w, int h)
		{
			backBuffer?.Dispose();
			swapChain?.ResizeBuffers(1, w, h, Format.Unknown, 0);

			SetViewPort(0, 0, w, h);

			CreateRenderTarget();
			CreateRasterizerState();
			CreateDepthStencil(w, h);

			OITRelease();
			OITInitialize();
		}

		void CreateDepthStencil(int w, int h)
		{
			const Format textureFormat = Format.R24G8_Typeless;
			const Format viewFormat    = Format.R24_UNorm_X8_Typeless;
			const Format depthFormat   = Format.D24_UNorm_S8_UInt;

			CoreExtensions.DisposeAndNullify(ref depthTexture);
			CoreExtensions.DisposeAndNullify(ref depthStateRW);
			CoreExtensions.DisposeAndNullify(ref depthStateRO);
			CoreExtensions.DisposeAndNullify(ref depthView);
			CoreExtensions.DisposeAndNullify(ref depthShaderResource);

			var depthTextureDesc = new Texture2DDescription
			{
				Width             = w,
				Height            = h,
				MipLevels         = 1,
				ArraySize         = 1,
				Format            = textureFormat,
				SampleDescription = new SampleDescription(1, 0),
				Usage             = ResourceUsage.Default,
				BindFlags         = BindFlags.DepthStencil | BindFlags.ShaderResource,
				CpuAccessFlags    = CpuAccessFlags.None,
				OptionFlags       = ResourceOptionFlags.None
			};

			depthTexture = new Texture2D(device, depthTextureDesc);

			depthDesc = new DepthStencilStateDescription
			{
				IsDepthEnabled = true,
				DepthWriteMask = DepthWriteMask.All,
#if REVERSE_Z
				DepthComparison = Comparison.Greater,
#else
				DepthComparison = Comparison.Less,
#endif

				FrontFace = new DepthStencilOperationDescription
				{
					FailOperation = StencilOperation.Keep,
					PassOperation = StencilOperation.Keep,
					Comparison    = Comparison.Always
				},

				BackFace = new DepthStencilOperationDescription
				{
					FailOperation = StencilOperation.Keep,
					PassOperation = StencilOperation.Keep,
					Comparison    = Comparison.Always
				}
			};

			depthStateRW = new DepthStencilState(device, depthDesc);

			depthDesc.DepthWriteMask = DepthWriteMask.Zero;

			depthStateRO = new DepthStencilState(device, depthDesc);

			var depthViewDesc = new DepthStencilViewDescription
			{
				Format    = depthFormat,
				Dimension = DepthStencilViewDimension.Texture2D,
				Texture2D = new DepthStencilViewDescription.Texture2DResource
				{
					MipSlice = 0
				}
			};

			depthView = new DepthStencilView(device, depthTexture, depthViewDesc);

			device.ImmediateContext.OutputMerger.SetTargets(depthView, backBuffer);
			device.ImmediateContext.OutputMerger.SetDepthStencilState(depthStateRW);

			var resourceViewDescription = new ShaderResourceViewDescription
			{
				Format    = viewFormat,
				Dimension = ShaderResourceViewDimension.Texture2D,
				Texture2D = new ShaderResourceViewDescription.Texture2DResource
				{
					MipLevels       = 1,
					MostDetailedMip = 0
				}
			};

			depthShaderResource = new ShaderResourceView(device, depthTexture, resourceViewDescription);
		}

		void CreateRasterizerState()
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

		void CopyToTexture(Texture2D texture, Bitmap bitmap, int level)
		{
			BitmapData bmpData = bitmap.LockBits(new System.Drawing.Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);

			byte[] buffer = new byte[bmpData.Stride * bitmap.Height];

			Marshal.Copy(bmpData.Scan0, buffer, 0, buffer.Length);

			bitmap.UnlockBits(bmpData);

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

		// TODO: generate mipmaps mod loader style
		public void CreateTextureFromBitmapSource(BitmapSource bitmapSource)
		{
			int stride = bitmapSource.Size.Width * 4;

			using var buffer = new DataStream(bitmapSource.Size.Height * stride, true, true);

			bitmapSource.CopyPixels(stride, buffer);

			var texture = new Texture2D(device, new Texture2DDescription
			{
				Width             = bitmapSource.Size.Width,
				Height            = bitmapSource.Size.Height,
				ArraySize         = 1,
				BindFlags         = BindFlags.ShaderResource,
				Usage             = ResourceUsage.Immutable,
				CpuAccessFlags    = CpuAccessFlags.None,
				Format            = Format.R8G8B8A8_UNorm,
				MipLevels         = 1,
				OptionFlags       = ResourceOptionFlags.None,
				SampleDescription = new SampleDescription(1, 0)
			}, new DataRectangle(buffer.DataPointer, stride));

			texturePool.Add(new SceneTexture(texture, new ShaderResourceView(device, texture)));
		}

		public void ClearTexturePool()
		{
			foreach (SceneTexture texture in texturePool)
			{
				texture.Dispose();
			}

			texturePool.Clear();
			SetTexture(0, -1);
		}

		public Buffer CreateVertexBuffer(IReadOnlyCollection<Vertex> vertices)
		{
			int vertexSize = vertices.Count * Vertex.SizeInBytes;

			var desc = new BufferDescription(vertexSize, BindFlags.VertexBuffer, ResourceUsage.Immutable);

			using var stream = new DataStream(vertexSize, true, true);

			foreach (Vertex v in vertices)
			{
				stream.Write(v.Position.X);
				stream.Write(v.Position.Y);
				stream.Write(v.Position.Z);

				stream.Write(v.Normal.X);
				stream.Write(v.Normal.Y);
				stream.Write(v.Normal.Z);

				RawColorBGRA color = v.Diffuse ?? Color.White;

				stream.Write(color.B);
				stream.Write(color.G);
				stream.Write(color.R);
				stream.Write(color.A);

				RawVector2 uv = v.UV ?? Vector2.Zero;

				stream.Write(uv.X);
				stream.Write(uv.Y);
			}

			if (stream.RemainingLength != 0)
			{
				throw new Exception("Failed to fill vertex buffer.");
			}

			stream.Position = 0;
			return new Buffer(device, stream, desc);
		}

		public Buffer CreateIndexBuffer(IEnumerable<short> indices, int sizeInBytes)
		{
			var desc = new BufferDescription(sizeInBytes, BindFlags.IndexBuffer, ResourceUsage.Immutable);

			using var stream = new DataStream(sizeInBytes, true, true);
			foreach (short i in indices)
			{
				stream.Write(i);
			}

			if (stream.RemainingLength != 0)
			{
				throw new Exception("Failed to fill index buffer.");
			}

			stream.Position = 0;
			return new Buffer(device, stream, desc);
		}

		public void SetTransform(TransformState state, in Matrix m)
		{
			switch (state)
			{
				case TransformState.World:
					perModelData.World.Value = m;
					break;
				case TransformState.View:
					perSceneData.View.Value = m;
					break;
				case TransformState.Projection:
#if REVERSE_Z
					var a = new Matrix(
						1f, 0f,  0f, 0f,
						0f, 1f,  0f, 0f,
						0f, 0f, -1f, 0f,
						0f, 0f,  1f, 1f
					);

					perSceneData.Projection.Value = rawMatrix * a;
#else
					perSceneData.Projection.Value = m;
#endif
					break;
				default:
					throw new ArgumentOutOfRangeException(nameof(state), state, null);
			}
		}

		public void SetTexture(int sampler, int textureIndex)
		{
			if (textureIndex >= 0 && textureIndex < texturePool.Count)
			{
				SceneTexture texture = texturePool[textureIndex];

				if (texture == lastTexture)
				{
					return;
				}

				device.ImmediateContext.PixelShader.SetShaderResource(sampler, texture.ShaderResource);
				lastTexture = texture;
			}
			else if (lastTexture != null)
			{
				lastTexture = null;
				device.ImmediateContext.PixelShader.SetShaderResource(sampler, null);
			}
		}

		// TODO: generate in bulk
		public DisplayState GetSADXDisplayState(NJS_MATERIAL material)
		{
			// Not implemented in SADX:
			// - NJD_FLAG.Pick
			// - NJD_FLAG.UseAnisotropic
			// - NJD_FLAG.UseFlat

			const NJD_FLAG state_mask = NJD_FLAG.ClampU | NJD_FLAG.ClampV | NJD_FLAG.FlipU | NJD_FLAG.FlipV
			                            | NJD_FLAG.DoubleSide | NJD_FLAG.UseAlpha
			                            | (NJD_FLAG)0xFC000000 /* blend modes */;

			NJD_FLAG flags = FlowControl.Apply(material.attrflags) & state_mask;

			if (DefaultCullMode == CullMode.None)
			{
				flags |= NJD_FLAG.DoubleSide;
			}

			if (displayStates.TryGetValue(flags, out DisplayState state))
			{
				return state;
			}

			var samplerDesc = new SamplerStateDescription
			{
				AddressW           = TextureAddressMode.Wrap,
				Filter             = Filter.MinMagMipLinear,
				MinimumLod         = -float.MaxValue,
				MaximumLod         = float.MaxValue,
				MaximumAnisotropy  = 1,
				ComparisonFunction = Comparison.Never
			};

			if ((flags & (NJD_FLAG.ClampU | NJD_FLAG.FlipU)) == (NJD_FLAG.ClampU | NJD_FLAG.FlipU))
			{
				samplerDesc.AddressU = TextureAddressMode.MirrorOnce;
			}
			else if ((flags & NJD_FLAG.ClampU) != 0)
			{
				samplerDesc.AddressU = TextureAddressMode.Clamp;
			}
			else if ((flags & NJD_FLAG.FlipU) != 0)
			{
				samplerDesc.AddressU = TextureAddressMode.Mirror;
			}
			else
			{
				samplerDesc.AddressU = TextureAddressMode.Wrap;
			}

			if ((flags & (NJD_FLAG.ClampV | NJD_FLAG.FlipV)) == (NJD_FLAG.ClampV | NJD_FLAG.FlipV))
			{
				samplerDesc.AddressV = TextureAddressMode.MirrorOnce;
			}
			else if ((flags & NJD_FLAG.ClampV) != 0)
			{
				samplerDesc.AddressV = TextureAddressMode.Clamp;
			}
			else if ((flags & NJD_FLAG.FlipV) != 0)
			{
				samplerDesc.AddressV = TextureAddressMode.Mirror;
			}
			else
			{
				samplerDesc.AddressV = TextureAddressMode.Wrap;
			}

			var sampler = new SamplerState(device, samplerDesc) { DebugName = $"Sampler: {flags.ToString()}" };

			// Base it off of the default rasterizer state.
			RasterizerStateDescription rasterDesc = rasterizerDescription;

			rasterDesc.CullMode = (flags & NJD_FLAG.DoubleSide) != 0 ? CullMode.None : DefaultCullMode;

			var raster = new RasterizerState(device, rasterDesc) { DebugName = $"Rasterizer: {flags.ToString()}" };

			var blendDesc = new BlendStateDescription();
			ref RenderTargetBlendDescription rt = ref blendDesc.RenderTarget[0];

			rt.IsBlendEnabled        = (flags & NJD_FLAG.UseAlpha) != 0;
			rt.SourceBlend           = blendModes[material.SourceBlend];
			rt.DestinationBlend      = blendModes[material.DestinationBlend];
			rt.BlendOperation        = BlendOperation.Add;
			rt.SourceAlphaBlend      = BlendOption.Zero;
			rt.DestinationAlphaBlend = BlendOption.Zero;
			rt.AlphaBlendOperation   = BlendOperation.Add;
			rt.RenderTargetWriteMask = ColorWriteMaskFlags.All;

			var blend = new BlendState(device, blendDesc) { DebugName = $"Blend: {flags.ToString()}" };

			var result = new DisplayState(sampler, raster, blend);
			displayStates[flags] = result;

			return result;
		}

		public void SetSceneMaterial(in SceneMaterial material)
		{
			materialData.Material.Value = material;

			if (!materialData.Modified)
			{
				return;
			}

			materialData.Clear();

			device.ImmediateContext.MapSubresource(materialBuffer, MapMode.WriteDiscard, MapFlags.None, out DataStream stream);

			using (stream)
			{
				var writer = new CBufferStreamWriter(stream);
				materialData.Write(writer);
			}

			device.ImmediateContext.UnmapSubresource(materialBuffer, 0);
		}

		public void Dispose()
		{
			swapChain?.Dispose();
			backBuffer?.Dispose();

			depthTexture?.Dispose();
			depthStateRW?.Dispose();
			depthStateRO?.Dispose();
			depthView?.Dispose();

			rasterizerState?.Dispose();

			materialBuffer?.Dispose();

			sceneVertexShader?.Dispose();
			scenePixelShader?.Dispose();

			oitCompositeVertexShader?.Dispose();
			oitCompositePixelShader?.Dispose();

			debugVertexShader?.Dispose();
			debugPixelShader?.Dispose();

			sceneInputLayout?.Dispose();

			debugInputLayout?.Dispose();
			debugHelperVertexBuffer?.Dispose();

			perSceneBuffer?.Dispose();
			perModelBuffer?.Dispose();
			materialBuffer?.Dispose();

			CoreExtensions.DisposeAndNullify(ref FragListHeadSRV);
			CoreExtensions.DisposeAndNullify(ref FragListHead);
			CoreExtensions.DisposeAndNullify(ref FragListHeadUAV);
			CoreExtensions.DisposeAndNullify(ref FragListCount);
			CoreExtensions.DisposeAndNullify(ref FragListCountSRV);
			CoreExtensions.DisposeAndNullify(ref FragListCountUAV);
			CoreExtensions.DisposeAndNullify(ref FragListNodes);
			CoreExtensions.DisposeAndNullify(ref FragListNodesSRV);
			CoreExtensions.DisposeAndNullify(ref FragListNodesUAV);

			ClearTexturePool();
			ClearDisplayStates();

#if DEBUG
			using (var debug = new DeviceDebug(device))
			{
				debug.ReportLiveDeviceObjects(ReportingLevel.Summary);
			}
#endif

			device?.Dispose();
		}

		void ClearDisplayStates()
		{
			foreach (KeyValuePair<NJD_FLAG, DisplayState> i in displayStates)
			{
				i.Value.Dispose();
			}

			displayStates.Clear();
		}

		public void DrawDebugLine(DebugPoint start, DebugPoint end)
		{
			DrawDebugLine(new DebugLine(start, end));
		}

		public void DrawDebugLine(DebugLine line)
		{
			debugLines.Add(line);
		}

		public void DrawBounds(in BoundingBox bounds, Color4 color)
		{
			debugWireCubes.Add(new DebugWireCube(in bounds, color));
		}
	}

	class InsufficientFeatureLevelException : Exception
	{
		public readonly FeatureLevel SupportedLevel;
		public readonly FeatureLevel TargetLevel;

		public InsufficientFeatureLevelException(FeatureLevel supported, FeatureLevel target)
			: base($"Required feature level unsupported. Expected {target}, got {supported}")
		{
			SupportedLevel = supported;
			TargetLevel    = target;
		}
	}
}