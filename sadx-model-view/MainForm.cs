using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Text;
using PuyoTools.Modules.Archive;
using PuyoTools.Modules.Compression;
using sadx_model_view.Ninja;
using sadx_model_view.SA1;
using SharpDX.Direct3D9;
using SharpDX;
using SharpDX.Mathematics.Interop;
using VrSharp.PvrTexture;

// TODO: Mipmap mode (From Texture, Always On, Always Off, Generate)

namespace sadx_model_view
{
	public partial class MainForm : Form
	{
		private bool initialized;

		// TODO: not this
		public static Cull CullMode = Cull.Counterclockwise;
		// TODO: not this
		public static readonly List<Texture> TexturePool = new List<Texture>();
		// TODO: not this
		public static NJS_SCREEN Screen;

		// SADX's default horizontal field of view.
		private static readonly float fov_h = MathUtil.DegreesToRadians(70);
		// SADX's default vertical field of view (55.412927352596554 degrees)
		private static readonly float fov_v = 2.0f * (float)Math.Atan(Math.Tan(fov_h / 2.0f) * (3.0f / 4.0f));

		private Direct3D direct3d;
		private Device device;
		private PresentParameters present;

		private NJS_OBJECT obj;
		private LandTable landTable;

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

		private Light light = new Light
		{
			Type      = LightType.Directional,
			Ambient   = new RawColor4(1.0f, 1.0f, 1.0f, 1.0f),
			Diffuse   = new RawColor4(1.0f, 1.0f, 1.0f, 1.0f),
			Specular  = new RawColor4(1.0f, 1.0f, 1.0f, 0.0f),
			Direction = new RawVector3(0.0f, -1.0f, 0.0f)
		};

		private Camera camera = new Camera();

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
				var directory = Path.GetDirectoryName(dialog.FileName) ?? string.Empty;
				string[] index = File.ReadAllLines(dialog.FileName);

				foreach (var line in index)
				{
					var i = line.LastIndexOf(",", StringComparison.Ordinal);

					var filename = Path.Combine(directory, line.Substring(++i));

					if (!File.Exists(filename))
						continue;

					var texture = Texture.FromFile(device, filename, Usage.None, Pool.Managed);
					TexturePool.Add(texture);
				}
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

		void CopyToTexture(Texture texture, Bitmap bitmap, int level)
		{
			DataStream data;
			texture.LockRectangle(level, LockFlags.Discard, out data);

			var bmpData = bitmap.LockBits(new System.Drawing.Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);

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
			texture.UnlockRectangle(level);
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
				PvrTexture pvr = new PvrTexture(entry.Open());
				Bitmap[] mipmaps = null;
				Bitmap bitmap;

				int levels = 1;
				Usage usage = Usage.None;

				if (pvr.HasMipmaps)
				{
					mipmaps = pvr.MipmapsToBitmap();
					usage = Usage.None;
					levels = mipmaps.Length;
					bitmap = mipmaps[0];
				}
				else
				{
					bitmap = pvr.ToBitmap();
				}

				var texture = new Texture(device, bitmap.Width, bitmap.Height, levels, usage, Format.A8R8G8B8, Pool.Managed);

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
			present = new PresentParameters(scene.ClientRectangle.Width, scene.ClientRectangle.Height)
			{
				SwapEffect             = SwapEffect.Discard,
				BackBufferCount        = 1,
				BackBufferFormat       = Format.X8R8G8B8,
				EnableAutoDepthStencil = true,
				AutoDepthStencilFormat = Format.D24X8,
				PresentationInterval   = PresentInterval.Immediate,
				Windowed               = true
			};

			direct3d = new Direct3D();
			UpdatePresentParameters();
			device = new Device(direct3d, 0, DeviceType.Hardware, scene.Handle, CreateFlags.HardwareVertexProcessing, present);

			RefreshDevice();
			scene.SizeChanged += OnSizeChanged;
		}

		private void RefreshDevice()
		{
			UpdatePresentParameters();

			if (initialized)
			{
				device.Reset(present);
			}

			initialized = true;
			UpdateCamera();
			SetupScene();
		}

		private void UpdatePresentParameters()
		{
			present.BackBufferWidth = scene.ClientRectangle.Width;
			present.BackBufferHeight = scene.ClientRectangle.Height;

			var width = (float)scene.ClientRectangle.Width;
			var height = (float)scene.ClientRectangle.Height;
			var ratio = width / height;

			var fov = fov_v;
			var h = 2 * (float)Math.Atan(Math.Tan(fov_v / 2.0f) * ratio);

			if (h < fov_h)
			{
				fov = 2 * (float)Math.Atan(Math.Tan(fov_h / 2.0f) * (height / width));
			}

			const float defaultRatio = 4.0f / 3.0f;

			if (height * defaultRatio == width || height * defaultRatio > width)
			{
				var tan = 2.0f * (float)Math.Tan(h / 2.0f);
				Screen.dist = width / tan;
			}
			else
			{
				var tan = 2.0f * (float)Math.Tan(fov / 2.0f);
				Screen.dist = height / tan;
			}

			Screen.w = width;
			Screen.h = height;
			Screen.cx = width / 2.0f;
			Screen.cy = height / 2.0f;

			camera.SetProjection(fov, ratio, -1.0f, -2300.0f);
		}

		private void SetupScene()
		{
			device.SetRenderState(RenderState.ZEnable,          true);
			device.SetRenderState(RenderState.CullMode,         CullMode);
			device.SetRenderState(RenderState.AlphaBlendEnable, false);
			device.SetRenderState(RenderState.SourceBlend,      Blend.SourceAlpha);
			device.SetRenderState(RenderState.DestinationBlend, Blend.InverseSourceAlpha);
			device.SetRenderState(RenderState.AlphaFunc,        Compare.GreaterEqual);

			device.SetRenderState(RenderState.Ambient, unchecked((int)0xFF888888));

			device.SetRenderState(RenderState.ColorVertex, true);
			device.SetRenderState(RenderState.AmbientMaterialSource,  ColorSource.Color1);
			device.SetRenderState(RenderState.DiffuseMaterialSource,  ColorSource.Color1);
			device.SetRenderState(RenderState.SpecularMaterialSource, ColorSource.Material);

			device.SetSamplerState(0, SamplerState.MagFilter, TextureFilter.Linear);
			device.SetSamplerState(0, SamplerState.MinFilter, TextureFilter.Linear);
			device.SetSamplerState(0, SamplerState.MipFilter, TextureFilter.Linear);

			device.SetTextureStageState(0, TextureStage.ColorOperation, TextureOperation.Modulate);
			device.SetTextureStageState(0, TextureStage.ColorArg1,      TextureOperation.SelectArg1);
			device.SetTextureStageState(0, TextureStage.ColorArg2,      TextureOperation.Disable);
			device.SetTextureStageState(0, TextureStage.AlphaOperation, TextureOperation.Modulate);
			device.SetTextureStageState(0, TextureStage.AlphaArg1,      TextureOperation.SelectArg1);
			device.SetTextureStageState(0, TextureStage.AlphaArg2,      TextureOperation.Disable);

			device.SetTransform(TransformState.World, Matrix.Identity);
			UpdateCamera();

			device.SetLight(0, ref light);
			device.EnableLight(0, true);
		}

		private float speed = 0.5f;

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
			device.SetTransform(TransformState.View, camera.View);
			device.SetTransform(TransformState.Projection, camera.Projection);
		}

		// TODO: conditional render (only render when the scene has been invalidated)
		public void MainLoop()
		{
			DeltaTime.Update();
			if (WindowState == FormWindowState.Minimized)
				return;

			device.Clear(ClearFlags.Target | ClearFlags.ZBuffer, new ColorBGRA(0, 191, 191, 255), 1.0f, 0);
			device.BeginScene();

			SetupScene();
			obj?.Draw(device, ref camera);
			landTable?.Draw(device, ref camera);

			device.EndScene();
			device.Present();
		}

		private void OnSizeChanged(object sender, EventArgs e)
		{
			if (WindowState == FormWindowState.Minimized)
				return;
			RefreshDevice();
		}

		private void OnClosed(object sender, FormClosedEventArgs e)
		{
			device.Dispose();
			direct3d.Dispose();
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

		private CamControls camcontrols = CamControls.None;

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
					CullMode = (CullMode == Cull.Counterclockwise) ? Cull.None : Cull.Counterclockwise;
					device.SetRenderState(RenderState.CullMode, CullMode);
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

		private System.Drawing.Point last_mouse = System.Drawing.Point.Empty;
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
