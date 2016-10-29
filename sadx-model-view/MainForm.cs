using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using System.Text;
using sadx_model_view.Ninja;
using SharpDX.Direct3D9;
using SharpDX;
using SharpDX.Mathematics.Interop;

// TODO: textures (maybe using texture packs for now to be lazy)
// TODO: landtables
// TODO: delta time camera movement

namespace sadx_model_view
{
	public partial class MainForm : Form
	{
		public static Cull CullMode = Cull.Counterclockwise;

		// SADX's default horizontal field of view.
		private static readonly float fov_h = MathUtil.DegreesToRadians(70);
		// SADX's default vertical field of view (55.412927352596554 degrees)
		private static readonly float fov_v = 2.0f * (float)Math.Atan(Math.Tan(fov_h / 2.0f) * (3.0f / 4.0f));

		private Direct3D direct3d;
		private Device device;
		private PresentParameters present;
		private Matrix projection;

		private int step;
		private NJS_OBJECT obj;

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
			Specular  = new RawColor4(1.0f, 1.0f, 1.0f, 1.0f),
			Direction = new RawVector3(0.0f, -1.0f, 0.0f)
		};

		private readonly Camera camera = new Camera();

		public MainForm()
		{
			InitializeComponent();
		}

		private void openToolStripMenuItem_Click(object sender, EventArgs e)
		{
			var dialog = new OpenFileDialog();
			if (dialog.ShowDialog(this) != DialogResult.OK)
				return;

			var whatever = dialog.FileName;
			if (string.Compare(Path.GetExtension(whatever), ".sa1mdl", StringComparison.InvariantCultureIgnoreCase) != 0)
				throw new NotImplementedException();

			using (var file = new FileStream(whatever, FileMode.Open))
			{
				var signature = new byte[6];
				file.Read(signature, 0, 6);
				var str = Encoding.UTF8.GetString(signature);

				if (str != "SA1MDL")
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
				obj = new NJS_OBJECT(file);
				obj.CommitVertexBuffer(device);
				obj.CalculateRadius();

				camera.Position = obj.pos;
				camera.Translate(Vector3.BackwardLH, obj.Radius * 2.0f);
				camera.LookAt(obj.pos);

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

								var i = ReadString(file, ref buffer);

								file.Position = pos;
								var name = Encoding.UTF8.GetString(buffer, 0, i);
								labels.Add(new KeyValuePair<uint, string>(addr, name));
							}
							break;

						case ChunkTypes.Animations:
							if (size == 0)
								break;

							// ReSharper disable once RedundantAssignment
							animations = Encoding.UTF8.GetString(buffer, 0, ReadString(file, ref buffer));
							break;

						case ChunkTypes.Morphs:
							throw new NotImplementedException(ChunkTypes.Morphs.ToString());

						case ChunkTypes.Author:
							if (size == 0)
								break;

							// ReSharper disable once RedundantAssignment
							author = Encoding.UTF8.GetString(buffer, 0, ReadString(file, ref buffer));
							break;

						case ChunkTypes.Tool:
							if (size == 0)
								break;

							// ReSharper disable once RedundantAssignment
							tool = Encoding.UTF8.GetString(buffer, 0, ReadString(file, ref buffer));
							break;

						case ChunkTypes.Description:
							if (size == 0)
								break;

							// ReSharper disable once RedundantAssignment
							description = Encoding.UTF8.GetString(buffer, 0, ReadString(file, ref buffer));
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

		/// <summary>
		/// Reads a null terminated string from <paramref name="stream"/> into <paramref name="buffer"/>.
		/// </summary>
		/// <param name="stream">The stream to read from.</param>
		/// <param name="buffer">The buffer to output to.</param>
		/// <returns>The length of the string.</returns>
		private static int ReadString(Stream stream, ref byte[] buffer)
		{
			int i = 0;
			do
			{
				stream.Read(buffer, i, 1);
			} while (buffer[i++] != 0);
			return i > 0 ? i - 1 : i;
		}

		private void OnShown(object sender, EventArgs e)
		{
			present = new PresentParameters(ClientSize.Width, ClientSize.Height)
			{
				SwapEffect             = SwapEffect.Discard,
				BackBufferCount        = 1,
				BackBufferFormat       = Format.X8R8G8B8,
				EnableAutoDepthStencil = true,
				AutoDepthStencilFormat = Format.D24X8,
				PresentationInterval   = PresentInterval.One
			};

			direct3d = new Direct3D();
			device = new Device(direct3d, 0, DeviceType.Hardware, Handle, CreateFlags.HardwareVertexProcessing, present);

			RefreshDevice();
		}

		private void RefreshDevice()
		{
			present.BackBufferWidth  = ClientSize.Width;
			present.BackBufferHeight = ClientSize.Height;

			var width  = (float)ClientSize.Width;
			var height = (float)ClientSize.Height;
			var ratio  = width / height;

			var fov = fov_v;
			var h = 2 * (float)Math.Atan(Math.Tan(fov_v / 2.0f) * ratio);

			if (h < fov_h)
			{
				fov = 2 * (float)Math.Atan(Math.Tan(fov_h / 2.0f) * (height / width));
			}

			Matrix.PerspectiveFovLH(fov, ratio, 0.1f, 10000.0f, out projection);
			SetViewMatrix();

			device.Reset(present);

			SetupScene();
		}

		private void SetupScene()
		{
			device.SetRenderState(RenderState.ZEnable,          true);
			device.SetRenderState(RenderState.CullMode,         CullMode);
			device.SetRenderState(RenderState.AlphaBlendEnable, true);
			device.SetRenderState(RenderState.SourceBlend,      Blend.SourceAlpha);
			device.SetRenderState(RenderState.DestinationBlend, Blend.InverseSourceAlpha);
			device.SetRenderState(RenderState.AlphaFunc,        Compare.Equal);
			device.SetRenderState(RenderState.AlphaRef,         255);

			device.SetTransform(TransformState.Projection, projection);
			device.SetTransform(TransformState.World,      Matrix.Identity);
			SetViewMatrix();

			device.SetLight(0, ref light);
			device.EnableLight(0, true);
		}

		private void SetViewMatrix()
		{
			if (camcontrols != CamControls.None)
			{
				Vector3 v = new Vector3();

				if (camcontrols.HasFlag(CamControls.Forward))
				{
					v.Z += 1.0f;
				}
				if (camcontrols.HasFlag(CamControls.Backward))
				{
					v.Z -= 1.0f;
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

				camera.Translate(v, 2.0f);
			}

			camera.UpdateMatrix();
			device.SetTransform(TransformState.View, camera.Matrix);
		}

		public void MainLoop()
		{
			device.Clear(ClearFlags.Target | ClearFlags.ZBuffer, new ColorBGRA(0, 191, 191, 255), 1.0f, 0);
			device.BeginScene();

			SetupScene();
			obj?.Draw(device);

			device.EndScene();
			device.Present();

			step = step + 1 % 256;
		}

		private void OnSizeChanged(object sender, EventArgs e)
		{
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

		private void MainForm_KeyDown(object sender, KeyEventArgs e)
		{
			switch (e.KeyCode)
			{
				case Keys.F:
					if (obj != null)
						camera.LookAt(obj.pos);
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

		private void MainForm_KeyUp(object sender, KeyEventArgs e)
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
		private void MainForm_MouseMove(object sender, MouseEventArgs e)
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
	}
}
