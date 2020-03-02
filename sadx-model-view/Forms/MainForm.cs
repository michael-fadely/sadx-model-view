using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using PuyoTools.Modules.Archive;
using PuyoTools.Modules.Compression;
using sadx_model_view.Extensions;
using sadx_model_view.Extensions.SharpDX.Mathematics.Collision;
using sadx_model_view.Ninja;
using sadx_model_view.SA1;
using SharpDX;
using SharpDX.Direct3D11;
using SharpDX.WIC;
using VrSharp.PvrTexture;
using Bitmap = System.Drawing.Bitmap;

// TODO: Mipmap mode (From Texture, Always On, Always Off, Generate)

namespace sadx_model_view.Forms
{
	public partial class MainForm : Form
	{
		float speed = 0.5f;
		System.Drawing.Point last_mouse = System.Drawing.Point.Empty;
		CamControls camcontrols = CamControls.None;

		VisibilityTree objectTree, landTableTree;
		BoundsOctree<ObjectTriangles> triangleTree;
		Renderer renderer;

		// SADX's default horizontal field of view.
		static readonly float fov_h = MathUtil.DegreesToRadians(70);
		// SADX's default vertical field of view (55.412927352596554 degrees)
		static readonly float fov_v = 2.0f * (float)Math.Atan(Math.Tan(fov_h / 2.0f) * (3.0f / 4.0f));

		NJS_OBJECT obj;
		LandTable landTable;

		enum ChunkTypes : uint
		{
			Label = 0x4C42414C,
			Animations = 0x4D494E41,
			[Obsolete("Superseded by animations; never used.")]
			Morphs = 0x46524F4D,
			Author = 0x48545541,
			Tool = 0x4C4F4F54,
			Description = 0x43534544,
			[Obsolete("This type was never implemented.")]
			Texture = 0x584554,
			End = 0x444E45
		}

		readonly Camera camera = new Camera();

		public MainForm()
		{
			InitializeComponent();
		}

		void openToolStripMenuItem_Click(object sender, EventArgs e)
		{
			OpenFileDialog dialog = openModelDialog;
			if (dialog.ShowDialog(this) != DialogResult.OK)
			{
				return;
			}

			renderer.ClearTexturePool();

			using var file = new FileStream(dialog.FileName, FileMode.Open);
			var signature = new byte[6];
			file.Read(signature, 0, 6);
			string signatureStr = Encoding.UTF8.GetString(signature);

			if (signatureStr != "SA1MDL" && signatureStr != "SA1LVL")
			{
				throw new NotImplementedException();
			}

			var buffer = new byte[4096];
			file.Position += 1;
			file.Read(buffer, 0, 1);

			if (buffer[0] != 3)
			{
				throw new NotImplementedException();
			}

			file.Read(buffer, 0, sizeof(int) * 2);

			uint object_ptr   = BitConverter.ToUInt32(buffer, 0);
			uint metadata_ptr = BitConverter.ToUInt32(buffer, 4);

			file.Position = object_ptr;

			obj?.Dispose();
			obj = null;

			landTable?.Dispose();
			landTable = null;

			objectTree    = null;
			landTableTree = null;

			switch (signatureStr)
			{
				case "SA1MDL":
				{
					obj = ObjectCache.FromStream(file, object_ptr);
					obj.CommitVertexBuffer(renderer);
					obj.CalculateRadius();

					camera.Position = obj.Position;
					camera.Translate(Vector3.BackwardRH, obj.Radius * 2.0f);
					camera.LookAt(obj.Position);

					objectTree = new VisibilityTree(obj);

					List<ObjectTriangles> triangles = new List<ObjectTriangles>();

					foreach (NJS_OBJECT o in obj)
					{
						if (o.Model == null)
						{
							continue;
						}

						triangles.Add(o.GetTriangles());
					}

					BoundingBox bb = BoundingBox.FromPoints(triangles.SelectMany(x => x.Triangles).SelectMany(x => x.ToArray()).ToArray());
					triangleTree = new BoundsOctree<ObjectTriangles>(bb, 0.1f, 1.0f);

					foreach (ObjectTriangles pair in triangles)
					{
						if (pair.Triangles.Count == 0)
						{
							continue;
						}

						Vector3[] a = pair.Triangles.SelectMany(x => x.ToArray()).ToArray();
						bb = BoundingBox.FromPoints(a);
						triangleTree.Add(pair, bb);
					}

					break;
				}

				case "SA1LVL":
				{
					landTable = new LandTable(file);
					landTable.CommitVertexBuffer(renderer);
					landTableTree = new VisibilityTree(landTable);

					List<ObjectTriangles> triangles = landTable.GetTriangles().ToList();
					BoundingBox bb = BoundingBox.FromPoints(triangles.SelectMany(x => x.Triangles).SelectMany(x => x.ToArray()).ToArray());
					triangleTree = new BoundsOctree<ObjectTriangles>(bb, 0.1f, 1.0f);

					foreach (ObjectTriangles pair in triangles)
					{
						if (pair.Triangles.Count == 0)
						{
							continue;
						}

						Vector3[] a = pair.Triangles.SelectMany(x => x.ToArray()).ToArray();
						bb = BoundingBox.FromPoints(a);
						triangleTree.Add(pair, bb);
					}

					break;
				}

				default:
					throw new NotImplementedException(signatureStr);
			}

			ObjectCache.Clear();
			ModelCache.Clear();

			if (metadata_ptr == 0)
			{
				return;
			}

			file.Position = metadata_ptr;
			var done = false;

			// ReSharper disable once CollectionNeverQueried.Local
			var labels = new List<KeyValuePair<uint, string>>();
			// ReSharper disable once NotAccessedVariable
			string description = string.Empty;
			// ReSharper disable once NotAccessedVariable
			string tool = string.Empty;
			// ReSharper disable once NotAccessedVariable
			string animations = string.Empty;
			// ReSharper disable once NotAccessedVariable
			string author = string.Empty;

			while (!done)
			{
				file.Read(buffer, 0, 8);
				long offset = file.Position;
				var type = (ChunkTypes)BitConverter.ToUInt32(buffer, 0);
				int size = BitConverter.ToInt32(buffer, 4);

				switch (type)
				{
					case ChunkTypes.Label:
						while (true)
						{
							file.Read(buffer, 0, 8);
							uint addr = BitConverter.ToUInt32(buffer, 0);

							if (addr == 0xFFFFFFFF)
							{
								break;
							}

							uint name_addr = BitConverter.ToUInt32(buffer, 4);

							if (name_addr == 0 || name_addr == 0xFFFFFFFF)
							{
								break;
							}

							long pos = file.Position;
							file.Position = offset + name_addr;

							int i = file.ReadString(ref buffer);

							file.Position = pos;
							string name = Encoding.UTF8.GetString(buffer, 0, i);
							labels.Add(new KeyValuePair<uint, string>(addr, name));
						}
						break;

					case ChunkTypes.Animations:
						if (size == 0)
						{
							break;
						}

						// ReSharper disable once RedundantAssignment
						animations = Encoding.UTF8.GetString(buffer, 0, file.ReadString(ref buffer));
						break;

					case ChunkTypes.Author:
						if (size == 0)
						{
							break;
						}

						// ReSharper disable once RedundantAssignment
						author = Encoding.UTF8.GetString(buffer, 0, file.ReadString(ref buffer));
						break;

					case ChunkTypes.Tool:
						if (size == 0)
						{
							break;
						}

						// ReSharper disable once RedundantAssignment
						tool = Encoding.UTF8.GetString(buffer, 0, file.ReadString(ref buffer));
						break;

					case ChunkTypes.Description:
						if (size == 0)
						{
							break;
						}

						// ReSharper disable once RedundantAssignment
						description = Encoding.UTF8.GetString(buffer, 0, file.ReadString(ref buffer));
						break;

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

		void openTexturesToolStripMenuItem_Click(object sender, EventArgs e)
		{
			OpenFileDialog dialog = openTexturesDialog;
			if (dialog.ShowDialog(this) != DialogResult.OK)
			{
				return;
			}

			string extension = Path.GetExtension(dialog.FileName);

			if (extension == null)
			{
				MessageBox.Show(this, "no extension wtf", "wtf");
				return;
			}

			renderer.ClearTexturePool();

			if (extension.Equals(".txt", StringComparison.OrdinalIgnoreCase))
			{
				LoadTextureIndex(dialog.FileName);
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

		void LoadTextureIndex(string fileName)
		{
			using var factory = new ImagingFactory2();
			string   directory = Path.GetDirectoryName(fileName) ?? string.Empty;
			string[] index     = File.ReadAllLines(fileName);

			var lineNumber = 0;
			foreach (string line in index)
			{
				++lineNumber;

				int i = line.LastIndexOf(",", StringComparison.Ordinal);

				string texturePath = Path.Combine(directory, line.Substring(++i));

				if (!File.Exists(texturePath))
				{
					MessageBox.Show($"Missing texture on line {lineNumber}: {texturePath}");
					renderer.ClearTexturePool();
					break;
				}

				using var decoder = new BitmapDecoder(factory, texturePath, DecodeOptions.CacheOnDemand);
				using var converter = new FormatConverter(factory);
				converter.Initialize(decoder.GetFrame(0), PixelFormat.Format32bppPRGBA, BitmapDitherType.None, null, 0.0, BitmapPaletteType.Custom);
				renderer.CreateTextureFromBitmapSource(converter);
			}
		}

		void LoadPRS(string fileName)
		{
			var prs = new PrsCompression();

			using var stream = new MemoryStream();
			using (var file = new FileStream(fileName, FileMode.Open))
			{
				prs.Decompress(file, stream);
			}

			stream.Position = 0;
			LoadPVM(stream);
		}

		void LoadPVM(string fileName)
		{
			using var file = new FileStream(fileName, FileMode.Open);
			LoadPVM(file);
		}

		void LoadPVM(Stream stream)
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

				var levels = 1;

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

				renderer.CreateTextureFromBitMap(bitmap, mipmaps, levels);
			}
		}

		void OnShown(object sender, EventArgs e)
		{
#if DEBUG
			showOctreeToolStripMenuItem.Checked = true;
#endif

			int w = scene.ClientRectangle.Width;
			int h = scene.ClientRectangle.Height;

			try
			{
				renderer = new Renderer(w, h, scene.Handle);
			}
			catch (InsufficientFeatureLevelException)
			{
				MessageBox.Show(this,
				                "Your GPU does not meet the minimum required feature level. (Direct3D 10.0)",
				                "GPU TOO OLD FAM",
				                MessageBoxButtons.OK);

				Close();
				return;
			}
			catch (SharpDXException ex)
			{
				MessageBox.Show(this, ex.ToString(), "Something happened.", MessageBoxButtons.OK);
				Close();
				return;
			}
			catch (Exception ex)
			{
				MessageBox.Show(this, ex.Message, "Something happened.", MessageBoxButtons.OK);
				Close();
				return;
			}

			enableOITToolStripMenuItem.Enabled = renderer.OitCapable;

			UpdateProjection();
			scene.SizeChanged += OnSizeChanged;
		}

		void UpdateProjection()
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

			camera.SetProjection(fov, ratio, 1.0f, 100000.0f);
		}

		void UpdateCamera()
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

			Matrix m = camera.Projection;
			renderer.SetTransform(TransformState.Projection, in m);
			m = camera.View;
			renderer.SetTransform(TransformState.View, in m);
		}

		Ray lastRay;
		RayHit? lastHit;

		// TODO: conditional render (only render when the scene has been invalidated)
		public void MainLoop()
		{
			DeltaTime.Update();

			if (WindowState == FormWindowState.Minimized)
			{
				return;
			}

			UpdateCamera();

			if (renderer == null)
			{
				return;
			}

			renderer.Clear();

			if (lastHit != null)
			{
				renderer.DrawDebugLine(new DebugLine(new DebugPoint(lastRay.Position, Color.DarkGreen),
				                                     new DebugPoint(lastRay.Position + (lastRay.Direction * lastHit.Value.Distance), Color.DarkGreen)));
			}
			else
			{
				renderer.DrawDebugLine(new DebugLine(new DebugPoint(lastRay.Position, Color.Blue),
				                                     new DebugPoint(lastRay.Position + (lastRay.Direction * 16777215f), Color.Blue)));
			}

			if (obj != null)
			{
				if (objectTree.Empty)
				{
					objectTree.Add(obj, renderer);
				}

				if (showOctreeToolStripMenuItem.Checked)
				{
					foreach (BoundingBox bounds in objectTree.GiveMeTheBounds())
					{
						Color4 color = camera.Frustum.Contains(bounds) == ContainmentType.Contains
							? new Color4(0f, 1f, 0f, 1f)
							: new Color4(1f, 0f, 0f, 1f);
						renderer.DrawBounds(in bounds, color);
					}
				}

				List<MeshsetQueueElementBase> visible = objectTree.GetVisible(camera);
				base.Text = $"{visible.Count}";

				renderer.Draw(visible, camera);
			}

			if (landTable != null)
			{
				renderer.FlowControl.UseMaterialFlags = true;
				renderer.FlowControl.Add(0, NJD_FLAG.IgnoreSpecular);

				if (landTableTree.Empty)
				{
					landTableTree.Add(landTable, renderer);
				}

				if (showOctreeToolStripMenuItem.Checked)
				{
					foreach (BoundingBox bounds in landTableTree.GiveMeTheBounds())
					{
						//Color4 color = camera.Frustum.Contains(bounds) == ContainmentType.Contains
						//	? new Color4(0f, 1f, 0f, 1f)
						//	: new Color4(1f, 0f, 0f, 1f);

						//renderer.DrawBounds(in bounds, color);

						if (camera.Frustum.Contains(bounds) == ContainmentType.Contains)
						{
							renderer.DrawBounds(in bounds, new Color4(1.0f, 0.0f, 1.0f, 1.0f));
						}
						else
						{
							renderer.DrawBounds(in bounds, new Color4(1.0f, 0.0f, 0.0f, 1.0f));
						}
					}
				}

				List<MeshsetQueueElementBase> visible = landTableTree.GetVisible(camera);
				base.Text = $"{visible.Count}";

				renderer.Draw(visible, camera);
				renderer.FlowControl.Reset();
			}

			renderer.Present(camera);
		}

		void OnSizeChanged(object sender, EventArgs e)
		{
			if (WindowState == FormWindowState.Minimized)
			{
				return;
			}

			renderer.RefreshDevice(scene.ClientRectangle.Width, scene.ClientRectangle.Height);
			UpdateProjection();
			UpdateCamera();
		}

		void OnClosed(object sender, FormClosedEventArgs e)
		{
			obj?.Dispose();
			landTable?.Dispose();
			renderer?.Dispose();
		}

		[Flags]
		enum CamControls
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

		void scene_KeyDown(object sender, KeyEventArgs e)
		{
			switch (e.KeyCode)
			{
				case Keys.Subtract:
					speed = Math.Max(0.125f, speed - 0.125f);
					break;

				case Keys.Add:
					speed += 0.125f;
					break;

				case Keys.F:
					if (obj != null)
					{
						camera.LookAt(obj.Position);
					}
					break;

				case Keys.C:
					renderer.DefaultCullMode = renderer.DefaultCullMode == CullMode.Back ? CullMode.None : CullMode.Back;
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

		void scene_KeyUp(object sender, KeyEventArgs e)
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

		void scene_MouseMove(object sender, MouseEventArgs e)
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

		void recompileShadersToolStripMenuItem_Click(object sender, EventArgs e)
		{
			while (true)
			{
				try
				{
					renderer?.LoadShaders();
					break;
				}
				catch (Exception ex)
				{
					MessageBox.Show(this, ex.Message, "Shader Compilation Failure", MessageBoxButtons.OK, MessageBoxIcon.Error);
				}
			}
		}

		private void scene_MouseUp(object sender, MouseEventArgs e)
		{
			if (triangleTree == null)
			{
				return;
			}

			var viewport = new ViewportF(0f, 0f, scene.ClientRectangle.Width, scene.ClientRectangle.Height);
			var ray = Ray.GetPickRay(e.X, e.Y, viewport, camera.Frustum.Matrix);

			var colliding = new List<RayCollisionResult<ObjectTriangles>>();
			triangleTree.GetColliding(colliding, in ray);

			lastRay = ray;
			lastHit = null;

			RayCollisionResult<ObjectTriangles>? closestObject = null;
			RayHit closestTriHit = new RayHit(Vector3.Zero, 16777215f);

			foreach (RayCollisionResult<ObjectTriangles> collider in colliding)
			{
				if (collider.Collider.Object.SkipDraw)
				{
					continue;
				}

				foreach (Triangle triangle in collider.Collider.Triangles)
				{
					if (!ray.IntersectsTriangle(triangle.A, triangle.B, triangle.C, out RayHit hit, doubleSided: false))
					{
						continue;
					}

					if (hit.Distance >= closestTriHit.Distance)
					{
						continue;
					}

					closestTriHit = hit;
					closestObject = collider;
				}
			}

			if (closestObject == null)
			{
				return;
			}

			lastHit = closestTriHit;
			closestObject.Value.Collider.Object.SkipDraw = true;
		}

		private void enableOITToolStripMenuItem_CheckedChanged(object sender, EventArgs e)
		{
			// UNDONE: toggle OIT
		}

		void enableAlphaToolStripMenuItem_CheckedChanged(object sender, EventArgs e)
		{
			if (renderer != null)
			{
				renderer.EnableAlpha = enableAlphaToolStripMenuItem.Checked;
			}
		}
	}
}
