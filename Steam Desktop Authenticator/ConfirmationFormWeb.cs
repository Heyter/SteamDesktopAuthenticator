using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using SteamAuth;
using System.Drawing.Drawing2D;

namespace Steam_Desktop_Authenticator
{
    public partial class ConfirmationFormWeb : Form
    {
        private readonly SteamGuardAccount _steamAccount;
        private Panel _confirmationsContainer;
        private bool _hasResizeEvent;
        private const int PanelHeight = 120;
        private const int PanelMargin = 10;

        public ConfirmationFormWeb(SteamGuardAccount steamAccount)
        {
            InitializeComponent();
            splitContainer1.Panel2.AutoScroll = true;
            splitContainer1.Panel2.AutoScrollMinSize = new Size(0, 0);
            splitContainer1.Panel2.Padding = new Padding(5);
            _steamAccount = steamAccount;
            Text = $"Trade Confirmations - {steamAccount.AccountName}";
            DoubleBuffered = true;
        }

        private void UpdateScrollMinSize()
        {
            int totalHeight = 0;

            System.Collections.IList list = _confirmationsContainer.Controls;
            for (int i = 0; i < list.Count; i++)
            {
                Control control = (Control)list[i];
                control.Width = _confirmationsContainer.Width;
                totalHeight += control.Height + control.Margin.Bottom;
            }

            _confirmationsContainer.Height = totalHeight;
            splitContainer1.Panel2.AutoScrollMinSize = new Size(0, totalHeight);
        }

        private async ValueTask LoadData()
        {
            try
            {
                splitContainer1.Panel2.SuspendLayout();
                splitContainer1.Panel2.Controls.Clear();
                splitContainer1.Panel2.AutoScrollMinSize = Size.Empty;
                splitContainer1.Panel2.AutoScrollPosition = Point.Empty;

                if (await CheckSessionValidity() == false)
                    return;

                var confirmations = await _steamAccount.FetchConfirmationsAsync();
                if (confirmations == null || confirmations.Length == 0)
                {
                    splitContainer1.Panel2.Controls.Add(new Label
                    {
                        Text = "Nothing to confirm/cancel",
                        AutoSize = true,
                        ForeColor = Color.Black,
                        Location = new Point(150, 20)
                    });

                    return;
                }

                if (_confirmationsContainer != null)
                {
                    splitContainer1.Panel2.Controls.Remove(_confirmationsContainer);
                    _confirmationsContainer.Dispose();
                }

                int newWidth = splitContainer1.Panel2.ClientSize.Width - SystemInformation.VerticalScrollBarWidth;
                _confirmationsContainer = new Panel
                {
                    Dock = DockStyle.None,
                    AutoSize = false,
                    Width = newWidth,
                    Height = 0,
                    AutoScroll = false,
                    Padding = new Padding(0, 0, 0, PanelMargin)
                };

                splitContainer1.Panel2.Controls.Add(_confirmationsContainer);
                splitContainer1.Panel2.AutoScroll = true;

                if (!_hasResizeEvent)
                {
                    _hasResizeEvent = true;
                    splitContainer1.Panel2.Resize += (sender, e) =>
                    {
                        _confirmationsContainer.Width = newWidth;
                        UpdateScrollMinSize();
                    };
                }

                await CreateConfirmationPanels(confirmations);
                UpdateScrollMinSize();
            }
            catch (Exception ex)
            {
                ShowErrorLabel($"Something went wrong:\n{ex.Message}");
            }
            finally
            {
                splitContainer1.Panel2.ResumeLayout(true);
            }
        }

        private async Task<bool> CheckSessionValidity()
        {
            if (_steamAccount.Session.IsRefreshTokenExpired())
            {
                var result = MessageBox.Show(
                    "Your session has expired. Log in again to refresh the session by selecting OK, or use the button in the menu of the selected account.",
                    "Trade Confirmations",
                    MessageBoxButtons.OKCancel,
                    MessageBoxIcon.Error);

                if (result == DialogResult.OK)
                {
                    MainForm.PromptRefreshLogin(_steamAccount);
                    return !_steamAccount.Session.IsRefreshTokenExpired();
                }
                Close();
                return false;
            }

            if (_steamAccount.Session.IsAccessTokenExpired())
            {
                try
                {
                    await _steamAccount.Session.RefreshAccessToken();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Steam Login Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    Close();
                    return false;
                }
            }

            return true;
        }

        private async Task CreateConfirmationPanels(Confirmation[] confirmations)
        {
            if (confirmations.Length == 0)
                return;

            if (confirmations.Length <= 5)
            {
                for (int i = 0; i < confirmations.Length; i++)
                {
                    Confirmation confirmation = confirmations[i];
                    var panel = CreateConfirmationPanel(confirmation);
                    _confirmationsContainer.Controls.Add(panel);
                }

                return;
            }

            var panels = new Panel[confirmations.Length];

            await Task.Run(() =>
            {
                Parallel.For(0, confirmations.Length, i =>
                {
                    panels[i] = CreateConfirmationPanel(confirmations[i]);
                });
            });

            for (int i = 0; i < panels.Length; i++)
            {
                Panel panel = panels[i];
                _confirmationsContainer.Controls.Add(panel);
            }
        }

        private Panel CreateConfirmationPanel(Confirmation confirmation)
        {
            var panel = new Panel
            {
                Height = PanelHeight,
                Width = splitContainer1.Panel2.ClientSize.Width - SystemInformation.VerticalScrollBarWidth,
                Margin = new Padding(0, 0, 0, PanelMargin),
                Dock = DockStyle.Top
            };

            panel.Paint += (s, e) =>
            {
                using var brush = new LinearGradientBrush(panel.ClientRectangle, Color.Black, Color.DarkCyan, 90F);
                e.Graphics.FillRectangle(brush, panel.ClientRectangle);
            };

            if (!string.IsNullOrEmpty(confirmation.Icon))
            {
                var pictureBox = new PictureBox
                {
                    Width = 60,
                    Height = 60,
                    Location = new Point(20, 20),
                    SizeMode = PictureBoxSizeMode.Zoom,
                    BorderStyle = BorderStyle.None,
                    WaitOnLoad = false
                };

                try { pictureBox.Load(confirmation.Icon); }
                catch (Exception) { };

                panel.Controls.Add(pictureBox);
            }

            AddLabelsAndButtons(panel, confirmation);
            return panel;
        }

        private void AddLabelsAndButtons(Panel panel, Confirmation confirmation)
        {
            panel.Controls.Add(new Label
            {
                Text = $"{confirmation.Headline}\n{confirmation.Creator}",
                AutoSize = true,
                ForeColor = Color.Snow,
                Location = new Point(90, 20),
                BackColor = Color.Transparent
            });

            var acceptButton = new ConfirmationButton
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

            var cancelButton = new ConfirmationButton
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

            panel.Controls.Add(new Label
            {
                Text = string.Join("\n", confirmation.Summary),
                AutoSize = true,
                ForeColor = Color.Snow,
                Location = new Point(90, 80),
                BackColor = Color.Transparent
            });
        }

        private void ShowErrorLabel(string message)
        {
            splitContainer1.Panel2.Controls.Add(new Label
            {
                Text = message,
                AutoSize = true,
                ForeColor = Color.Red,
                Location = new Point(20, 20)
            });
        }

        private async void BtnAccept_Click(object sender, EventArgs e)
        {
            await ProcessConfirmationAction((ConfirmationButton)sender, _steamAccount.AcceptConfirmation);
        }

        private async void BtnCancel_Click(object sender, EventArgs e)
        {
            await ProcessConfirmationAction((ConfirmationButton)sender, _steamAccount.DenyConfirmation);
        }

        private async Task ProcessConfirmationAction(ConfirmationButton button, Func<Confirmation, Task<bool>> action)
        {
            bool result = await action(button.Confirmation);

            if (result)
            {
                button.ParentPanel.Dispose();
                UpdateScrollMinSize();
            }
            else
            {
                button.ParentPanel.Enabled = false;
                button.ParentPanel.ForeColor = Color.Red;
                button.Text = "Failed...";
            }
        }

        private async void BtnRefresh_Click(object sender, EventArgs e)
        {
            await RefreshDataWithButtonState();
        }

        private async void ConfirmationFormWeb_Shown(object sender, EventArgs e)
        {
            await RefreshDataWithButtonState();
        }

        private async Task RefreshDataWithButtonState()
        {
            btnRefresh.Enabled = false;
            btnRefresh.Text = "Refreshing...";
            await LoadData();
            btnRefresh.Enabled = true;
            btnRefresh.Text = "Refresh";
        }
    }
}