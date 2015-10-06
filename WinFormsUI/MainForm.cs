using System;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Threading;
using SPP1.ExternalMergeSort;
using System.IO;
using System.Text;
using System.Linq;

namespace SPP1.WinFormsUI
{
    public partial class MainForm : Form
    {
        public MainForm()
        {
            InitializeComponent();
        }

        private CancellationTokenSource tokenSource;
        private string filePath;

        private void startButton_Click(object sender, EventArgs e)
        {
            int chunkSize;
            try
            {
                chunkSize = Int32.Parse(chunkSizeTextBox.Text);
            }
            catch
            {
                MessageBox.Show("\"" + chunkSizeTextBox.Text + "\" is not a valid integer value.", this.Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            tokenSource = new CancellationTokenSource();
            var token = tokenSource.Token;

            var sorter = new ExternalMergeSorter(filePath, chunkSize, !overwriteCheckBox.Checked, tokenSource.Token);
            sorter.ProgressMessagePosted += OnProgressMessagePosted;

            Task.Factory.StartNew(async () => await sorter.Sort(), tokenSource.Token);

            cancelButton.Enabled = true;
            startButton.Enabled = false;
            selectFileButton.Enabled = false;
        }

        private void OnProgressMessagePosted(object sender, ExternalMergeSorter.ProgressMessagePostedEventArgs e)
        {
            if (logListBox.InvokeRequired)
                Invoke(new Action<string>(DisplayMessage), e.Message);
            else
                DisplayMessage(e.Message);

            if (e.Status == ExternalMergeSorter.SortStatus.Completed)
                Invoke(new Action(ResetForm));
        }

        private void DisplayMessage(string message)
        {
            logListBox.Items.Add(message);
            logListBox.SelectedIndex = logListBox.Items.Count - 1;
            logListBox.ClearSelected();
        }

        private void cancelButton_Click(object sender, EventArgs e)
        {
            if (tokenSource != null)
            {
                try { tokenSource.Cancel(); } catch {; }

                cancelButton.Enabled = false;
            }
        }

        private void selectFileButton_Click(object sender, EventArgs e)
        {
            var dialog = new OpenFileDialog();
            dialog.Multiselect = false;
            if (dialog.ShowDialog() == DialogResult.OK)
            {
                filePath = dialog.FileName;
                filePathLabel.Text = filePath;
                chunkSizeTextBox.Text = ExternalMergeSorter.EstimateChunkSize(filePath).ToString();

                startButton.Enabled = true;
            }
        }

        private void ResetForm()
        {
            filePath = null;
            tokenSource.Dispose();
            tokenSource = null;

            filePathLabel.Text = "";
            chunkSizeTextBox.Text = "";
            startButton.Enabled = false;
            cancelButton.Enabled = false;
            selectFileButton.Enabled = true;
        }
    }
}
