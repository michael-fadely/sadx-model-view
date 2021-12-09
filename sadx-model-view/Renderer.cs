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
		private static readonly NJS_MATERIAL NullMaterial = new();

		private static readonly BlendOption[] BlendModes =
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

		private int _lastVisibleCount;

		public FlowControl FlowControl;
		public bool EnableAlpha = true;

		private CullMode _defaultCullMode = CullMode.None;

		public CullMode DefaultCullMode
		{
			get => _defaultCullMode;
			set
			{
				_defaultCullMode = value;
				ClearDisplayStates();
			}
		}

		private readonly List<SceneTexture> _texturePool = new();

		private readonly Device                       _device;
		private readonly SwapChain                    _swapChain;
		private          RenderTargetView?            _backBuffer;
		private          Viewport                     _viewport;
		private          Texture2D?                   _depthTexture;
		private          DepthStencilStateDescription _depthDesc;
		private          DepthStencilState?           _depthStateRw;
		private          DepthStencilState?           _depthStateRo;
		private          DepthStencilView?            _depthView;
		private          ShaderResourceView?          _depthShaderResource;
		private          RasterizerState?             _rasterizerState;
		private          RasterizerStateDescription   _rasterizerDescription;

		private Texture2D?          _compositeTexture;
		private RenderTargetView?   _compositeView;
		private ShaderResourceView? _compositeSRV;

		private readonly Buffer _debugHelperVertexBuffer;
		private readonly Buffer _perSceneBuffer;
		private readonly Buffer _perModelBuffer;
		private readonly Buffer _materialBuffer;

		private readonly PerSceneBuffer _perSceneData = new();
		private readonly PerModelBuffer _perModelData = new();
		private readonly MaterialBuffer _materialData = new();

		private readonly MeshsetQueue                       _meshQueue     = new();
		private readonly Dictionary<NJD_FLAG, DisplayState> _displayStates = new();

		private readonly List<DebugLine>     _debugLines     = new();
		private readonly List<DebugWireCube> _debugWireCubes = new();

		private VertexShader? _sceneVertexShader;
		private PixelShader?  _scenePixelShader;
		private InputLayout?  _sceneInputLayout;

		private VertexShader? _oitCompositeVertexShader;
		private PixelShader?  _oitCompositePixelShader;

		private VertexShader? _debugVertexShader;
		private PixelShader?  _debugPixelShader;
		private InputLayout?  _debugInputLayout;

		private Buffer?          _lastVertexBuffer;
		private BlendState?      _lastBlend;
		private RasterizerState? _lastRasterizerState;
		private SamplerState?    _lastSamplerState;
		private SceneTexture?    _lastTexture;

		public bool OITCapable { get; private set; }

		private bool _oitEnabled;

		public bool OITEnabled
		{
			get => _oitEnabled;
			set
			{
				if (value && !OITCapable)
				{
					throw new InvalidOperationException("Device not OIT-capable!");
				}

				if (value == _oitEnabled)
				{
					return;
				}

				_oitEnabled = value;

				try
				{
					LoadShaders();
				}
				catch
				{
					if (value)
					{
						_oitEnabled = false;
						LoadShaders();
					}

					throw;
				}
			}
		}

		public Renderer(int screenWidth, int screenHeight, IntPtr sceneHandle)
		{
			FlowControl.Reset();

			var desc = new SwapChainDescription
			{
				BufferCount       = 1,
				ModeDescription   = new ModeDescription(screenWidth, screenHeight, new Rational(1000, 60), Format.R8G8B8A8_UNorm),
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

			Device.CreateWithSwapChain(DriverType.Hardware, flag, levels, desc, out _device, out _swapChain);

			if (_device.FeatureLevel < FeatureLevel.Level_10_0)
			{
				throw new InsufficientFeatureLevelException(_device.FeatureLevel, FeatureLevel.Level_10_0);
			}

			OITCapable = _device.FeatureLevel >= FeatureLevel.Level_11_0;

			int bufferSize = (int)CBuffer.CalculateSize(_perSceneData);

			var bufferDesc = new BufferDescription(bufferSize,
			                                       ResourceUsage.Dynamic, BindFlags.ConstantBuffer, CpuAccessFlags.Write,
			                                       ResourceOptionFlags.None, bufferSize);

			_perSceneBuffer = new Buffer(_device, bufferDesc);

			bufferSize = (int)CBuffer.CalculateSize(_perModelData);

			bufferDesc = new BufferDescription(bufferSize,
			                                   ResourceUsage.Dynamic, BindFlags.ConstantBuffer, CpuAccessFlags.Write,
			                                   ResourceOptionFlags.None, bufferSize);

			_perModelBuffer = new Buffer(_device, bufferDesc);

			int size = (int)CBuffer.CalculateSize(_materialData);

			bufferDesc = new BufferDescription(size, ResourceUsage.Dynamic, BindFlags.ConstantBuffer,
			                                   CpuAccessFlags.Write, ResourceOptionFlags.None, structureByteStride: size);

			_materialBuffer = new Buffer(_device, bufferDesc);

			RefreshDevice(screenWidth, screenHeight);

			LoadShaders();

			_device.ImmediateContext.VertexShader.SetConstantBuffer(0, _perSceneBuffer);
			_device.ImmediateContext.PixelShader.SetConstantBuffer(0, _perSceneBuffer);

			_device.ImmediateContext.VertexShader.SetConstantBuffer(1, _materialBuffer);
			_device.ImmediateContext.PixelShader.SetConstantBuffer(1, _materialBuffer);

			_device.ImmediateContext.VertexShader.SetConstantBuffer(2, _perModelBuffer);
			_device.ImmediateContext.PixelShader.SetConstantBuffer(2, _perModelBuffer);

			int helperBufferSize = DebugWireCube.SizeInBytes.AlignUp(16);

			var debugHelperDescription = new BufferDescription(helperBufferSize, BindFlags.VertexBuffer, ResourceUsage.Dynamic)
			{
				CpuAccessFlags      = CpuAccessFlags.Write,
				StructureByteStride = DebugPoint.SizeInBytes
			};

			_debugHelperVertexBuffer = new Buffer(_device, debugHelperDescription);

		}

		private void SetShaderToScene()
		{
			_device.ImmediateContext.VertexShader.Set(_sceneVertexShader);
			_device.ImmediateContext.PixelShader.Set(_scenePixelShader);
			_device.ImmediateContext.InputAssembler.InputLayout = _sceneInputLayout;

			_device.ImmediateContext.InputAssembler.PrimitiveTopology = PrimitiveTopology.TriangleList;
		}

		private void SetShaderToOITComposite()
		{
			if (!OITCapable)
			{
				return;
			}

			_device.ImmediateContext.VertexShader.Set(_oitCompositeVertexShader);
			_device.ImmediateContext.PixelShader.Set(_oitCompositePixelShader);

			_device.ImmediateContext.InputAssembler.InputLayout = null;
			_device.ImmediateContext.InputAssembler.SetIndexBuffer(null, Format.Unknown, 0);
			_device.ImmediateContext.InputAssembler.SetVertexBuffers(0, 0, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);

			_lastVertexBuffer = null;
		}

		private void SetShaderToDebug()
		{
			_device.ImmediateContext.VertexShader.Set(_debugVertexShader);
			_device.ImmediateContext.PixelShader.Set(_debugPixelShader);
			_device.ImmediateContext.InputAssembler.InputLayout = _debugInputLayout;

			var binding = new VertexBufferBinding(_debugHelperVertexBuffer, DebugPoint.SizeInBytes, 0);

			_device.ImmediateContext.InputAssembler.SetVertexBuffers(0, binding);
			_device.ImmediateContext.InputAssembler.PrimitiveTopology = PrimitiveTopology.LineList;
		}

		private void LoadOITCompositeShader()
		{
			CoreExtensions.DisposeAndNullify(ref _oitCompositeVertexShader);
			CoreExtensions.DisposeAndNullify(ref _oitCompositePixelShader);

			if (!OITCapable || !_oitEnabled)
			{
				return;
			}

			var macros = new ShaderMacro[]
			{
				new("RS_OIT", (OITCapable && _oitEnabled) ? "1" : "0")
			};

			using var includeMan = new DefaultIncludeHandler();

			CompilationResult vsResult = ShaderBytecode.CompileFromFile("Shaders\\oit_composite.hlsl", "vs_main", "vs_5_0", include: includeMan, defines: macros);

			if (vsResult.HasErrors || !string.IsNullOrEmpty(vsResult.Message))
			{
				throw new Exception(vsResult.Message);
			}

			_oitCompositeVertexShader = new VertexShader(_device, vsResult.Bytecode);

			CompilationResult psResult = ShaderBytecode.CompileFromFile("Shaders\\oit_composite.hlsl", "ps_main", "ps_5_0", include: includeMan, defines: macros);

			if (psResult.HasErrors || !string.IsNullOrEmpty(psResult.Message))
			{
				throw new Exception(psResult.Message);
			}

			_oitCompositePixelShader = new PixelShader(_device, psResult.Bytecode);
		}

		private void LoadSceneShaders()
		{
			using var includeMan = new DefaultIncludeHandler();

			_sceneVertexShader?.Dispose();
			_scenePixelShader?.Dispose();
			_sceneInputLayout?.Dispose();

			var macros = new ShaderMacro[]
			{
				new("RS_OIT", (OITCapable && _oitEnabled) ? "1" : "0")
			};

			CompilationResult vsResult = ShaderBytecode.CompileFromFile("Shaders\\scene_vs.hlsl", "main", "vs_5_0",
			                                                             include: includeMan, defines: macros);

			if (vsResult.HasErrors || !string.IsNullOrEmpty(vsResult.Message))
			{
				throw new Exception(vsResult.Message);
			}

			_sceneVertexShader = new VertexShader(_device, vsResult.Bytecode);

			CompilationResult psResult = ShaderBytecode.CompileFromFile("Shaders\\scene_ps.hlsl", "main", "ps_5_0",
			                                                             include: includeMan, defines: macros);

			if (psResult.HasErrors || !string.IsNullOrEmpty(psResult.Message))
			{
				throw new Exception(psResult.Message);
			}

			_scenePixelShader = new PixelShader(_device, psResult.Bytecode);

			var layout = new InputElement[]
			{
				new("POSITION", 0, Format.R32G32B32_Float, InputElement.AppendAligned, 0, InputClassification.PerVertexData, 0),
				new("NORMAL",   0, Format.R32G32B32_Float, InputElement.AppendAligned, 0, InputClassification.PerVertexData, 0),
				new("COLOR",    0, Format.R8G8B8A8_UNorm,  InputElement.AppendAligned, 0, InputClassification.PerVertexData, 0),
				new("TEXCOORD", 0, Format.R32G32_Float,    InputElement.AppendAligned, 0, InputClassification.PerVertexData, 0)
			};

			_sceneInputLayout = new InputLayout(_device, vsResult.Bytecode, layout);

			SetShaderToScene();
		}

		private void LoadDebugShaders()
		{
			using var includeMan = new DefaultIncludeHandler();
			_debugVertexShader?.Dispose();
			_debugPixelShader?.Dispose();
			_debugInputLayout?.Dispose();

			var macros = new ShaderMacro[]
			{
				new("RS_OIT", (OITCapable && _oitEnabled) ? "1" : "0")
			};

			CompilationResult vsResult = ShaderBytecode.CompileFromFile("Shaders\\debug_vs.hlsl", "main", "vs_5_0", include: includeMan, defines: macros);

			if (vsResult.HasErrors || !string.IsNullOrEmpty(vsResult.Message))
			{
				throw new Exception(vsResult.Message);
			}

			_debugVertexShader = new VertexShader(_device, vsResult.Bytecode);

			CompilationResult psResult = ShaderBytecode.CompileFromFile("Shaders\\debug_ps.hlsl", "main", "ps_5_0", include: includeMan, defines: macros);

			if (psResult.HasErrors || !string.IsNullOrEmpty(psResult.Message))
			{
				throw new Exception(psResult.Message);
			}

			_debugPixelShader = new PixelShader(_device, psResult.Bytecode);

			var layout = new InputElement[]
			{
				new("POSITION", 0, Format.R32G32B32_Float,    InputElement.AppendAligned, 0, InputClassification.PerVertexData, 0),
				new("COLOR",    0, Format.R32G32B32A32_Float, InputElement.AppendAligned, 0, InputClassification.PerVertexData, 0)
			};

			_debugInputLayout = new InputLayout(_device, vsResult.Bytecode, layout);
		}

		public void LoadShaders()
		{
			OITInitialize();
			LoadOITCompositeShader();

			LoadSceneShaders();
			LoadDebugShaders();
		}

		private ShaderResourceView?  _fragListHeadSRV;
		private Texture2D?           _fragListHead;
		private UnorderedAccessView? _fragListHeadUAV;

		private Texture2D?           _fragListCount;
		private ShaderResourceView?  _fragListCountSRV;
		private UnorderedAccessView? _fragListCountUAV;

		private Buffer?              _fragListNodes;
		private ShaderResourceView?  _fragListNodesSRV;
		private UnorderedAccessView? _fragListNodesUAV;

		private void FragListHead_Init()
		{
			CoreExtensions.DisposeAndNullify(ref _fragListHead);
			CoreExtensions.DisposeAndNullify(ref _fragListHeadSRV);
			CoreExtensions.DisposeAndNullify(ref _fragListHeadUAV);

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
				Width             = _viewport.Width,
				Height            = _viewport.Height,
				MipLevels         = 1,
				SampleDescription = { Count = 1, Quality = 0 }
			};

			_fragListHead = new Texture2D(_device, textureDescription);

			var resourceViewDescription = new ShaderResourceViewDescription
			{
				Format    = textureDescription.Format,
				Dimension = ShaderResourceViewDimension.Texture2D,
				Texture2D = { MipLevels = 1, MostDetailedMip = 0 }
			};

			_fragListHeadSRV = new ShaderResourceView(_device, _fragListHead, resourceViewDescription);

			var uavDescription = new UnorderedAccessViewDescription
			{
				Format    = textureDescription.Format,
				Dimension = UnorderedAccessViewDimension.Texture2D,
				Buffer =
				{
					FirstElement = 0,
					ElementCount = _viewport.Width * _viewport.Height,
					Flags        = 0
				}
			};

			_fragListHeadUAV = new UnorderedAccessView(_device, _fragListHead, uavDescription);
		}

		private void FragListCount_Init()
		{
			CoreExtensions.DisposeAndNullify(ref _fragListCount);
			CoreExtensions.DisposeAndNullify(ref _fragListCountSRV);
			CoreExtensions.DisposeAndNullify(ref _fragListCountUAV);

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
				Width             = _viewport.Width,
				Height            = _viewport.Height,
				MipLevels         = 1,
				SampleDescription = { Count = 1, Quality = 0 }
			};

			_fragListCount = new Texture2D(_device, textureDescription);

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

			_fragListCountSRV = new ShaderResourceView(_device, _fragListCount, resourceViewDescription);

			var uavDescription = new UnorderedAccessViewDescription
			{
				Format    = textureDescription.Format,
				Dimension = UnorderedAccessViewDimension.Texture2D,
				Buffer =
				{
					FirstElement = 0,
					ElementCount = _viewport.Width * _viewport.Height,
					Flags        = 0
				}
			};

			_fragListCountUAV = new UnorderedAccessView(_device, _fragListCount, uavDescription);
		}

		private void FragListNodes_Init()
		{
			CoreExtensions.DisposeAndNullify(ref _fragListNodes);
			CoreExtensions.DisposeAndNullify(ref _fragListNodesSRV);
			CoreExtensions.DisposeAndNullify(ref _fragListNodesUAV);

			if (!_oitEnabled)
			{
				return;
			}

			const int maxFragments  = 32;    // TODO: configurable maxFragments
			const int sizeOfOITNode = 4 * 4; // TODO: sizeof(OITNode)

			_perSceneData.BufferLength.Value = (uint)_viewport.Width * (uint)_viewport.Height * maxFragments;

			var bufferDescription = new BufferDescription
			{
				OptionFlags         = ResourceOptionFlags.BufferStructured,
				BindFlags           = BindFlags.UnorderedAccess | BindFlags.ShaderResource,
				SizeInBytes         = sizeOfOITNode * _viewport.Width * _viewport.Height * maxFragments,
				StructureByteStride = sizeOfOITNode
			};

			_fragListNodes = new Buffer(_device, bufferDescription);

			var resourceViewDescription = new ShaderResourceViewDescription
			{
				Format    = Format.Unknown,
				Dimension = ShaderResourceViewDimension.Buffer,
				Buffer =
				{
					ElementCount = _viewport.Width * _viewport.Height * maxFragments
				}
			};

			_fragListNodesSRV = new ShaderResourceView(_device, _fragListNodes, resourceViewDescription);

			var uavDescription = new UnorderedAccessViewDescription
			{
				Format    = Format.Unknown,
				Dimension = UnorderedAccessViewDimension.Buffer,
				Buffer =
				{
					FirstElement = 0,
					ElementCount = _viewport.Width * _viewport.Height * maxFragments,
					Flags        = UnorderedAccessViewBufferFlags.Counter
				}
			};

			_fragListNodesUAV = new UnorderedAccessView(_device, _fragListNodes, uavDescription);
		}

		private readonly UnorderedAccessView[] _nullUaVs = new UnorderedAccessView[3];

		private void OITRead()
		{
			if (!OITCapable || !_oitEnabled)
			{
				return;
			}

			DeviceContext context = _device.ImmediateContext;

			// SharpDX does not have SetRenderTargetsAndUnorderedAccessViews
			context.OutputMerger.SetRenderTargets(depthStencilView: null, renderTargetView: _backBuffer);
			context.OutputMerger.SetUnorderedAccessViews(startSlot: 1, unorderedAccessViews: _nullUaVs);

			ShaderResourceView?[] srvs =
			{
				_fragListHeadSRV,
				_fragListCountSRV,
				_fragListNodesSRV,
				_compositeSRV,
				_depthShaderResource
			};

			context.PixelShader.SetShaderResources(0, srvs);
		}

		private void OITWrite()
		{
			if (!OITCapable)
			{
				return;
			}

			DeviceContext context = _device.ImmediateContext;

			// Unbinds the shader resource views for our fragment list and list head.
			// UAVs cannot be bound as standard resource views and UAVs simultaneously.
			var resourceViews = new ShaderResourceView[5];
			context.PixelShader.SetShaderResources(0, resourceViews);

			var unorderedViews = new UnorderedAccessView?[]
			{
				_fragListHeadUAV,
				_fragListCountUAV,
				_fragListNodesUAV
			};

			// This is used to set the hidden counter of FragListNodes to 0.
			// It only works on FragListNodes, but the number of elements here
			// must match the number of UAVs given.
			int[] zero = { 0, 0, 0 };

			// Bind our fragment list & list head UAVs for read/write operations.
			context.OutputMerger.SetRenderTargets(_depthView, _oitEnabled ? _compositeView : _backBuffer);
			context.OutputMerger.SetUnorderedAccessViews(1, unorderedViews, zero);

			// Resets the list head indices to OIT_FRAGMENT_LIST_NULL.
			// 4 elements are required as this can be used to clear a texture
			// with 4 color channels, even though our list head only has one.
			context.ClearUnorderedAccessView(_fragListHeadUAV, new RawInt4(-1, -1, -1, -1));

			context.ClearUnorderedAccessView(_fragListCountUAV, new RawInt4(0, 0, 0, 0));
		}

		private void OITInitialize()
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

		private void OITComposite()
		{
			if (!OITCapable || !_oitEnabled)
			{
				return;
			}

			using DepthStencilState depthState = _device.ImmediateContext.OutputMerger.GetDepthStencilState(out int stencilRefRef);
			// TODO: disable culling

			using BlendState blendState = _device.ImmediateContext.OutputMerger.BlendState;
			_device.ImmediateContext.OutputMerger.BlendState = null;

			SetShaderToOITComposite();
			OITRead();

			// Draw 3 points. The composite shader will use SV_VertexID to
			// automatically produce a triangle that fills the whole screen.
			_device.ImmediateContext.Draw(3, 0);

			_device.ImmediateContext.OutputMerger.SetDepthStencilState(depthState, stencilRefRef);
			// TODO: restore culling
			_device.ImmediateContext.OutputMerger.BlendState = blendState;

			SetShaderToScene();
		}

		private void OITRelease()
		{
			if (!OITCapable)
			{
				return;
			}

			UnorderedAccessView?[] nullViews = { null, null, null, null, null };

			DeviceContext context = _device.ImmediateContext;

			context.OutputMerger.SetRenderTargets(_depthView, _backBuffer);
			context.OutputMerger.SetUnorderedAccessViews(1, nullViews);

			CoreExtensions.DisposeAndNullify(ref _fragListHeadSRV);
			CoreExtensions.DisposeAndNullify(ref _fragListHead);
			CoreExtensions.DisposeAndNullify(ref _fragListHeadUAV);
			CoreExtensions.DisposeAndNullify(ref _fragListCount);
			CoreExtensions.DisposeAndNullify(ref _fragListCountSRV);
			CoreExtensions.DisposeAndNullify(ref _fragListCountUAV);
			CoreExtensions.DisposeAndNullify(ref _fragListNodes);
			CoreExtensions.DisposeAndNullify(ref _fragListNodesSRV);
			CoreExtensions.DisposeAndNullify(ref _fragListNodesUAV);
		}

		public void Clear()
		{
			_meshQueue.Clear();

			_device.ImmediateContext.Rasterizer.State = _rasterizerState;
			_device.ImmediateContext.ClearRenderTargetView(_oitEnabled ? _compositeView : _backBuffer, new RawColor4(0.0f, 1.0f, 1.0f, 1.0f));

#if REVERSE_Z
			device.ImmediateContext.ClearDepthStencilView(depthView, DepthStencilClearFlags.Depth, 0.0f, 0);
#else
			_device.ImmediateContext.ClearDepthStencilView(_depthView, DepthStencilClearFlags.Depth, 1.0f, 0);
#endif
		}

		public void Draw(Camera camera, NJS_OBJECT? @object)
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
				_meshQueue.Enqueue(this, camera, @object, model, set);
			}
		}

		private void DrawSet(NJS_MODEL parent, NJS_MESHSET set)
		{
			List<NJS_MATERIAL> mats = parent.mats;

			ushort materialId = set.MaterialId;
			NJS_MATERIAL njMaterial = mats.Count > 0 && materialId < mats.Count ? mats[materialId] : NullMaterial;

			FlowControl flowControl = FlowControl;

			if (_texturePool.Count < 1)
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

			if (state.Blend != _lastBlend)
			{
				_device.ImmediateContext.OutputMerger.SetBlendState(state.Blend);
				_lastBlend = state.Blend;
			}

			if (state.Sampler != _lastSamplerState)
			{
				_device.ImmediateContext.PixelShader.SetSampler(0, state.Sampler);
				_lastSamplerState = state.Sampler;
			}

			if (state.Raster != _lastRasterizerState)
			{
				_device.ImmediateContext.Rasterizer.State = state.Raster;
				_lastRasterizerState = state.Raster;
			}

			RenderTargetBlendDescription desc = state.Blend.Description.RenderTarget[0];

			_perModelData.DrawCall.Value += 1;
			_perModelData.DrawCall.Value %= ushort.MaxValue + 1;

			_perModelData.SourceBlend.Value      = (uint)desc.SourceBlend;
			_perModelData.DestinationBlend.Value = (uint)desc.DestinationBlend;
			_perModelData.BlendOperation.Value   = (uint)desc.BlendOperation;

			_perModelData.IsStandardBlending.Value = (desc.SourceBlend == BlendOption.SourceAlpha || desc.SourceBlend == BlendOption.One) &&
			                                        (desc.DestinationBlend == BlendOption.InverseSourceAlpha || desc.DestinationBlend == BlendOption.Zero);

			CommitPerModelData();

			if (parent.VertexBuffer != _lastVertexBuffer)
			{
				var binding = new VertexBufferBinding(parent.VertexBuffer, Vertex.SizeInBytes, 0);
				_device.ImmediateContext.InputAssembler.SetVertexBuffers(0, binding);
				_lastVertexBuffer = parent.VertexBuffer;
			}

			_device.ImmediateContext.InputAssembler.SetIndexBuffer(set.IndexBuffer, Format.R16_UInt, 0);
			_device.ImmediateContext.DrawIndexed(set.IndexCount, 0, 0);
		}

		private void CommitPerModelData()
		{
			if (!_perModelData.Modified)
			{
				return;
			}

			_device.ImmediateContext.MapSubresource(_perModelBuffer, MapMode.WriteDiscard,
			                                       MapFlags.None, out DataStream stream);

			Matrix wvMatrixInvT = _perModelData.World.Value * _perSceneData.View.Value;
			Matrix.Invert(ref wvMatrixInvT, out wvMatrixInvT);

			_perModelData.wvMatrixInvT.Value = wvMatrixInvT;

			using (stream)
			{
				var writer = new CBufferStreamWriter(stream);
				_perModelData.Write(writer);
				//Debug.Assert(stream.RemainingLength == 0);
			}

			_device.ImmediateContext.UnmapSubresource(_perModelBuffer, 0);
			_perModelData.Clear();
		}

		private void CommitPerSceneData()
		{
			if (!_perSceneData.Modified)
			{
				return;
			}

			_device.ImmediateContext.MapSubresource(_perSceneBuffer, MapMode.WriteDiscard,
			                                       MapFlags.None, out DataStream stream);

			using (stream)
			{
				var writer = new CBufferStreamWriter(stream);
				_perSceneData.Write(writer);
				Debug.Assert(stream.RemainingLength == 0);
			}

			_perSceneData.Clear();
			_device.ImmediateContext.UnmapSubresource(_perSceneBuffer, 0);
		}

		public void Present(Camera camera) // TODO: don't pass camera to present - maybe store the camera as part of the draw queue
		{
			int visibleCount = 0;
			_materialData.WriteDepth.Value = true;

			_perSceneData.CameraPosition.Value = camera.Position;
			CommitPerSceneData();

			//meshTree.SortOpaque();

			foreach (MeshsetQueueElement e in _meshQueue.OpaqueSets)
			{
				++visibleCount;
				DrawMeshsetQueueElement(e);
			}

			if (EnableAlpha && _meshQueue.AlphaSets.Count > 1)
			{
				if (_oitEnabled)
				{
					// Enable z-write so that if we find "transparent" geometry which
					// is actually opaque, we can force it into the opaque back buffer
					// to avoid extra work in the composite stage.
					_device.ImmediateContext.OutputMerger.SetDepthStencilState(_depthStateRw);
					_materialData.WriteDepth.Value = true;

					// Now simply draw
					foreach (MeshsetQueueElement e in _meshQueue.AlphaSets)
					{
						DrawMeshsetQueueElement(e);
					}
				}
				else
				{
					_meshQueue.SortAlpha();

					// First draw with depth writes enabled & alpha threshold (in shader)
					foreach (MeshsetQueueElement e in _meshQueue.AlphaSets)
					{
						DrawMeshsetQueueElement(e);
					}

					// Now draw with depth writes disabled
					_device.ImmediateContext.OutputMerger.SetDepthStencilState(_depthStateRo);
					_materialData.WriteDepth.Value = false;

					foreach (MeshsetQueueElement e in _meshQueue.AlphaSets)
					{
						++visibleCount;
						DrawMeshsetQueueElement(e);
					}

					_device.ImmediateContext.OutputMerger.SetDepthStencilState(_depthStateRw);
					_materialData.WriteDepth.Value = true;
				}
			}

			DrawDebugHelpers();
			_meshQueue.Clear();

			if (_oitEnabled)
			{
				OITComposite();
				_swapChain.Present(0, 0);
				OITWrite();
			}
			else
			{
				_swapChain.Present(0, 0);
			}

			if (!MatrixStack.Empty)
			{
				throw new Exception("Matrix stack still contains data");
			}

			// Used for debugging.
			// ReSharper disable once RedundantCheckBeforeAssignment
			if (visibleCount != _lastVisibleCount)
			{
				_lastVisibleCount = visibleCount;
				Debug.WriteLine($"Visible: {visibleCount}");
			}

			_lastVertexBuffer = null;
		}

		private static void WriteToStream(in DebugPoint point, DataStream stream)
		{
			stream.Write(point.Point);

			Color4 color = point.Color;

			stream.Write(color.Red);
			stream.Write(color.Green);
			stream.Write(color.Blue);
			stream.Write(1.0f);
		}

		private static void WriteToStream(in DebugLine line, DataStream stream)
		{
			WriteToStream(in line.PointA, stream);
			WriteToStream(in line.PointB, stream);
		}

		private void WriteToStream(in DebugWireCube cube, DataStream stream)
		{
			foreach (DebugLine line in cube.Lines)
			{
				WriteToStream(in line, stream);
			}
		}

		private void DrawDebugHelpers()
		{
			if (_debugLines.Count == 0 && _debugWireCubes.Count == 0)
			{
				return;
			}

			CommitPerSceneData();

			SetShaderToDebug();

			// TODO: make debug z-writes (and tests) configurable
			_device.ImmediateContext.OutputMerger.SetDepthStencilState(_depthStateRw);
			_materialData.WriteDepth.Value = true;

			// using these variables we're able to batch lines into
			// the cube-sized vertex buffer to reduce draw calls.
			int lineCount = _debugLines.Count;
			int lineIndex = 0;

			while (lineCount > 0)
			{
				int n = Math.Min(lineCount, 12);
				_device.ImmediateContext.MapSubresource(_debugHelperVertexBuffer, MapMode.WriteDiscard, MapFlags.None, out DataStream stream);

				using (stream)
				{
					for (int i = 0; i < n; i++)
					{
						DebugLine line = _debugLines[lineIndex++];
						WriteToStream(in line, stream);
						--lineCount;
					}
				}

				_device.ImmediateContext.UnmapSubresource(_debugHelperVertexBuffer, 0);
				_device.ImmediateContext.Draw(2 * n, 0);
			}

			foreach (DebugWireCube cube in _debugWireCubes)
			{
				_device.ImmediateContext.MapSubresource(_debugHelperVertexBuffer, MapMode.WriteDiscard, MapFlags.None, out DataStream stream);

				using (stream)
				{
					WriteToStream(in cube, stream);
				}

				_device.ImmediateContext.UnmapSubresource(_debugHelperVertexBuffer, 0);
				_device.ImmediateContext.Draw(24, 0);
			}

			_debugWireCubes.Clear();
			_debugLines.Clear();

			SetShaderToScene();
		}

		private void DrawMeshsetQueueElement(MeshsetQueueElement e)
		{
			FlowControl = e.FlowControl;

			Matrix m = e.Transform;
			SetTransform(TransformState.World, in m);

			if (!e.Object.SkipDraw)
			{
				DrawSet(e.Model, e.Set);
			}
		}

		private void CreateOITCompositeTexture(Texture2DDescription textureDescription)
		{
			CoreExtensions.DisposeAndNullify(ref _compositeTexture);
			CoreExtensions.DisposeAndNullify(ref _compositeView);
			CoreExtensions.DisposeAndNullify(ref _compositeSRV);

			textureDescription.Usage     = ResourceUsage.Default;
			textureDescription.BindFlags = BindFlags.RenderTarget | BindFlags.ShaderResource;

			_compositeTexture = new Texture2D(_device, textureDescription);

			var viewDescription = new RenderTargetViewDescription
			{
				Format    = textureDescription.Format,
				Dimension = RenderTargetViewDimension.Texture2D,
				Texture2D = { MipSlice = 0 }
			};

			_compositeView = new RenderTargetView(_device, _compositeTexture, viewDescription);

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

			_compositeSRV = new ShaderResourceView(_device, _compositeTexture, srvDescription);
		}

		private void CreateRenderTarget()
		{
			Texture2DDescription description;

			using (var pBackBuffer = Resource.FromSwapChain<Texture2D>(_swapChain, 0))
			{
				CoreExtensions.DisposeAndNullify(ref _backBuffer);
				_backBuffer = new RenderTargetView(_device, pBackBuffer);

				description = pBackBuffer.Description;
			}

			CreateOITCompositeTexture(description);

			_device.ImmediateContext.OutputMerger.SetRenderTargets(_backBuffer);
		}

		private void SetViewPort(int x, int y, int width, int height)
		{
			_viewport.MinDepth = 0f;
			_viewport.MaxDepth = 1f;

			Viewport vp = _viewport;

			vp.X      = x;
			vp.Y      = y;
			vp.Width  = width;
			vp.Height = height;

			if (vp == _viewport)
			{
				return;
			}

			_viewport = vp;
			_device.ImmediateContext.Rasterizer.SetViewport(_viewport);
		}

		public void Draw(IEnumerable<MeshsetQueueElementBase> visible, Camera camera)
		{
			foreach (MeshsetQueueElement e in visible.Select(x => new MeshsetQueueElement(x, camera)))
			{
				_meshQueue.Enqueue(e);
			}
		}

		public void RefreshDevice(int w, int h)
		{
			_backBuffer?.Dispose();
			_swapChain.ResizeBuffers(1, w, h, Format.Unknown, 0);

			SetViewPort(0, 0, w, h);

			CreateRenderTarget();
			CreateRasterizerState();
			CreateDepthStencil(w, h);

			OITRelease();
			OITInitialize();
		}

		private void CreateDepthStencil(int w, int h)
		{
			const Format textureFormat = Format.R24G8_Typeless;
			const Format viewFormat    = Format.R24_UNorm_X8_Typeless;
			const Format depthFormat   = Format.D24_UNorm_S8_UInt;

			CoreExtensions.DisposeAndNullify(ref _depthTexture);
			CoreExtensions.DisposeAndNullify(ref _depthStateRw);
			CoreExtensions.DisposeAndNullify(ref _depthStateRo);
			CoreExtensions.DisposeAndNullify(ref _depthView);
			CoreExtensions.DisposeAndNullify(ref _depthShaderResource);

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

			_depthTexture = new Texture2D(_device, depthTextureDesc);

			_depthDesc = new DepthStencilStateDescription
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

			_depthStateRw = new DepthStencilState(_device, _depthDesc);

			_depthDesc.DepthWriteMask = DepthWriteMask.Zero;

			_depthStateRo = new DepthStencilState(_device, _depthDesc);

			var depthViewDesc = new DepthStencilViewDescription
			{
				Format    = depthFormat,
				Dimension = DepthStencilViewDimension.Texture2D,
				Texture2D = new DepthStencilViewDescription.Texture2DResource
				{
					MipSlice = 0
				}
			};

			_depthView = new DepthStencilView(_device, _depthTexture, depthViewDesc);

			_device.ImmediateContext.OutputMerger.SetTargets(_depthView, _backBuffer);
			_device.ImmediateContext.OutputMerger.SetDepthStencilState(_depthStateRw);

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

			_depthShaderResource = new ShaderResourceView(_device, _depthTexture, resourceViewDescription);
		}

		private void CreateRasterizerState()
		{
			_rasterizerDescription = new RasterizerStateDescription
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

			_rasterizerState?.Dispose();
			_rasterizerState = new RasterizerState(_device, _rasterizerDescription);

			_device.ImmediateContext.Rasterizer.State = _rasterizerState;
		}

		private void CopyToTexture(Texture2D texture, Bitmap bitmap, int level)
		{
			BitmapData bmpData = bitmap.LockBits(new System.Drawing.Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
			byte[] buffer = new byte[bmpData.Stride * bitmap.Height];
			Marshal.Copy(bmpData.Scan0, buffer, 0, buffer.Length);
			bitmap.UnlockBits(bmpData);

			CopyToTexture(texture, bitmap.Width, bitmap.Height, buffer, level);
		}

		private void CopyToTexture(Texture2D texture, int width, int height, byte[] data, int level)
		{
			_device.ImmediateContext.UpdateSubresource(data, texture, level, 4 * width, 4 * height);
		}

		public void CreateTextureFromRawTextures(IReadOnlyList<RawTexture> rawTextures)
		{
			var description = new Texture2DDescription
			{
				ArraySize         = 1,
				BindFlags         = BindFlags.ShaderResource,
				CpuAccessFlags    = CpuAccessFlags.Write,
				Format            = Format.B8G8R8A8_UNorm,
				Width             = rawTextures[0].Width,
				Height            = rawTextures[0].Height,
				MipLevels         = rawTextures.Count,
				Usage             = ResourceUsage.Default,
				SampleDescription = new SampleDescription(1, 0)
			};

			var texture = new Texture2D(_device, description);

			for (int i = 0; i < rawTextures.Count; ++i)
			{
				RawTexture rawTexture = rawTextures[i];
				CopyToTexture(texture, rawTexture.Width, rawTexture.Height, rawTexture.Data, i);
			}

			var pair = new SceneTexture(texture, new ShaderResourceView(_device, texture));
			_texturePool.Add(pair);
		}

		public void CreateTextureFromBitmaps(IReadOnlyList<Bitmap> bitmaps)
		{
			var description = new Texture2DDescription
			{
				ArraySize         = 1,
				BindFlags         = BindFlags.ShaderResource,
				CpuAccessFlags    = CpuAccessFlags.Write,
				Format            = Format.B8G8R8A8_UNorm,
				Width             = bitmaps[0].Width,
				Height            = bitmaps[0].Height,
				MipLevels         = bitmaps.Count,
				Usage             = ResourceUsage.Default,
				SampleDescription = new SampleDescription(1, 0)
			};

			var texture = new Texture2D(_device, description);

			for (int i = 0; i < bitmaps.Count; ++i)
			{
				CopyToTexture(texture, bitmaps[i], i);
			}

			var pair = new SceneTexture(texture, new ShaderResourceView(_device, texture));
			_texturePool.Add(pair);
		}

		// TODO: generate mipmaps mod loader style
		public void CreateTextureFromBitmapSource(BitmapSource bitmapSource)
		{
			int stride = bitmapSource.Size.Width * 4;

			using var buffer = new DataStream(bitmapSource.Size.Height * stride, true, true);

			bitmapSource.CopyPixels(stride, buffer);

			var description = new Texture2DDescription
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
			};

			var texture = new Texture2D(_device, description, new DataRectangle(buffer.DataPointer, stride));

			_texturePool.Add(new SceneTexture(texture, new ShaderResourceView(_device, texture)));
		}

		public void ClearTexturePool()
		{
			foreach (SceneTexture texture in _texturePool)
			{
				texture.Dispose();
			}

			_texturePool.Clear();
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
			return new Buffer(_device, stream, desc);
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
			return new Buffer(_device, stream, desc);
		}

		public void SetTransform(TransformState state, in Matrix m)
		{
			switch (state)
			{
				case TransformState.World:
					_perModelData.World.Value = m;
					break;
				case TransformState.View:
					_perSceneData.View.Value = m;
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
					_perSceneData.Projection.Value = m;
#endif
					break;
				default:
					throw new ArgumentOutOfRangeException(nameof(state), state, null);
			}
		}

		public void SetTexture(int sampler, int textureIndex)
		{
			if (textureIndex >= 0 && textureIndex < _texturePool.Count)
			{
				SceneTexture texture = _texturePool[textureIndex];

				if (texture == _lastTexture)
				{
					return;
				}

				_device.ImmediateContext.PixelShader.SetShaderResource(sampler, texture.ShaderResource);
				_lastTexture = texture;
			}
			else if (_lastTexture != null)
			{
				_lastTexture = null;
				_device.ImmediateContext.PixelShader.SetShaderResource(sampler, null);
			}
		}

		// TODO: generate in bulk
		public DisplayState GetSADXDisplayState(NJS_MATERIAL material)
		{
			// Not implemented in SADX:
			// - NJD_FLAG.Pick
			// - NJD_FLAG.UseAnisotropic
			// - NJD_FLAG.UseFlat

			const NJD_FLAG stateMask = NJD_FLAG.ClampU | NJD_FLAG.ClampV | NJD_FLAG.FlipU | NJD_FLAG.FlipV
			                            | NJD_FLAG.DoubleSide | NJD_FLAG.UseAlpha
			                            | (NJD_FLAG)0xFC000000 /* blend modes */;

			NJD_FLAG flags = FlowControl.Apply(material.attrflags) & stateMask;

			if (DefaultCullMode == CullMode.None)
			{
				flags |= NJD_FLAG.DoubleSide;
			}

			if (_displayStates.TryGetValue(flags, out DisplayState? state))
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

			var sampler = new SamplerState(_device, samplerDesc) { DebugName = $"Sampler: {flags.ToString()}" };

			// Base it off of the default rasterizer state.
			RasterizerStateDescription rasterDesc = _rasterizerDescription;

			rasterDesc.CullMode = (flags & NJD_FLAG.DoubleSide) != 0 ? CullMode.None : DefaultCullMode;

			var raster = new RasterizerState(_device, rasterDesc) { DebugName = $"Rasterizer: {flags.ToString()}" };

			var blendDesc = new BlendStateDescription();
			ref RenderTargetBlendDescription rt = ref blendDesc.RenderTarget[0];

			rt.IsBlendEnabled        = (flags & NJD_FLAG.UseAlpha) != 0;
			rt.SourceBlend           = BlendModes[material.SourceBlend];
			rt.DestinationBlend      = BlendModes[material.DestinationBlend];
			rt.BlendOperation        = BlendOperation.Add;
			rt.SourceAlphaBlend      = BlendOption.Zero;
			rt.DestinationAlphaBlend = BlendOption.Zero;
			rt.AlphaBlendOperation   = BlendOperation.Add;
			rt.RenderTargetWriteMask = ColorWriteMaskFlags.All;

			var blend = new BlendState(_device, blendDesc) { DebugName = $"Blend: {flags.ToString()}" };

			var result = new DisplayState(sampler, raster, blend);
			_displayStates[flags] = result;

			return result;
		}

		public void SetSceneMaterial(in SceneMaterial material)
		{
			_materialData.Material.Value = material;

			if (!_materialData.Modified)
			{
				return;
			}

			_materialData.Clear();

			_device.ImmediateContext.MapSubresource(_materialBuffer, MapMode.WriteDiscard, MapFlags.None, out DataStream stream);

			using (stream)
			{
				var writer = new CBufferStreamWriter(stream);
				_materialData.Write(writer);
			}

			_device.ImmediateContext.UnmapSubresource(_materialBuffer, 0);
		}

		public void Dispose()
		{
			_swapChain.Dispose();
			_backBuffer?.Dispose();

			_depthTexture?.Dispose();
			_depthStateRw?.Dispose();
			_depthStateRo?.Dispose();
			_depthView?.Dispose();

			_rasterizerState?.Dispose();

			_materialBuffer.Dispose();

			_sceneVertexShader?.Dispose();
			_scenePixelShader?.Dispose();

			_oitCompositeVertexShader?.Dispose();
			_oitCompositePixelShader?.Dispose();

			_debugVertexShader?.Dispose();
			_debugPixelShader?.Dispose();

			_sceneInputLayout?.Dispose();

			_debugInputLayout?.Dispose();
			_debugHelperVertexBuffer.Dispose();

			_perSceneBuffer.Dispose();
			_perModelBuffer.Dispose();
			_materialBuffer.Dispose();

			CoreExtensions.DisposeAndNullify(ref _fragListHeadSRV);
			CoreExtensions.DisposeAndNullify(ref _fragListHead);
			CoreExtensions.DisposeAndNullify(ref _fragListHeadUAV);
			CoreExtensions.DisposeAndNullify(ref _fragListCount);
			CoreExtensions.DisposeAndNullify(ref _fragListCountSRV);
			CoreExtensions.DisposeAndNullify(ref _fragListCountUAV);
			CoreExtensions.DisposeAndNullify(ref _fragListNodes);
			CoreExtensions.DisposeAndNullify(ref _fragListNodesSRV);
			CoreExtensions.DisposeAndNullify(ref _fragListNodesUAV);

			ClearTexturePool();
			ClearDisplayStates();

#if DEBUG
			using (var debug = new DeviceDebug(_device))
			{
				debug.ReportLiveDeviceObjects(ReportingLevel.Summary);
			}
#endif

			_device.Dispose();
		}

		private void ClearDisplayStates()
		{
			foreach (KeyValuePair<NJD_FLAG, DisplayState> i in _displayStates)
			{
				i.Value.Dispose();
			}

			_displayStates.Clear();
		}

		public void DrawDebugLine(DebugPoint start, DebugPoint end)
		{
			DrawDebugLine(new DebugLine(start, end));
		}

		public void DrawDebugLine(DebugLine line)
		{
			_debugLines.Add(line);
		}

		public void DrawBounds(in BoundingBox bounds, Color4 color)
		{
			_debugWireCubes.Add(new DebugWireCube(bounds, color));
		}
	}

	internal class InsufficientFeatureLevelException : Exception
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