using System;
using System.Windows.Forms;
using SteamAuth;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;

namespace Steam_Desktop_Authenticator
{
    public partial class MainForm : Form
    {
        private SteamGuardAccount _currentAccount;
        private SteamGuardAccount[] _allAccounts;
        private Manifest _manifest;
        private static readonly SemaphoreSlim _confirmationsSemaphore = new(1, 1);

        private long _steamTime;
        private long _currentSteamChunk;
        private string _passKey;
        private bool _startSilent;
        private readonly TradePopupForm _popupFrm = new();

        public MainForm()
        {
            InitializeComponent();
            DoubleBuffered = true;
        }

        public void SetEncryptionKey(string key) => _passKey = key;
        public void StartSilent(bool silent) => _startSilent = silent;

        // Form event handlers
        private async void MainForm_Shown(object sender, EventArgs e)
        {
            labelVersion.Text = $"v{Application.ProductVersion}";

            try
            {
                _manifest = Manifest.GetManifest();
            }
            catch (ManifestParseException)
            {
                MessageBox.Show("Unable to read your settings. Try restating SDA.",
                    "Steam Desktop Authenticator",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                Close();
                return;
            }

            _manifest.FirstRun = false;
            _manifest.Save();

            await TimerSteamGuard_TickAsync();

            if (_manifest.Encrypted)
            {
                if (_passKey == null)
                {
                    _passKey = _manifest.PromptForPassKey();
                    if (_passKey == null)
                        Application.Exit();
                }

                btnManageEncryption.Text = "Manage Encryption";
            }
            else
                btnManageEncryption.Text = "Setup Encryption";

            btnManageEncryption.Enabled = _manifest.Entries.Count > 0;
            LoadSettings();
            LoadAccountsList();

            if (_startSilent) WindowState = FormWindowState.Minimized;
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            trayIcon.Icon = this.Icon;
        }

        private void MainForm_Resize(object sender, EventArgs e)
        {
            if (this.WindowState == FormWindowState.Minimized)
            {
                this.Hide();
            }
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            Application.Exit();
        }


        // UI Button handlers

        private void BtnSteamLogin_Click(object sender, EventArgs e)
        {
            var loginForm = new LoginForm();
            _ = loginForm.ShowDialog();
            this.LoadAccountsList();
        }

        private void BtnTradeConfirmations_Click(object sender, EventArgs e)
        {
            if (_currentAccount == null) return;

            string oText = btnTradeConfirmations.Text;
            btnTradeConfirmations.Text = "Loading...";
            btnTradeConfirmations.Text = oText;

            ConfirmationFormWeb confirms = new(_currentAccount);
            confirms.Show();
        }

        private void BtnManageEncryption_Click(object sender, EventArgs e)
        {
            if (_manifest.Encrypted)
            {
                using InputForm currentPassKeyForm = new("Enter current passkey", true);
                currentPassKeyForm.ShowDialog();

                if (currentPassKeyForm.Canceled)
                {
                    return;
                }

                string curPassKey = currentPassKeyForm.txtBox.Text;

                using InputForm changePassKeyForm = new("Enter new passkey, or leave blank to remove encryption.");
                changePassKeyForm.ShowDialog();

                if (changePassKeyForm.Canceled && !string.IsNullOrEmpty(changePassKeyForm.txtBox.Text))
                {
                    return;
                }

                using InputForm changePassKeyForm2 = new("Confirm new passkey, or leave blank to remove encryption.");
                changePassKeyForm2.ShowDialog();

                if (changePassKeyForm2.Canceled && !string.IsNullOrEmpty(changePassKeyForm.txtBox.Text))
                {
                    return;
                }

                string newPassKey = changePassKeyForm.txtBox.Text;

                if (newPassKey != changePassKeyForm2.txtBox.Text)
                {
                    MessageBox.Show("Passkeys do not match.");
                    return;
                }

                if (newPassKey.Length == 0) newPassKey = null;
                string action = newPassKey == null ? "remove" : "change";

                if (!_manifest.ChangeEncryptionKey(curPassKey, newPassKey))
                {
                    MessageBox.Show("Unable to " + action + " passkey.");
                }
                else
                {
                    MessageBox.Show("Passkey successfully " + action + "d.");
                    this.LoadAccountsList();
                }
            }
            else
            {
                _passKey = _manifest.PromptSetupPassKey();
                this.LoadAccountsList();
            }
        }

        private void BtnCopy_Click(object sender, EventArgs e)
        {
            CopyLoginToken();
        }

        // Tool strip menu handlers

        private void MenuQuit_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }

        private void MenuRemoveAccountFromManifest_Click(object sender, EventArgs e)
        {
            if (_manifest.Encrypted)
            {
                MessageBox.Show("You cannot remove accounts from the manifest file while it is encrypted.", "Remove from manifest", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            else
            {
                DialogResult res = MessageBox.Show("This will remove the selected account from the manifest file.\nUse this to move a maFile to another computer.\nThis will NOT delete your maFile.", "Remove from manifest", MessageBoxButtons.OKCancel);
                if (res == DialogResult.OK)
                {
                    _manifest.RemoveAccount(_currentAccount, false);
                    MessageBox.Show("Account removed from manifest.\nYou can now move its maFile to another computer and import it using the File menu.", "Remove from manifest");
                    LoadAccountsList();
                }
            }
        }

        private void MenuLoginAgain_Click(object sender, EventArgs e)
        {
            PromptRefreshLogin(_currentAccount);
        }

        private void MenuImportAccount_Click(object sender, EventArgs e)
        {
            ImportAccountForm currentImport_maFile_Form = new();
            currentImport_maFile_Form.ShowDialog();
            LoadAccountsList();
        }

        private void MenuSettings_Click(object sender, EventArgs e)
        {
            new SettingsForm().ShowDialog();
            _manifest = Manifest.GetManifest(true);
            LoadSettings();
        }

        private async void MenuDeactivateAuthenticator_Click(object sender, EventArgs e)
        {
            if (_currentAccount == null) return;

            // Check for a valid refresh token first
            if (_currentAccount.Session.IsRefreshTokenExpired())
            {
                MessageBox.Show("Your session has expired. Use the login again button under the selected account menu.", "Deactivate Authenticator", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // Check for a valid access token, refresh it if needed
            if (_currentAccount.Session.IsAccessTokenExpired())
            {
                try
                {
                    await _currentAccount.Session.RefreshAccessToken();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Deactivate Authenticator Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
            }

            DialogResult res = MessageBox.Show("Would you like to remove Steam Guard completely?\nYes - Remove Steam Guard completely.\nNo - Switch back to Email authentication.", "Deactivate Authenticator: " + _currentAccount.AccountName, MessageBoxButtons.YesNoCancel);
            int scheme = 0;
            if (res == DialogResult.Yes)
            {
                scheme = 2;
            }
            else if (res == DialogResult.No)
            {
                scheme = 1;
            }
            else if (res == DialogResult.Cancel)
            {
                scheme = 0;
            }

            if (scheme != 0)
            {
                string confCode = _currentAccount.GenerateSteamGuardCode();
                InputForm confirmationDialog = new(string.Format("Removing Steam Guard from {0}. Enter this confirmation code: {1}", _currentAccount.AccountName, confCode));
                confirmationDialog.ShowDialog();

                if (confirmationDialog.Canceled)
                {
                    return;
                }

                string enteredCode = confirmationDialog.txtBox.Text.ToUpper();
                if (enteredCode != confCode)
                {
                    MessageBox.Show("Confirmation codes do not match. Steam Guard not removed.");
                    return;
                }

                bool success = await _currentAccount.DeactivateAuthenticator(scheme);
                if (success)
                {
                    MessageBox.Show(string.Format("Steam Guard {0}. maFile will be deleted after hitting okay. If you need to make a backup, now's the time.", (scheme == 2 ? "removed completely" : "switched to emails")));
                    _manifest.RemoveAccount(_currentAccount);
                    LoadAccountsList();
                }
                else
                {
                    MessageBox.Show("Steam Guard failed to deactivate.");
                }
            }
            else
            {
                MessageBox.Show("Steam Guard was not removed. No action was taken.");
            }
        }

        // Tray menu handlers
        private void TrayIcon_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            TrayRestore_Click(sender, EventArgs.Empty);
        }

        private void TrayRestore_Click(object sender, EventArgs e)
        {
            this.Show();
            this.WindowState = FormWindowState.Normal;
        }

        private void TrayQuit_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }

        private void TrayTradeConfirmations_Click(object sender, EventArgs e)
        {
            BtnTradeConfirmations_Click(sender, e);
        }

        private void TrayCopySteamGuard_Click(object sender, EventArgs e)
        {
            if (txtLoginToken.Text != "")
            {
                Clipboard.SetText(txtLoginToken.Text);
            }
        }

        private void TrayAccountList_SelectedIndexChanged(object sender, EventArgs e)
        {
            listAccounts.SelectedIndex = trayAccountList.SelectedIndex;
        }


        // Misc UI handlers
        private void ListAccounts_SelectedValueChanged(object sender, EventArgs e)
        {
            if (listAccounts.SelectedIndex < 0) return;
            var accountName = listAccounts.Items[listAccounts.SelectedIndex] as string;

            for (int i = 0; i < _allAccounts.Length; i++)
            {
                SteamGuardAccount account = _allAccounts[i];
                if (account.AccountName == accountName)
                {
                    trayAccountList.Text = account.AccountName;
                    _currentAccount = account;
                    LoadAccountInfo();
                    break;
                }
            }
        }

        private void TxtAccSearch_TextChanged(object sender, EventArgs e)
        {
            List<string> names = new(GetAllNames());
            names = names.FindAll(new Predicate<string>(IsFilter));

            listAccounts.Items.Clear();
            listAccounts.Items.AddRange([.. names]);

            trayAccountList.Items.Clear();
            trayAccountList.Items.AddRange([.. names]);
        }


        // Timers
        private async Task TimerSteamGuard_TickAsync()
        {
            lblStatus.Text = "Aligning time with Steam...";
            _steamTime = await TimeAligner.GetSteamTimeAsync().ConfigureAwait(false);
            lblStatus.Text = string.Empty;

            _currentSteamChunk = _steamTime / 30L;
            LoadAccountInfo();

            if (_currentAccount != null)
            {
                pbTimeout.Value = 30 - (int)(_steamTime - (_currentSteamChunk * 30L));
            }
        }

        private async void TimerSteamGuard_Tick(object sender, EventArgs e)
        {
            try
            {
                timerSteamGuard.Enabled = false;
                await TimerSteamGuard_TickAsync().ConfigureAwait(false);
            }
            finally
            {
                timerSteamGuard.Enabled = true;
            }
        }

        private async void TimerTradesPopup_Tick(object sender, EventArgs e)
        {
            try
            {
                timerTradesPopup.Enabled = false;
                await TimerTradesPopup_TickAsync().ConfigureAwait(false);
            }
            finally
            {
                timerTradesPopup.Enabled = true;
            }
        }

        private async Task TimerTradesPopup_TickAsync()
        {
            if (_currentAccount == null || _popupFrm.Visible) return;
            if (!await _confirmationsSemaphore.WaitAsync(0).ConfigureAwait(false)) return;

            try
            {
                lblStatus.Text = "Checking confirmations...";

                var accounts = _manifest.CheckAllAccounts ? _allAccounts : [_currentAccount];
                var (Confirmations, AutoAcceptConfirmations) = await ProcessAccountsConfirmationsAsync(accounts);

                if (Confirmations.Count > 0)
                {
                    _popupFrm.Confirmations = [.. Confirmations];
                    _popupFrm.Popup();
                }

                await ProcessAutoAcceptConfirmations(AutoAcceptConfirmations).ConfigureAwait(false);
            }
            finally
            {
                lblStatus.Text = string.Empty;
                _confirmationsSemaphore.Release();
            }
        }

        private async Task<(List<Confirmation> Confirmations,
            Dictionary<SteamGuardAccount, List<Confirmation>> AutoAcceptConfirmations)>
            ProcessAccountsConfirmationsAsync(SteamGuardAccount[] accounts)
        {
            var confirmations = new List<Confirmation>();
            var autoAcceptConfirmations = new Dictionary<SteamGuardAccount, List<Confirmation>>();

            foreach (var acc in accounts)
            {
                if (acc.Session.IsRefreshTokenExpired())
                {
                    MessageBox.Show($"Your session for account {acc.AccountName} has expired.",
                        "Trade Confirmations",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                    PromptRefreshLogin(acc);
                    continue;
                }

                if (acc.Session.IsAccessTokenExpired())
                {
                    try
                    {
                        lblStatus.Text = "Refreshing session...";
                        await acc.Session.RefreshAccessToken().ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.Message, "Steam Login Error",
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                        continue;
                    }
                }

                try
                {
                    var fetchedConfirmations = await acc.FetchConfirmationsAsync().ConfigureAwait(false);
                    ProcessFetchedConfirmations(acc, fetchedConfirmations, confirmations, autoAcceptConfirmations);
                }
                catch { }
            }

            return (confirmations, autoAcceptConfirmations);
        }

        private void ProcessFetchedConfirmations(
            SteamGuardAccount account,
            Confirmation[] confirmations,
            List<Confirmation> normalConfirmations,
            Dictionary<SteamGuardAccount, List<Confirmation>> autoAcceptConfirmations)
        {
            foreach (var conf in confirmations)
            {
                if ((conf.ConfType == Confirmation.EMobileConfirmationType.MarketListing && _manifest.AutoConfirmMarketTransactions) ||
                    (conf.ConfType == Confirmation.EMobileConfirmationType.Trade && _manifest.AutoConfirmTrades))
                {
                    if (!autoAcceptConfirmations.ContainsKey(account))
                        autoAcceptConfirmations[account] = [];
                    autoAcceptConfirmations[account].Add(conf);
                }
                else
                {
                    normalConfirmations.Add(conf);
                }
            }
        }

        private static async Task ProcessAutoAcceptConfirmations(
            Dictionary<SteamGuardAccount, List<Confirmation>> autoAcceptConfirmations)
        {
            foreach (var (account, confirmations) in autoAcceptConfirmations)
            {
                await account.AcceptMultipleConfirmations([.. confirmations]).ConfigureAwait(false);
            }
        }

        // Other methods

        private void CopyLoginToken()
        {
            string text = txtLoginToken.Text;
            if (string.IsNullOrEmpty(text))
                return;
            Clipboard.SetText(text);
        }

        /// <summary>
        /// Display a login form to the user to refresh their OAuth Token
        /// </summary>
        /// <param name="account">The account to refresh</param>
        public static void PromptRefreshLogin(SteamGuardAccount account)
        {
            var loginForm = new LoginForm(LoginForm.LoginType.Refresh, account);
            _ = loginForm.ShowDialog();
        }

        /// <summary>
        /// Load UI with the current account info, this is run every second
        /// </summary>
        private void LoadAccountInfo()
        {
            if (_currentAccount != null && _steamTime != 0)
            {
                _popupFrm.Account = _currentAccount;
                txtLoginToken.Text = _currentAccount.GenerateSteamGuardCodeForTime(_steamTime);
                groupAccount.Text = "Account: " + _currentAccount.AccountName;
            }
        }

        /// <summary>
        /// Decrypts files and populates list UI with accounts
        /// </summary>
        private void LoadAccountsList()
        {
            _currentAccount = null;
            listAccounts.Items.Clear();
            trayAccountList.Items.Clear();

            _allAccounts = _manifest.GetAllAccounts(_passKey);
            if (_allAccounts.Length == 0) return;

            var accountNames = _allAccounts.Select(static a => a.AccountName).ToArray();
            listAccounts.Items.AddRange(accountNames);
            trayAccountList.Items.AddRange(accountNames);

            listAccounts.SelectedIndex = trayAccountList.SelectedIndex = 0;
            listAccounts.Sorted = trayAccountList.Sorted = true;

            menuDeactivateAuthenticator.Enabled = btnTradeConfirmations.Enabled = true;
        }

        private void ListAccounts_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Control)
            {
                if (e.KeyCode == Keys.Up || e.KeyCode == Keys.Down)
                {
                    int to = listAccounts.SelectedIndex - (e.KeyCode == Keys.Up ? 1 : -1);
                    _manifest.MoveEntry(listAccounts.SelectedIndex, to);
                    LoadAccountsList();
                }
                return;
            }

            if (!IsKeyAChar(e.KeyCode) && !IsKeyADigit(e.KeyCode))
            {
                return;
            }

            txtAccSearch.Focus();
            txtAccSearch.Text = e.KeyCode.ToString();
            txtAccSearch.SelectionStart = 1;
        }

        private static bool IsKeyAChar(Keys key)
        {
            return key >= Keys.A && key <= Keys.Z;
        }

        private static bool IsKeyADigit(Keys key)
        {
            return (key >= Keys.D0 && key <= Keys.D9) || (key >= Keys.NumPad0 && key <= Keys.NumPad9);
        }

        private bool IsFilter(string f)
        {
            if (txtAccSearch.Text.StartsWith('~'))
            {
                try
                {
                    return Regex.IsMatch(f, txtAccSearch.Text);
                }
                catch (Exception)
                {
                    return true;
                }

            }
            else
            {
                return f.Contains(txtAccSearch.Text, StringComparison.CurrentCultureIgnoreCase);
            }
        }

        private string[] GetAllNames()
        {
            var itemArray = new string[_allAccounts.Length];
            for (int i = 0; i < itemArray.Length; i++)
            {
                itemArray[i] = _allAccounts[i].AccountName;
            }
            return itemArray;
        }

        private void LoadSettings()
        {
            timerTradesPopup.Enabled = _manifest.PeriodicChecking;
            timerTradesPopup.Interval = _manifest.PeriodicCheckingInterval * 1000;
        }

        private void MainForm_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.C && e.Modifiers == Keys.Control)
            {
                CopyLoginToken();
            }
        }

        private void PanelButtons_SizeChanged(object sender, EventArgs e)
        {
            int totButtons = panelButtons.Controls.OfType<Button>().Count();

            Point curPos = new(0, 0);
            foreach (var but in panelButtons.Controls.OfType<Button>())
            {
                but.Width = panelButtons.Width / totButtons;
                but.Location = curPos;
                curPos = new Point(curPos.X + but.Width, 0);
            }
        }
    }
}
