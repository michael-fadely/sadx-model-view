using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using PuyoTools.Modules.Archive;
using PuyoTools.Modules.Compression;
using sadx_model_view.Ninja;
using sadx_model_view.SA1;
using SharpDX;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using SharpDX.Mathematics.Interop;
using VrSharp.PvrTexture;
using Device = SharpDX.Direct3D11.Device;
using MapFlags = SharpDX.Direct3D11.MapFlags;
using Resource = SharpDX.Direct3D11.Resource;

// TODO: Mipmap mode (From Texture, Always On, Always Off, Generate)

namespace sadx_model_view
{
	public partial class MainForm : Form
	{
		private float speed = 0.5f;
		private System.Drawing.Point last_mouse = System.Drawing.Point.Empty;
		private CamControls camcontrols = CamControls.None;

		// TODO: not this
		public static CullMode CullMode = CullMode.Back;
		// TODO: not this
		public static readonly List<Texture2D> TexturePool = new List<Texture2D>();
		// TODO: not this
		public static NJS_SCREEN Screen;

		// SADX's default horizontal field of view.
		private static readonly float fov_h = MathUtil.DegreesToRadians(70);
		// SADX's default vertical field of view (55.412927352596554 degrees)
		private static readonly float fov_v = 2.0f * (float)Math.Atan(Math.Tan(fov_h / 2.0f) * (3.0f / 4.0f));

		private NJS_OBJECT obj;
		private LandTable landTable;

		private Device device;
		private SwapChain swapChain;
		private RenderTargetView backBuffer;
		private Viewport viewPort;
		private Texture2D depthStencilBuffer;
		private DepthStencilStateDescription depthStencilDesc;
		private DepthStencilState depthStencilState;
		private DepthStencilView depthStencil;

		private enum ChunkTypes : uint
		{
			Label       = 0x4C42414C,
			Animations  = 0x4D494E41,
			Morphs      = 0x46524F4D,
			Author      = 0x48545541,
			Tool        = 0x4C4F4F54,
			Description = 0x43534544,
			Texture     = 0x584554,
			End         = 0x444E45
		}

		private Camera camera = new Camera();
		private RasterizerState renderState;
		private RasterizerStateDescription rasterDesc;

		public MainForm()
		{
			InitializeComponent();
		}

		private void openToolStripMenuItem_Click(object sender, EventArgs e)
		{
			var dialog = openModelDialog;
			if (dialog.ShowDialog(this) != DialogResult.OK)
			{
				return;
			}

			ClearTexturePool();

			using (var file = new FileStream(dialog.FileName, FileMode.Open))
			{
				var signature = new byte[6];
				file.Read(signature, 0, 6);
				var signatureStr = Encoding.UTF8.GetString(signature);

				if (signatureStr != "SA1MDL" && signatureStr != "SA1LVL")
					throw new NotImplementedException();

				var buffer = new byte[4096];
				file.Position += 1;
				file.Read(buffer, 0, 1);

				if (buffer[0] != 3)
					throw new NotImplementedException();

				file.Read(buffer, 0, sizeof(int) * 2);

				var object_ptr = BitConverter.ToUInt32(buffer, 0);
				var metadata_ptr = BitConverter.ToUInt32(buffer, 4);

				file.Position = object_ptr;

				obj?.Dispose();
				obj = null;

				landTable?.Dispose();
				landTable = null;

				switch (signatureStr)
				{
					case "SA1MDL":
						obj = ObjectCache.FromStream(file, object_ptr);
						obj.CommitVertexBuffer(device);
						obj.CalculateRadius();

						camera.Position = obj.Position;
						camera.Translate(Vector3.BackwardRH, obj.Radius * 2.0f);
						camera.LookAt(obj.Position);
						break;

					case "SA1LVL":
						landTable = new LandTable(file);
						landTable.CommitVertexBuffer(device);
						break;

					default:
						throw new NotImplementedException(signatureStr);
				}

				ObjectCache.Clear();
				ModelCache.Clear();

				if (metadata_ptr == 0)
					return;

				file.Position = metadata_ptr;
				bool done = false;

				// ReSharper disable once CollectionNeverQueried.Local
				var labels = new List<KeyValuePair<uint, string>>();
				// ReSharper disable once NotAccessedVariable
				var description = string.Empty;
				// ReSharper disable once NotAccessedVariable
				var tool = string.Empty;
				// ReSharper disable once NotAccessedVariable
				var animations = string.Empty;
				// ReSharper disable once NotAccessedVariable
				var author = string.Empty;

				while (!done)
				{
					file.Read(buffer, 0, 8);
					var offset = file.Position;
					var type = (ChunkTypes)BitConverter.ToUInt32(buffer, 0);
					var size = BitConverter.ToInt32(buffer, 4);

					switch (type)
					{
						case ChunkTypes.Label:
							while (true)
							{
								file.Read(buffer, 0, 8);
								var addr = BitConverter.ToUInt32(buffer, 0);

								if (addr == 0xFFFFFFFF)
									break;

								var name_addr = BitConverter.ToUInt32(buffer, 4);

								if (name_addr == 0 || name_addr == 0xFFFFFFFF)
									break;

								var pos = file.Position;
								file.Position = offset + name_addr;

								var i = file.ReadString(ref buffer);

								file.Position = pos;
								var name = Encoding.UTF8.GetString(buffer, 0, i);
								labels.Add(new KeyValuePair<uint, string>(addr, name));
							}
							break;

						case ChunkTypes.Animations:
							if (size == 0)
								break;

							// ReSharper disable once RedundantAssignment
							animations = Encoding.UTF8.GetString(buffer, 0, file.ReadString(ref buffer));
							break;

						case ChunkTypes.Morphs:
							throw new NotImplementedException(ChunkTypes.Morphs.ToString());

						case ChunkTypes.Author:
							if (size == 0)
								break;

							// ReSharper disable once RedundantAssignment
							author = Encoding.UTF8.GetString(buffer, 0, file.ReadString(ref buffer));
							break;

						case ChunkTypes.Tool:
							if (size == 0)
								break;

							// ReSharper disable once RedundantAssignment
							tool = Encoding.UTF8.GetString(buffer, 0, file.ReadString(ref buffer));
							break;

						case ChunkTypes.Description:
							if (size == 0)
								break;

							// ReSharper disable once RedundantAssignment
							description = Encoding.UTF8.GetString(buffer, 0, file.ReadString(ref buffer));
							break;

						case ChunkTypes.Texture:
							throw new NotImplementedException(ChunkTypes.Texture.ToString());

						case ChunkTypes.End:
							done = true;
							break;

						default:
							throw new ArgumentOutOfRangeException();
					}

					file.Position = offset + size;
				}

#if false
				MessageBox.Show(this, $"Description: {description}"
					+ $"\nTool: {tool}"
					+ $"\nAuthor: {author}"
					+ $"\nAnimations: {animations}");

				var thing = string.Join(" | ", (from x in labels select $"{x.Key}: {x.Value}"));

				MessageBox.Show(this, $"Labels:\n{thing}");
#endif
			}
		}

		private void openTexturesToolStripMenuItem_Click(object sender, EventArgs e)
		{
			var dialog = openTexturesDialog;
			if (dialog.ShowDialog(this) != DialogResult.OK)
			{
				return;
			}

			var extension = Path.GetExtension(dialog.FileName);

			if (extension == null)
			{
				MessageBox.Show(this, "no extension wtf", "wtf");
				return;
			}

			ClearTexturePool();

			if (extension.Equals(".txt", StringComparison.OrdinalIgnoreCase))
			{
				MessageBox.Show(this, "Not yet implemented", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
				/*
				var directory = Path.GetDirectoryName(dialog.FileName) ?? string.Empty;
				string[] index = File.ReadAllLines(dialog.FileName);

				foreach (var line in index)
				{
					int i = line.LastIndexOf(",", StringComparison.Ordinal);

					string filename = Path.Combine(directory, line.Substring(++i));

					if (!File.Exists(filename))
					{
						continue;
					}

					var texture = Texture.FromFile(device, filename, Usage.None, Pool.Managed);
					TexturePool.Add(texture);
				}
				*/
			}
			else if (extension.Equals(".prs", StringComparison.OrdinalIgnoreCase))
			{
				LoadPRS(dialog.FileName);
			}
			else if (extension.Equals(".pvm", StringComparison.OrdinalIgnoreCase))
			{
				LoadPVM(dialog.FileName);
			}
		}

		private void LoadPRS(string fileName)
		{
			var prs = new PrsCompression();

			using (var stream = new MemoryStream())
			{
				using (var file = new FileStream(fileName, FileMode.Open))
				{
					prs.Decompress(file, stream);
				}

				stream.Position = 0;
				LoadPVM(stream);
			}
		}

		private void LoadPVM(string fileName)
		{
			using (var file = new FileStream(fileName, FileMode.Open))
			{
				LoadPVM(file);
			}
		}

		void CopyToTexture(Texture2D texture, Bitmap bitmap, int level)
		{
			DataStream data;
			device.ImmediateContext.MapSubresource(texture, level, MapMode.WriteDiscard, MapFlags.None, out data);

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

		private void LoadPVM(Stream stream)
		{
			var pvm = new PvmArchive();

			if (!pvm.Is(stream, string.Empty))
			{
				MessageBox.Show("nope");
			}

			foreach (ArchiveEntry entry in pvm.Open(stream).Entries)
			{
				var pvr = new PvrTexture(entry.Open());
				Bitmap[] mipmaps = null;
				Bitmap bitmap;

				int levels = 1;

				if (pvr.HasMipmaps)
				{
					mipmaps = pvr.MipmapsToBitmap();
					levels = mipmaps.Length;
					bitmap = mipmaps[0];
				}
				else
				{
					bitmap = pvr.ToBitmap();
				}

				var texDesc = new Texture2DDescription
				{
					ArraySize         = 1,
					BindFlags         = BindFlags.ShaderResource,
					CpuAccessFlags    = CpuAccessFlags.Write,
					Format            = Format.R8G8B8A8_UNorm,
					Width             = bitmap.Width,
					Height            = bitmap.Height,
					MipLevels         = levels,
					Usage             = ResourceUsage.Dynamic,
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
		}

		private static void ClearTexturePool()
		{
			foreach (var texture in TexturePool)
			{
				texture.Dispose();
			}

			TexturePool.Clear();
		}

		private void OnShown(object sender, EventArgs e)
		{
			int w = scene.ClientRectangle.Width;
			int h = scene.ClientRectangle.Height;

			var desc = new SwapChainDescription
			{
				BufferCount     = 1,
				ModeDescription = new ModeDescription(w, h, new Rational(1000, 60), Format.R8G8B8A8_UNorm),
				Usage           = Usage.RenderTargetOutput,
				OutputHandle    = scene.Handle,
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
				MessageBox.Show(this,
					"Your GPU does not meet the minimum required feature level. (Direct3D 10.0)",
					"GPU TOO OLD FAM",
					MessageBoxButtons.OK);

				Close();
				return;
			}

			RefreshDevice();
			scene.SizeChanged += OnSizeChanged;
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

			vp.X      = x;
			vp.Y      = y;
			vp.Width  = width;
			vp.Height = height;

			if (vp == viewPort)
			{
				return;
			}

			viewPort = vp;
			device.ImmediateContext.Rasterizer.SetViewport(viewPort);
		}

		private void RefreshDevice()
		{
			int w = scene.ClientRectangle.Width;
			int h = scene.ClientRectangle.Height;

			backBuffer?.Dispose();
			swapChain?.ResizeBuffers(1, w, h, Format.Unknown, 0);
			SetViewPort(0, 0, w, h);

			CreateDepthStencil();
			CreateRenderTarget();
			CreateRasterizerState();
			UpdateProjection();
			UpdateCamera();
		}

		private void UpdateProjection()
		{
			float width = scene.ClientRectangle.Width;
			float height = scene.ClientRectangle.Height;
			float ratio = width / height;

			float fov = fov_v;
			float h = 2 * (float)Math.Atan(Math.Tan(fov_v / 2.0f) * ratio);

			if (h < fov_h)
			{
				fov = 2 * (float)Math.Atan(Math.Tan(fov_h / 2.0f) * (height / width));
			}

			const float defaultRatio = 4.0f / 3.0f;

			if (height * defaultRatio == width || height * defaultRatio > width)
			{
				float tan = 2.0f * (float)Math.Tan(h / 2.0f);
				Screen.dist = width / tan;
			}
			else
			{
				float tan = 2.0f * (float)Math.Tan(fov / 2.0f);
				Screen.dist = height / tan;
			}

			Screen.w = width;
			Screen.h = height;
			Screen.cx = width / 2.0f;
			Screen.cy = height / 2.0f;

			camera.SetProjection(fov, ratio, -1.0f, -2300.0f);
		}

		private void CreateDepthStencil()
		{
			int w = scene.ClientRectangle.Width;
			int h = scene.ClientRectangle.Height;

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

		private void UpdateCamera()
		{
			if (camcontrols != CamControls.None)
			{
				var v = new Vector3();

				if (camcontrols.HasFlag(CamControls.Forward))
				{
					v.Z -= 1.0f;
				}
				if (camcontrols.HasFlag(CamControls.Backward))
				{
					v.Z += 1.0f;
				}

				if (camcontrols.HasFlag(CamControls.Right))
				{
					v.X += 1.0f;
				}
				if (camcontrols.HasFlag(CamControls.Left))
				{
					v.X -= 1.0f;
				}

				if (camcontrols.HasFlag(CamControls.Up))
				{
					v.Y += 1.0f;
				}
				if (camcontrols.HasFlag(CamControls.Down))
				{
					v.Y -= 1.0f;
				}

				camera.Translate(v, speed * DeltaTime.Delta);
			}

			if (!camera.Invalid)
			{
				return;
			}

			camera.Update();
		}

		// TODO: conditional render (only render when the scene has been invalidated)
		public void MainLoop()
		{
			DeltaTime.Update();
			if (WindowState == FormWindowState.Minimized)
				return;

			device?.ImmediateContext.ClearRenderTargetView(backBuffer, new RawColor4(1.0f, 1.0f, 1.0f, 1.0f));
			device?.ImmediateContext.ClearDepthStencilView(depthStencil, DepthStencilClearFlags.Depth, 1.0f, 0);

			obj?.Draw(device, ref camera);

			if (landTable != null)
			{
				FlowControl.UseMaterialFlags = true;
				FlowControl.Add(0, NJD_FLAG.IgnoreSpecular);
				landTable.Draw(device, ref camera);
				FlowControl.Reset();
			}

			swapChain.Present(1, 0);
		}

		private void OnSizeChanged(object sender, EventArgs e)
		{
			if (WindowState == FormWindowState.Minimized)
				return;
			RefreshDevice();
		}

		private void OnClosed(object sender, FormClosedEventArgs e)
		{
			device?.Dispose();
		}

		[Flags]
		private enum CamControls
		{
			None,
			Forward  = 1 << 0,
			Backward = 1 << 1,
			Left     = 1 << 2,
			Right    = 1 << 3,
			Up       = 1 << 4,
			Down     = 1 << 5,
			Look     = 1 << 6
		}

		private void scene_KeyDown(object sender, KeyEventArgs e)
		{
			switch (e.KeyCode)
			{
				case Keys.Subtract:
					speed -= 0.125f;
					break;

				case Keys.Add:
					speed += 0.125f;
					break;

				case Keys.F:
					if (obj != null)
						camera.LookAt(obj.Position);
					break;

				case Keys.C:
					CullMode = (CullMode == CullMode.Back) ? CullMode.None : CullMode.Back;
					// TODO device.SetRenderState(RenderState.CullMode, CullMode);
					break;

				case Keys.W:
					camcontrols |= CamControls.Forward;
					break;

				case Keys.S:
					camcontrols |= CamControls.Backward;
					break;

				case Keys.A:
					camcontrols |= CamControls.Left;
					break;

				case Keys.D:
					camcontrols |= CamControls.Right;
					break;

				case Keys.Up:
					camcontrols |= CamControls.Up;
					break;

				case Keys.Down:
					camcontrols |= CamControls.Down;
					break;

				case Keys.Space:
					camcontrols |= CamControls.Look;
					break;
			}
		}

		private void scene_KeyUp(object sender, KeyEventArgs e)
		{
			switch (e.KeyCode)
			{
				case Keys.W:
					camcontrols &= ~CamControls.Forward;
					break;

				case Keys.S:
					camcontrols &= ~CamControls.Backward;
					break;

				case Keys.A:
					camcontrols &= ~CamControls.Left;
					break;

				case Keys.D:
					camcontrols &= ~CamControls.Right;
					break;

				case Keys.Up:
					camcontrols &= ~CamControls.Up;
					break;

				case Keys.Down:
					camcontrols &= ~CamControls.Down;
					break;

				case Keys.Space:
					camcontrols &= ~CamControls.Look;
					break;
			}
		}

		private void scene_MouseMove(object sender, MouseEventArgs e)
		{
			var delta = new System.Drawing.Point(e.Location.X - last_mouse.X, e.Location.Y - last_mouse.Y);
			last_mouse = e.Location;

			if (!camcontrols.HasFlag(CamControls.Look))
			{
				return;
			}

			Vector3 rotation;
			rotation.Y = (float)(Math.PI * (delta.X / (float)ClientRectangle.Width));
			rotation.X = (float)(Math.PI * (delta.Y / (float)ClientRectangle.Height));
			rotation.Z = 0.0f;
			camera.Rotate(rotation);
		}

		private void sortToolStripMenuItem_Click(object sender, EventArgs e)
		{
			if (obj != null)
			{
				obj.Sort();
				obj.CommitVertexBuffer(device);
				obj.CalculateRadius();
			}

			if (landTable != null)
			{
				landTable.Sort();
				landTable.CommitVertexBuffer(device);
			}
		}
	}
}
