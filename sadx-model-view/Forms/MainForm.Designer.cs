using sadx_model_view.Controls;

namespace sadx_model_view.Forms
{
	partial class MainForm
	{
		/// <summary>
		/// Required designer variable.
		/// </summary>
		private System.ComponentModel.IContainer components = null;

		/// <summary>
		/// Clean up any resources being used.
		/// </summary>
		/// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
		protected override void Dispose(bool disposing)
		{
			if (disposing && (components != null))
			{
				components.Dispose();
			}
			base.Dispose(disposing);
		}

		#region Windows Form Designer generated code

		/// <summary>
		/// Required method for Designer support - do not modify
		/// the contents of this method with the code editor.
		/// </summary>
		private void InitializeComponent()
		{
			this.menuStrip = new System.Windows.Forms.MenuStrip();
			this.fileToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.openToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.openTexturesToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.editToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.recompileShadersToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.viewToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.enableAlphaToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.showOctreeToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.openModelDialog = new System.Windows.Forms.OpenFileDialog();
			this.openTexturesDialog = new System.Windows.Forms.OpenFileDialog();
			this.scene = new sadx_model_view.Controls.Scene();
			this.menuStrip.SuspendLayout();
			this.SuspendLayout();
			// 
			// menuStrip
			// 
			this.menuStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.fileToolStripMenuItem,
            this.editToolStripMenuItem,
            this.viewToolStripMenuItem});
			this.menuStrip.Location = new System.Drawing.Point(0, 0);
			this.menuStrip.Name = "menuStrip";
			this.menuStrip.Size = new System.Drawing.Size(944, 24);
			this.menuStrip.TabIndex = 0;
			// 
			// fileToolStripMenuItem
			// 
			this.fileToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.openToolStripMenuItem,
            this.openTexturesToolStripMenuItem});
			this.fileToolStripMenuItem.Name = "fileToolStripMenuItem";
			this.fileToolStripMenuItem.Size = new System.Drawing.Size(37, 20);
			this.fileToolStripMenuItem.Text = "&File";
			// 
			// openToolStripMenuItem
			// 
			this.openToolStripMenuItem.Name = "openToolStripMenuItem";
			this.openToolStripMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.O)));
			this.openToolStripMenuItem.Size = new System.Drawing.Size(189, 22);
			this.openToolStripMenuItem.Text = "&Open";
			this.openToolStripMenuItem.Click += new System.EventHandler(this.openToolStripMenuItem_Click);
			// 
			// openTexturesToolStripMenuItem
			// 
			this.openTexturesToolStripMenuItem.Name = "openTexturesToolStripMenuItem";
			this.openTexturesToolStripMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.T)));
			this.openTexturesToolStripMenuItem.Size = new System.Drawing.Size(189, 22);
			this.openTexturesToolStripMenuItem.Text = "Open &Textures";
			this.openTexturesToolStripMenuItem.Click += new System.EventHandler(this.openTexturesToolStripMenuItem_Click);
			// 
			// editToolStripMenuItem
			// 
			this.editToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.recompileShadersToolStripMenuItem});
			this.editToolStripMenuItem.Name = "editToolStripMenuItem";
			this.editToolStripMenuItem.Size = new System.Drawing.Size(39, 20);
			this.editToolStripMenuItem.Text = "&Edit";
			// 
			// recompileShadersToolStripMenuItem
			// 
			this.recompileShadersToolStripMenuItem.Name = "recompileShadersToolStripMenuItem";
			this.recompileShadersToolStripMenuItem.Size = new System.Drawing.Size(174, 22);
			this.recompileShadersToolStripMenuItem.Text = "Recompile Shaders";
			this.recompileShadersToolStripMenuItem.Click += new System.EventHandler(this.recompileShadersToolStripMenuItem_Click);
			// 
			// viewToolStripMenuItem
			// 
			this.viewToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.enableAlphaToolStripMenuItem,
            this.showOctreeToolStripMenuItem});
			this.viewToolStripMenuItem.Name = "viewToolStripMenuItem";
			this.viewToolStripMenuItem.Size = new System.Drawing.Size(44, 20);
			this.viewToolStripMenuItem.Text = "View";
			// 
			// enableAlphaToolStripMenuItem
			// 
			this.enableAlphaToolStripMenuItem.Checked = true;
			this.enableAlphaToolStripMenuItem.CheckOnClick = true;
			this.enableAlphaToolStripMenuItem.CheckState = System.Windows.Forms.CheckState.Checked;
			this.enableAlphaToolStripMenuItem.Name = "enableAlphaToolStripMenuItem";
			this.enableAlphaToolStripMenuItem.Size = new System.Drawing.Size(143, 22);
			this.enableAlphaToolStripMenuItem.Text = "Enable Alpha";
			this.enableAlphaToolStripMenuItem.CheckedChanged += new System.EventHandler(this.enableAlphaToolStripMenuItem_CheckedChanged);
			// 
			// showOctreeToolStripMenuItem
			// 
			this.showOctreeToolStripMenuItem.CheckOnClick = true;
			this.showOctreeToolStripMenuItem.Name = "showOctreeToolStripMenuItem";
			this.showOctreeToolStripMenuItem.Size = new System.Drawing.Size(143, 22);
			this.showOctreeToolStripMenuItem.Text = "Show Octree";
			// 
			// openModelDialog
			// 
			this.openModelDialog.DefaultExt = "sa1mdl";
			this.openModelDialog.Filter = "Sonic Adventure files|*.sa1mdl;*.sa1lvl|Sonic Adventure Model|*.sa1mdl|Sonic Adve" +
    "nture LandTable|*.sa1lvl|All files|*.*";
			this.openModelDialog.Title = "Open Model";
			// 
			// openTexturesDialog
			// 
			this.openTexturesDialog.DefaultExt = "txt";
			this.openTexturesDialog.Filter = "All texture files|*.txt;*.prs;*.pvm|Texture Pack Index files|*.txt|PRS files|*.pr" +
    "s|PVM files|*.pvm";
			this.openTexturesDialog.Title = "Open Textures";
			// 
			// scene
			// 
			this.scene.BackgroundImageLayout = System.Windows.Forms.ImageLayout.None;
			this.scene.Dock = System.Windows.Forms.DockStyle.Fill;
			this.scene.Location = new System.Drawing.Point(0, 24);
			this.scene.Margin = new System.Windows.Forms.Padding(0);
			this.scene.Name = "scene";
			this.scene.Size = new System.Drawing.Size(944, 537);
			this.scene.TabIndex = 1;
			this.scene.KeyDown += new System.Windows.Forms.KeyEventHandler(this.scene_KeyDown);
			this.scene.KeyUp += new System.Windows.Forms.KeyEventHandler(this.scene_KeyUp);
			this.scene.MouseMove += new System.Windows.Forms.MouseEventHandler(this.scene_MouseMove);
			this.scene.MouseUp += new System.Windows.Forms.MouseEventHandler(this.scene_MouseUp);
			// 
			// MainForm
			// 
			this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.ClientSize = new System.Drawing.Size(944, 561);
			this.Controls.Add(this.scene);
			this.Controls.Add(this.menuStrip);
			this.MainMenuStrip = this.menuStrip;
			this.MinimumSize = new System.Drawing.Size(320, 200);
			this.Name = "MainForm";
			this.Text = "Model Viewer";
			this.FormClosed += new System.Windows.Forms.FormClosedEventHandler(this.OnClosed);
			this.Load += new System.EventHandler(this.OnShown);
			this.KeyDown += new System.Windows.Forms.KeyEventHandler(this.scene_KeyDown);
			this.KeyUp += new System.Windows.Forms.KeyEventHandler(this.scene_KeyUp);
			this.MouseMove += new System.Windows.Forms.MouseEventHandler(this.scene_MouseMove);
			this.menuStrip.ResumeLayout(false);
			this.menuStrip.PerformLayout();
			this.ResumeLayout(false);
			this.PerformLayout();

		}

		#endregion

		private System.Windows.Forms.MenuStrip menuStrip;
		private System.Windows.Forms.ToolStripMenuItem fileToolStripMenuItem;
		private System.Windows.Forms.ToolStripMenuItem openToolStripMenuItem;
		private System.Windows.Forms.ToolStripMenuItem editToolStripMenuItem;
		private System.Windows.Forms.OpenFileDialog openModelDialog;
		private System.Windows.Forms.OpenFileDialog openTexturesDialog;
		private System.Windows.Forms.ToolStripMenuItem openTexturesToolStripMenuItem;
		private Scene scene;
		private System.Windows.Forms.ToolStripMenuItem recompileShadersToolStripMenuItem;
		private System.Windows.Forms.ToolStripMenuItem viewToolStripMenuItem;
		private System.Windows.Forms.ToolStripMenuItem enableAlphaToolStripMenuItem;
		private System.Windows.Forms.ToolStripMenuItem showOctreeToolStripMenuItem;
	}
}

