using AssetsTools.NET;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;

namespace UABEAvalonia
{
    public partial class ProgressWindow : Window
    {
        public IAssetBundleCompressProgress Progress { get; }

        public ProgressWindow()
        {
            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif
            Progress = new ProgressWindowProgress(this);

            progressBar.Minimum = 0.0;
            progressBar.Maximum = 1.0;
        }

        public ProgressWindow(string title) : this()
        {
            lblTitle.Text = title;
        }

        private void UpdateProgress(float progress)
        {
            progressBar.Value = progress;
            if (progressBar.Value >= 1.0f)
            {
                Close(true);
            }
        }

        internal class ProgressWindowProgress : IAssetBundleCompressProgress
        {
            private ProgressWindow window;

            public ProgressWindowProgress(ProgressWindow window)
            {
                this.window = window;
            }

            public void SetProgress(float progress)
            {
                Dispatcher.UIThread.Post(() => window.UpdateProgress(progress), DispatcherPriority.Background);
            }
        }
    }
}