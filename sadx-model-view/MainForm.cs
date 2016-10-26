using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Text;
using Ninja;
using SharpDX.Direct3D9;
using SharpDX;
using SharpDX.Mathematics.Interop;

namespace sadx_model_view
{
	public partial class MainForm : Form
	{
		private Direct3D direct3d;
		private Device device;
		private PresentParameters present;
		private Matrix projection;
		private int step;
		private NJS_OBJECT obj = null;

		public enum ChunkTypes : uint
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
			Direction = new RawVector3(0.0f, -1.0f, 0.0f)
		};

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
				var str = System.Text.Encoding.UTF8.GetString(signature);

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

				obj = new NJS_OBJECT(file);
				obj.CommitVertexBuffer(device);

				if (metadata_ptr == 0)
					return;

				file.Position = metadata_ptr;
				bool done = false;

				var labels = new List<KeyValuePair<uint, string>>();

				var description = string.Empty;
				var tool        = string.Empty;
				var animations  = string.Empty;
				var author      = string.Empty;

				// TODO: this
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

							animations = Encoding.UTF8.GetString(buffer, 0, ReadString(file, ref buffer));
							break;

						case ChunkTypes.Morphs:
							throw new NotImplementedException(ChunkTypes.Morphs.ToString());

						case ChunkTypes.Author:
							if (size == 0)
								break;

							author = Encoding.UTF8.GetString(buffer, 0, ReadString(file, ref buffer));
							break;

						case ChunkTypes.Tool:
							if (size == 0)
								break;

							tool = Encoding.UTF8.GetString(buffer, 0, ReadString(file, ref buffer));
							break;

						case ChunkTypes.Description:
							if (size == 0)
								break;

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

				MessageBox.Show(this, $"Description: {description}"
					+ $"\nTool: {tool}"
					+ $"\nAuthor: {author}"
					+ $"\nAnimations: {animations}");

				var thing = string.Join(" | ", (from x in labels select $"{x.Key}: {x.Value}"));

				MessageBox.Show(this, $"Labels:\n{thing}");
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
			device.SetRenderState(RenderState.ZEnable, true);
			device.SetRenderState(RenderState.CullMode, Cull.None);
			device.SetRenderState(RenderState.AlphaBlendEnable, true);
			device.SetRenderState(RenderState.SourceBlend, Blend.SourceAlpha);
			device.SetRenderState(RenderState.DestinationBlend, Blend.InverseSourceAlpha);
			device.SetRenderState(RenderState.AlphaFunc, Compare.Equal);
			device.SetRenderState(RenderState.AlphaRef, 255);

			present.BackBufferWidth = ClientSize.Width;
			present.BackBufferHeight = ClientSize.Height;
			var w = (float)ClientSize.Width;
			var h = (float)ClientSize.Height;
			projection = Matrix.PerspectiveFovLH(MathUtil.DegreesToRadians(45), w / h, 0.1f, 10000.0f);

			device.Reset(present);

			device.SetRenderState(RenderState.ZEnable, true);
			device.SetTransform(TransformState.View, Matrix.Identity /*camera.Transform*/);
			device.SetTransform(TransformState.Projection, projection);

			device.SetLight(0, ref light);
			device.EnableLight(0, true);
		}

		public void MainLoop()
		{
			device.Clear(ClearFlags.Target | ClearFlags.ZBuffer, new ColorBGRA(0, (byte)step, (byte)step, 0), 1.0f, 0);
			device.BeginScene();

			device.SetTransform(TransformState.View, Matrix.Identity /*camera.Transform*/);

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
	}
}
