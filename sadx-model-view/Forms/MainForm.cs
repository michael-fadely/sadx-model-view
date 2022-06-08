using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

using PuyoTools.Core.Archives;
using PuyoTools.Core.Compression;
using PuyoTools.Core.Textures.Pvr;

using sadx_model_view.Extensions;
using sadx_model_view.Extensions.SharpDX.Mathematics.Collision;
using sadx_model_view.Ninja;
using sadx_model_view.SA1;

using SharpDX;
using SharpDX.Direct3D11;
using SharpDX.WIC;

// TODO: Mipmap mode (From Texture, Always On, Always Off, Generate)

namespace sadx_model_view.Forms
{
	public partial class MainForm : Form
	{
		private float                _speed          = 30.0f;
		private System.Drawing.Point _lastMouse      = System.Drawing.Point.Empty;
		private CameraControls       _cameraControls = CameraControls.None;

		private VisibilityTree?                _objectTree;
		private VisibilityTree?                _landTableTree;
		private BoundsOctree<ObjectTriangles>? _triangleTree;
		private Renderer?                      _renderer;

		// SADX's default horizontal field of view.
		private static readonly float s_fovX = MathUtil.DegreesToRadians(70);
		// SADX's default vertical field of view (55.412927352596554 degrees)
		private static readonly float s_fovY = 2.0f * MathF.Atan(MathF.Tan(s_fovX / 2.0f) * (3.0f / 4.0f));

		private NJS_OBJECT? _object;
		private LandTable?  _landTable;

		private enum ChunkTypes : uint
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

		private readonly Camera _camera = new Camera();

		public MainForm()
		{
			InitializeComponent();
		}

		private void openToolStripMenuItem_Click(object sender, EventArgs e)
		{
			OpenFileDialog dialog = openModelDialog;
			if (dialog.ShowDialog(this) != DialogResult.OK)
			{
				return;
			}

			_renderer!.ClearTexturePool();

			using var file = new FileStream(dialog.FileName, FileMode.Open);
			byte[] signature = new byte[6];
			file.ReadExact(signature, 0, 6);
			string signatureStr = Encoding.UTF8.GetString(signature);

			if (signatureStr != "SA1MDL" && signatureStr != "SA1LVL")
			{
				throw new NotImplementedException();
			}

			byte[] buffer = new byte[4096];
			file.Position += 1;
			file.ReadExact(buffer, 0, 1);

			if (buffer[0] != 3)
			{
				throw new NotImplementedException();
			}

			file.ReadExact(buffer, 0, sizeof(int) * 2);

			uint objectOffset   = BitConverter.ToUInt32(buffer, 0);
			uint metadataOffset = BitConverter.ToUInt32(buffer, 4);

			file.Position = objectOffset;

			DisposableExtensions.DisposeAndNullify(ref _object);
			DisposableExtensions.DisposeAndNullify(ref _landTable);

			_objectTree    = null;
			_landTableTree = null;

			switch (signatureStr)
			{
				case "SA1MDL":
				{
					_object = ObjectCache.FromStream(file, objectOffset);
					_object.CommitVertexBuffer(_renderer);
					_object.CalculateRadius();

					_camera.Position = _object.Position;
					_camera.Translate(Vector3.BackwardRH, _object.Radius * 2.0f);
					_camera.LookAt(_object.Position);

					_objectTree = new VisibilityTree(_object);

					var triangles = new List<ObjectTriangles>();

					foreach (NJS_OBJECT o in _object)
					{
						if (o.Model == null)
						{
							continue;
						}

						triangles.Add(o.GetTriangles());
					}

					BoundingBox bb = BoundingBox.FromPoints(triangles.SelectMany(x => x.Triangles).SelectMany(x => x.ToArray()).ToArray());
					_triangleTree = new BoundsOctree<ObjectTriangles>(bb, 0.1f, 1.0f);

					foreach (ObjectTriangles pair in triangles)
					{
						if (pair.Triangles.Count == 0)
						{
							continue;
						}

						Vector3[] a = pair.Triangles.SelectMany(x => x.ToArray()).ToArray();
						bb = BoundingBox.FromPoints(a);
						_triangleTree.Add(pair, bb);
					}

					break;
				}

				case "SA1LVL":
				{
					_landTable = new LandTable(file);
					_landTable.CommitVertexBuffer(_renderer);
					_landTableTree = new VisibilityTree(_landTable);

					List<ObjectTriangles> triangles = _landTable.GetTriangles().ToList();
					BoundingBox bb = BoundingBox.FromPoints(triangles.SelectMany(x => x.Triangles).SelectMany(x => x.ToArray()).ToArray());
					_triangleTree = new BoundsOctree<ObjectTriangles>(bb, 0.1f, 1.0f);

					foreach (ObjectTriangles pair in triangles)
					{
						if (pair.Triangles.Count == 0)
						{
							continue;
						}

						Vector3[] a = pair.Triangles.SelectMany(x => x.ToArray()).ToArray();
						bb = BoundingBox.FromPoints(a);
						_triangleTree.Add(pair, bb);
					}

					break;
				}

				default:
					throw new NotImplementedException(signatureStr);
			}

			ObjectCache.Clear();
			ModelCache.Clear();

			if (metadataOffset == 0)
			{
				return;
			}

			file.Position = metadataOffset;
			bool done = false;

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
				file.ReadExact(buffer, 0, 8);
				long offset = file.Position;
				var type = (ChunkTypes)BitConverter.ToUInt32(buffer, 0);
				int size = BitConverter.ToInt32(buffer, 4);

				switch (type)
				{
					case ChunkTypes.Label:
						while (true)
						{
							file.ReadExact(buffer, 0, 8);
							uint labelOffset = BitConverter.ToUInt32(buffer, 0);

							if (labelOffset == 0xFFFFFFFF)
							{
								break;
							}

							uint nameOffset = BitConverter.ToUInt32(buffer, 4);

							if (nameOffset == 0 || nameOffset == 0xFFFFFFFF)
							{
								break;
							}

							long pos = file.Position;
							file.Position = offset + nameOffset;

							int i = file.ReadString(buffer);

							file.Position = pos;
							string name = Encoding.UTF8.GetString(buffer, 0, i);
							labels.Add(new KeyValuePair<uint, string>(labelOffset, name));
						}
						break;

					case ChunkTypes.Animations:
						if (size == 0)
						{
							break;
						}

						// ReSharper disable once RedundantAssignment
						animations = Encoding.UTF8.GetString(buffer, 0, file.ReadString(buffer));
						break;

					case ChunkTypes.Author:
						if (size == 0)
						{
							break;
						}

						// ReSharper disable once RedundantAssignment
						author = Encoding.UTF8.GetString(buffer, 0, file.ReadString(buffer));
						break;

					case ChunkTypes.Tool:
						if (size == 0)
						{
							break;
						}

						// ReSharper disable once RedundantAssignment
						tool = Encoding.UTF8.GetString(buffer, 0, file.ReadString(buffer));
						break;

					case ChunkTypes.Description:
						if (size == 0)
						{
							break;
						}

						// ReSharper disable once RedundantAssignment
						description = Encoding.UTF8.GetString(buffer, 0, file.ReadString(buffer));
						break;

					case ChunkTypes.End:
						done = true;
						break;

					default:
						throw new ArgumentOutOfRangeException();
				}

				file.Position = offset + size;
			}

#if DEBUG
			string metadataText = $"Description: {description}" +
			                      $"{Environment.NewLine}Tool: {tool}" +
			                      $"{Environment.NewLine}Author: {author}" +
			                      $"{Environment.NewLine}Animations: {animations}";

			MessageBox.Show(this, metadataText);

#if false
			if (labels.Count > 0)
			{
				Debug.WriteLine("Labels:");

				foreach (KeyValuePair<uint, string> pair in labels)
				{
					Debug.WriteLine($"\t{pair.Key}: {pair.Value}");
				}
			}
#endif
#endif
		}

		private void openTexturesToolStripMenuItem_Click(object sender, EventArgs e)
		{
			OpenFileDialog dialog = openTexturesDialog;
			if (dialog.ShowDialog(this) != DialogResult.OK)
			{
				return;
			}

			string extension = Path.GetExtension(dialog.FileName);

			if (string.IsNullOrEmpty(extension))
			{
				MessageBox.Show(this, "no extension wtf", "wtf");
				return;
			}

			_renderer!.ClearTexturePool();

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

		private void LoadTextureIndex(string fileName)
		{
			using var factory = new ImagingFactory2();
			string   directory = Path.GetDirectoryName(fileName) ?? string.Empty;
			string[] index     = File.ReadAllLines(fileName);

			int lineNumber = 0;
			foreach (string line in index)
			{
				++lineNumber;

				int i = line.LastIndexOf(",", StringComparison.Ordinal);

				string texturePath = Path.Combine(directory, line.Substring(++i));

				if (!File.Exists(texturePath))
				{
					MessageBox.Show($"Missing texture on line {lineNumber}: {texturePath}");
					_renderer!.ClearTexturePool();
					break;
				}

				using var decoder = new BitmapDecoder(factory, texturePath, DecodeOptions.CacheOnDemand);
				using var converter = new FormatConverter(factory);
				converter.Initialize(decoder.GetFrame(0), PixelFormat.Format32bppPRGBA, BitmapDitherType.None, null, 0.0, BitmapPaletteType.Custom);
				_renderer!.CreateTextureFromBitmapSource(converter);
			}
		}

		private void LoadPRS(string fileName)
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

		private void LoadPVM(string fileName)
		{
			using var file = new FileStream(fileName, FileMode.Open);
			LoadPVM(file);
		}

		private void LoadPVM(Stream stream)
		{
			if (!PvmArchive.Identify(stream))
			{
				MessageBox.Show("nope");
				return;
			}

			var pvm = new PvmArchive();
			var rawTextures = new List<RawTexture>();

			foreach (ArchiveEntry entry in pvm.Open(stream).Entries)
			{
				using Stream entryStream = entry.Open();

				if (!PvrTextureDecoder.Is(entryStream))
				{
					continue;
				}

				PvrTextureDecoder decoder = new(entryStream);

				rawTextures.Clear();
				rawTextures.Add(new RawTexture(decoder.Width, decoder.Height, decoder.GetPixelData()));

				foreach (PvrMipmapDecoder mipmapDecoder in decoder.Mipmaps)
				{
					rawTextures.Add(new RawTexture(mipmapDecoder.Width, mipmapDecoder.Height, mipmapDecoder.GetPixelData()));
				}

				_renderer!.CreateTextureFromRawTextures(rawTextures);
			}
		}

		private void OnShown(object sender, EventArgs e)
		{
#if DEBUG
			showOctreeToolStripMenuItem.Checked = true;
#endif

			int w = scene.ClientRectangle.Width;
			int h = scene.ClientRectangle.Height;

			try
			{
				_renderer = new Renderer(w, h, scene.Handle);
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

			enableOITToolStripMenuItem.Enabled = _renderer.OITCapable;

			UpdateProjection();
			scene.SizeChanged += OnSizeChanged;
		}

		private void UpdateProjection()
		{
			float width = scene.ClientRectangle.Width;
			float height = scene.ClientRectangle.Height;
			float ratio = width / height;

			float fov = s_fovY;
			float h = 2f * MathF.Atan(MathF.Tan(s_fovY / 2.0f) * ratio);

			if (h < s_fovX)
			{
				fov = 2f * MathF.Atan(MathF.Tan(s_fovX / 2.0f) * (height / width));
			}

			_camera.SetProjection(fov, ratio, 1.0f, 100000.0f);
		}

		private void UpdateCamera()
		{
			if (_cameraControls != CameraControls.None)
			{
				var v = new Vector3();

				if (_cameraControls.HasFlag(CameraControls.Forward))
				{
					v.Z -= 1.0f;
				}

				if (_cameraControls.HasFlag(CameraControls.Backward))
				{
					v.Z += 1.0f;
				}

				if (_cameraControls.HasFlag(CameraControls.Right))
				{
					v.X += 1.0f;
				}

				if (_cameraControls.HasFlag(CameraControls.Left))
				{
					v.X -= 1.0f;
				}

				if (_cameraControls.HasFlag(CameraControls.Up))
				{
					v.Y += 1.0f;
				}

				if (_cameraControls.HasFlag(CameraControls.Down))
				{
					v.Y -= 1.0f;
				}

				_camera.Translate(v, _speed * DeltaTime.SecondsElapsed);
			}

			if (!_camera.Invalid)
			{
				return;
			}

			_camera.Update();

			Matrix m = _camera.Projection;
			_renderer!.SetTransform(TransformState.Projection, in m);
			m = _camera.View;
			_renderer.SetTransform(TransformState.View, in m);
		}

		private Ray     _lastRay;
		private RayHit? _lastHit;

		// TODO: conditional render (only render when the scene has been invalidated)
		public void MainLoop()
		{
			DeltaTime.Update();

			if (WindowState == FormWindowState.Minimized)
			{
				return;
			}

			UpdateCamera();

			if (_renderer == null)
			{
				return;
			}

			_renderer.Clear();

			if (_lastHit != null)
			{
				_renderer.DrawDebugLine(new DebugLine(new DebugPoint(_lastRay.Position, Color.DarkGreen),
				                                     new DebugPoint(_lastRay.Position + (_lastRay.Direction * _lastHit.Value.Distance), Color.DarkGreen)));
			}
			else
			{
				_renderer.DrawDebugLine(new DebugLine(new DebugPoint(_lastRay.Position, Color.Blue),
				                                     new DebugPoint(_lastRay.Position + (_lastRay.Direction * 16777215f), Color.Blue)));
			}

			if (_object is not null && _objectTree is not null)
			{
				if (_objectTree.Empty)
				{
					_objectTree.Add(_object, _renderer);
				}

				if (showOctreeToolStripMenuItem.Checked)
				{
					foreach (BoundingBox bounds in _objectTree.GiveMeTheBounds())
					{
						Color4 color = _camera.Frustum.Contains(bounds) == ContainmentType.Contains
							? new Color4(0f, 1f, 0f, 1f)
							: new Color4(1f, 0f, 0f, 1f);
						_renderer.DrawBounds(in bounds, color);
					}
				}

				List<MeshsetQueueElementBase> visible = _objectTree.GetVisible(_camera);
				base.Text = $"{visible.Count}";

				_renderer.Draw(visible, _camera);
			}

			if (_landTable is not null && _landTableTree is not null)
			{
				_renderer.FlowControl.UseMaterialFlags = true;
				_renderer.FlowControl.Add(0, NJD_FLAG.IgnoreSpecular);

				if (_landTableTree.Empty)
				{
					_landTableTree.Add(_landTable, _renderer);
				}

				if (showOctreeToolStripMenuItem.Checked)
				{
					foreach (BoundingBox bounds in _landTableTree.GiveMeTheBounds())
					{
						//Color4 color = camera.Frustum.Contains(bounds) == ContainmentType.Contains
						//	? new Color4(0f, 1f, 0f, 1f)
						//	: new Color4(1f, 0f, 0f, 1f);

						//renderer.DrawBounds(in bounds, color);

						if (_camera.Frustum.Contains(bounds) == ContainmentType.Contains)
						{
							_renderer.DrawBounds(in bounds, new Color4(1.0f, 0.0f, 1.0f, 1.0f));
						}
						else
						{
							_renderer.DrawBounds(in bounds, new Color4(1.0f, 0.0f, 0.0f, 1.0f));
						}
					}
				}

				List<MeshsetQueueElementBase> visible = _landTableTree.GetVisible(_camera);
				base.Text = $"{visible.Count}";

				_renderer.Draw(visible, _camera);
				_renderer.FlowControl.Reset();
			}

			_renderer.Present(_camera);
		}

		private void OnSizeChanged(object? sender, EventArgs e)
		{
			if (WindowState == FormWindowState.Minimized)
			{
				return;
			}

			_renderer?.RefreshDevice(scene.ClientRectangle.Width, scene.ClientRectangle.Height);
			UpdateProjection();
			UpdateCamera();
		}

		private void OnClosed(object sender, FormClosedEventArgs e)
		{
			DisposableExtensions.DisposeAndNullify(ref _object);
			DisposableExtensions.DisposeAndNullify(ref _landTable);
			DisposableExtensions.DisposeAndNullify(ref _renderer);
		}

		[Flags]
		private enum CameraControls
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
					_speed = MathF.Max(7.5f, _speed - 7.5f);
					break;

				case Keys.Add:
					_speed += 7.5f;
					break;

				case Keys.F:
					if (_object != null)
					{
						_camera.LookAt(_object.Position);
					}
					break;

				case Keys.C:
					if (_renderer != null)
					{
						_renderer.DefaultCullMode = _renderer.DefaultCullMode == CullMode.Back ? CullMode.None : CullMode.Back;
					}

					break;

				case Keys.W:
					_cameraControls |= CameraControls.Forward;
					break;

				case Keys.S:
					_cameraControls |= CameraControls.Backward;
					break;

				case Keys.A:
					_cameraControls |= CameraControls.Left;
					break;

				case Keys.D:
					_cameraControls |= CameraControls.Right;
					break;

				case Keys.Up:
					_cameraControls |= CameraControls.Up;
					break;

				case Keys.Down:
					_cameraControls |= CameraControls.Down;
					break;

				case Keys.Space:
					_cameraControls |= CameraControls.Look;
					break;
			}
		}

		private void scene_KeyUp(object sender, KeyEventArgs e)
		{
			switch (e.KeyCode)
			{
				case Keys.W:
					_cameraControls &= ~CameraControls.Forward;
					break;

				case Keys.S:
					_cameraControls &= ~CameraControls.Backward;
					break;

				case Keys.A:
					_cameraControls &= ~CameraControls.Left;
					break;

				case Keys.D:
					_cameraControls &= ~CameraControls.Right;
					break;

				case Keys.Up:
					_cameraControls &= ~CameraControls.Up;
					break;

				case Keys.Down:
					_cameraControls &= ~CameraControls.Down;
					break;

				case Keys.Space:
					_cameraControls &= ~CameraControls.Look;
					break;
			}
		}

		private void scene_MouseMove(object sender, MouseEventArgs e)
		{
			var delta = new System.Drawing.Point(e.Location.X - _lastMouse.X, e.Location.Y - _lastMouse.Y);
			_lastMouse = e.Location;

			if (!_cameraControls.HasFlag(CameraControls.Look))
			{
				return;
			}

			Vector3 rotation;
			rotation.Y = MathF.PI * (delta.X / (float)ClientRectangle.Width);
			rotation.X = MathF.PI * (delta.Y / (float)ClientRectangle.Height);
			rotation.Z = 0.0f;
			_camera.Rotate(rotation);
		}

		private void recompileShadersToolStripMenuItem_Click(object sender, EventArgs e)
		{
			while (true)
			{
				try
				{
					_renderer?.LoadShaders();
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
			if (_triangleTree == null)
			{
				return;
			}

			var viewport = new ViewportF(0f, 0f, scene.ClientRectangle.Width, scene.ClientRectangle.Height);
			var ray = Ray.GetPickRay(e.X, e.Y, viewport, _camera.Frustum.Matrix);

			var colliding = new List<RayCollisionResult<ObjectTriangles>>();
			_triangleTree.GetColliding(colliding, in ray);

			_lastRay = ray;
			_lastHit = null;

			RayCollisionResult<ObjectTriangles>? closestObject = null;
			var closestTriHit = new RayHit(Vector3.Zero, 16777215f);

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

			_lastHit = closestTriHit;
			closestObject.Value.Collider.Object.SkipDraw = true;
		}

		private void enableOITToolStripMenuItem_CheckedChanged(object sender, EventArgs e)
		{
			if (_renderer != null)
			{
				_renderer.OITEnabled = enableOITToolStripMenuItem.Checked;
			}
		}

		private void enableAlphaToolStripMenuItem_CheckedChanged(object sender, EventArgs e)
		{
			if (_renderer != null)
			{
				_renderer.EnableAlpha = enableAlphaToolStripMenuItem.Checked;
			}
		}
	}
}
