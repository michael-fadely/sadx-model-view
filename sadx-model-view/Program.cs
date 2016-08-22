using System;
using System.Windows.Forms;
using SharpDX.Windows;

namespace sadx_model_view
{
	static class Program
	{
		/// <summary>
		/// The main entry point for the application.
		/// </summary>
		[STAThread]
		static void Main()
		{
			Application.EnableVisualStyles();
			Application.SetCompatibleTextRenderingDefault(false);
			MainForm form = new MainForm();
			RenderLoop.Run(form, form.MainLoop);
		}
	}
}
