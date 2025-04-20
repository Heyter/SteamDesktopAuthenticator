using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using SteamAuth;
using System.Drawing.Drawing2D;
using System.Linq;

namespace Steam_Desktop_Authenticator
{
    public partial class ConfirmationFormWeb : Form
    {
        private readonly SteamGuardAccount steamAccount;

        public ConfirmationFormWeb(SteamGuardAccount steamAccount)
        {
            InitializeComponent();
            this.steamAccount = steamAccount;
            Text = string.Format("Trade Confirmations - {0}", steamAccount.AccountName);
        }
        private async Task LoadData()
        {
            splitContainer1.Panel2.Controls.Clear();

            // Check for a valid refresh token first
            if (steamAccount.Session.IsRefreshTokenExpired())
            {
                MessageBox.Show("Your session has expired. Use the login again button under the selected account menu.", "Trade Confirmations", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Close();
                return;
            }

            // Check for a valid access token, refresh it if needed
            if (steamAccount.Session.IsAccessTokenExpired())
            {
                try
                {
                    await steamAccount.Session.RefreshAccessToken();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Steam Login Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    Close();
                    return;
                }
            }

            try
            {
                var confirmations = await steamAccount.FetchConfirmationsAsync();

                if (confirmations == null || confirmations.Length == 0)
                {
                    Label errorLabel = new() { Text = "Nothing to confirm/cancel", AutoSize = true, ForeColor = Color.Black, Location = new Point(150, 20) };
                    splitContainer1.Panel2.Controls.Add(errorLabel);
                    return;
                }

                Panel containerPanel = new Panel()
                {
                    Dock = DockStyle.Top,
                    AutoSize = true,
                    AutoSizeMode = AutoSizeMode.GrowAndShrink
                };

                splitContainer1.Panel2.AutoScroll = true;
                splitContainer1.Panel2.AutoScrollMinSize = new Size(0, confirmations.Length * 130);
                splitContainer1.Panel2.Resize += (sender, e) =>
                {
                    foreach (Panel panel in containerPanel.Controls.OfType<Panel>())
                    {
                        panel.Width = splitContainer1.Panel2.Width - SystemInformation.VerticalScrollBarWidth - 5;
                    }
                };

                foreach (var confirmation in confirmations)
                {
                    Panel panel = new() { Dock = DockStyle.Top, Height = 120, Width = splitContainer1.Panel2.Width - 25 };

                    panel.Paint += (s, e) =>
                    {
                        using LinearGradientBrush brush = new(panel.ClientRectangle, Color.Black, Color.DarkCyan, 90F);
                        e.Graphics.FillRectangle(brush, panel.ClientRectangle);
                    };

                    if (!string.IsNullOrEmpty(confirmation.Icon))
                    {
                        PictureBox pictureBox = new() { Width = 60, Height = 60, Location = new Point(20, 20), SizeMode = PictureBoxSizeMode.Zoom };
                        try
                        {
                            pictureBox.Load(confirmation.Icon);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("Failed to load avatar: " + ex.Message);
                        }
                        panel.Controls.Add(pictureBox);
                    }

                    Label nameLabel = new()
                    {
                        Text = $"{confirmation.Headline}\n{confirmation.Creator.ToString()}",
                        AutoSize = true,
                        ForeColor = Color.Snow,
                        Location = new Point(90, 20),
                        BackColor = Color.Transparent
                    };
                    panel.Controls.Add(nameLabel);

                    ConfirmationButton acceptButton = new()
                    {
                        Text = confirmation.Accept,
                        Location = new Point(90, 50),
                        FlatStyle = FlatStyle.Flat,
                        FlatAppearance = { BorderSize = 0 },
                        BackColor = Color.Black,
                        ForeColor = Color.Snow,
                        Confirmation = confirmation,
                        ParentPanel = panel
                    };
                    acceptButton.Click += BtnAccept_Click;
                    panel.Controls.Add(acceptButton);

                    ConfirmationButton cancelButton = new()
                    {
                        Text = confirmation.Cancel,
                        Location = new Point(180, 50),
                        FlatStyle = FlatStyle.Flat,
                        FlatAppearance = { BorderSize = 0 },
                        BackColor = Color.Black,
                        ForeColor = Color.Snow,
                        Confirmation = confirmation,
                        ParentPanel = panel
                    };
                    cancelButton.Click += BtnCancel_Click;
                    panel.Controls.Add(cancelButton);

                    Label summaryLabel = new()
                    {
                        Text = string.Join("\n", confirmation.Summary),
                        AutoSize = true,
                        ForeColor = Color.Snow,
                        Location = new Point(90, 80),
                        BackColor = Color.Transparent
                    };
                    panel.Controls.Add(summaryLabel);

                    containerPanel.Controls.Add(panel);
                }

                splitContainer1.Panel2.Controls.Add(containerPanel);
            }
            catch (Exception ex)
            {
                Label errorLabel = new() { Text = "Something went wrong:\n" + ex.Message, AutoSize = true, ForeColor = Color.Red, Location = new Point(20, 20) };
                splitContainer1.Panel2.Controls.Add(errorLabel);
            }
        }

        private async void BtnAccept_Click(object sender, EventArgs e)
        {
            var button = (ConfirmationButton)sender;
            var confirmation = button.Confirmation;
            bool result = await steamAccount.AcceptConfirmation(confirmation);

            if (result)
            {
                button.ParentPanel.Dispose();
                splitContainer1.Panel2.PerformLayout();
            }
            else
            {
                await LoadData();
            }
        }

        private async void BtnCancel_Click(object sender, EventArgs e)
        {
            var button = (ConfirmationButton)sender;
            var confirmation = button.Confirmation;
            bool result = await steamAccount.DenyConfirmation(confirmation);

            if (result)
            {
                button.ParentPanel.Dispose();
                splitContainer1.Panel2.PerformLayout();
            }
            else
            {
                await LoadData();
            }
        }


        private async void BtnRefresh_Click(object sender, EventArgs e)
        {
            btnRefresh.Enabled = false;
            btnRefresh.Text = "Refreshing...";

            await LoadData();

            btnRefresh.Enabled = true;
            btnRefresh.Text = "Refresh";
        }

        private async void ConfirmationFormWeb_Shown(object sender, EventArgs e)
        {
            btnRefresh.Enabled = false;
            btnRefresh.Text = "Refreshing...";

            await LoadData();

            btnRefresh.Enabled = true;
            btnRefresh.Text = "Refresh";
        }
    }
}
