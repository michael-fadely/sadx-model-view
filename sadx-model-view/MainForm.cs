using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
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
				var metadata_ptr  = BitConverter.ToUInt32(buffer, 4);

				file.Position = object_ptr;

				var objects = new NJS_OBJECT(file);
			}
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
			device   = new Device(direct3d, 0, DeviceType.Hardware, Handle, CreateFlags.HardwareVertexProcessing, present);

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
