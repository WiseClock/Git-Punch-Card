using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using LibGit2Sharp;
using Microsoft.WindowsAPICodePack.Dialogs;

namespace GitPunchCard
{
    public partial class MainWindow : Form
    {
        private readonly Font _mediumFont = new Font("Arial", 12, FontStyle.Regular, GraphicsUnit.Pixel);
        private readonly Font _largeFont = new Font("Arial", 16, FontStyle.Bold, GraphicsUnit.Pixel);
        private readonly Dictionary<string, string> _emailStringPair = new Dictionary<string, string>();

        public MainWindow()
        {
            InitializeComponent();
        }

        private static bool CheckIsRepo(string path)
        {
            return Repository.IsValid(path);
        }

        private void btnStart_Click(object sender, EventArgs e)
        {
            if (!CheckIsRepo(txtLocation.Text))
                return;
            string path = txtLocation.Text;

            _emailStringPair.Clear();
            Dictionary<string, Dictionary<string, int>> container = new Dictionary<string, Dictionary<string, int>>();
            int maxCommits = 0;

            using (Repository repo = new Repository(path))
            {
                IEnumerable<Commit> commits = repo.Head.Commits;
                foreach (var commit in commits)
                {
                    string user = commit.Committer.Email;
                    // if (!_emailStringPair.ContainsKey(user))
                        _emailStringPair[user] = commit.Committer.Name;
                    int dayOfWeek = (int)commit.Committer.When.DayOfWeek; // starts from sunday
                    int hour = commit.Committer.When.Hour;
                    string dayHourKey = $"{dayOfWeek}_{hour}";
                    if (container.ContainsKey(user))
                    {
                        if (container[user].ContainsKey(dayHourKey))
                        {
                            container[user][dayHourKey]++;
                            if (container[user][dayHourKey] > maxCommits)
                                maxCommits = container[user][dayHourKey];
                        }
                        else
                        {
                            container[user][dayHourKey] = 1;
                        }
                    }
                    else
                    {
                        container[user] = new Dictionary<string, int> { { dayHourKey, 1 } };
                    }
                }
            }

            DrawAllUsers(container, maxCommits);
        }

        private void btnSelectFolder_Click(object sender, EventArgs e)
        {
            using (CommonOpenFileDialog dialog = new CommonOpenFileDialog { IsFolderPicker = true })
            {
                if (dialog.ShowDialog() != CommonFileDialogResult.Ok) return;
                if (CheckIsRepo(dialog.FileName))
                {
                    txtLocation.Text = dialog.FileName;
                }
                else
                {
                    MessageBox.Show("Not a .git folder", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void DrawAllUsers(Dictionary<string, Dictionary<string, int>> container, int maxCommits)
        {
            const int width = 820;
            int height = 5000;

            int yOffset = 0;

            using (Bitmap bmp = new Bitmap(width, height))
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.CompositingQuality = CompositingQuality.HighQuality;
                g.SmoothingMode = SmoothingMode.HighQuality;
                g.TextRenderingHint = TextRenderingHint.AntiAlias;
                g.Clear(Color.White);

                foreach (var namePair in container)
                {
                    Dictionary<string, int> sortedValue = namePair.Value.OrderBy(x => x.Key).ToDictionary(x => x.Key, x => x.Value);
                    DrawUser(g, ref yOffset, namePair.Key, sortedValue, maxCommits);
                }

                height = yOffset;
                picResult.Size = new Size(width, height);

                Bitmap bitmapCrop = new Bitmap(width, height);
                using (Graphics gCrop = Graphics.FromImage(bitmapCrop))
                {
                    gCrop.DrawImage(bmp, new Rectangle(0, 0, width, height), new Rectangle(0, 0, width, height), GraphicsUnit.Pixel);
                }
                picResult.Image = new Bitmap(bitmapCrop, width, height);
            }
        }

        private void DrawUser(Graphics g, ref int yOffset, string name, Dictionary<string, int> hours, int maxCommits)
        {
            g.DrawString(_emailStringPair[name], _largeFont, Brushes.DimGray, 0, yOffset);
            yOffset += 40;

            for (int day = 0; day < 7; day++)
            {
                using (StringFormat format = new StringFormat())
                {
                    format.LineAlignment = StringAlignment.Center;
                    g.DrawString(GetDayString(day), _mediumFont, Brushes.DimGray, 0, yOffset, format);
                }
                
                g.DrawLine(Pens.LightGray, 0, yOffset + 40, 820, yOffset + 40);
                int x = 100;
                for (int hour = 0; hour < 24; hour++)
                {
                    g.DrawLine(Pens.LightGray, x, yOffset + 25 + (hour % 2 == 1 ? 5 : 0), x, yOffset + 40);

                    // draw dot
                    string key = $"{day}_{hour}";
                    if (hours.ContainsKey(key))
                    {
                        float sizePercentage = hours[key] / (float)maxCommits;
                        float size = sizePercentage * 29 + 3; // 29 max, 3 base
                        g.FillEllipse(new SolidBrush(Color.FromArgb(60, 60, 60)), x - size / 2, yOffset - size / 2, size, size);
                    }

                    x += 30;
                }

                yOffset += 60;
            }

            int timeX = 100;
            for (int hour = 0; hour < 24; hour++)
            {
                using (StringFormat format = new StringFormat())
                {
                    format.Alignment = StringAlignment.Center;
                    format.LineAlignment = StringAlignment.Far;
                    g.DrawString(hour.ToString(), _mediumFont, Brushes.DimGray, timeX, yOffset + 5, format);
                }
                timeX += 30;
            }

            yOffset += 15;
        }

        private static string GetDayString(int day)
        {
            switch (day)
            {
                case 0:
                    return "Sunday";
                case 1:
                    return "Monday";
                case 2:
                    return "Tuesday";
                case 3:
                    return "Wednesday";
                case 4:
                    return "Thursday";
                case 5:
                    return "Friday";
                case 6:
                    return "Saturday";
                default:
                    return "";
            }
        }

        private void btnSave_Click(object sender, EventArgs e)
        {
            if (picResult.Image == null)
                return;

            using (SaveFileDialog sfd = new SaveFileDialog())
            {
                sfd.DefaultExt = "png";
                sfd.AddExtension = true;
                sfd.Filter = "PNG|*.png";
                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    picResult.Image.Save(sfd.FileName, ImageFormat.Png);
                }
            }
        }
    }
}
