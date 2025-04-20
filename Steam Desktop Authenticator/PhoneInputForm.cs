using SteamAuth;
using System;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace Steam_Desktop_Authenticator
{
    public partial class PhoneInputForm : Form
    {
        private readonly SteamGuardAccount Account;
        public string PhoneNumber;
        public string CountryCode;
        public bool Canceled;

        public PhoneInputForm(SteamGuardAccount account)
        {
            this.Account = account;
            InitializeComponent();
        }

        private void BtnSubmit_Click(object sender, EventArgs e)
        {
            this.PhoneNumber = txtPhoneNumber.Text;
            this.CountryCode = txtCountryCode.Text;

            if (this.PhoneNumber[0] != '+')
            {
                MessageBox.Show("Phone number must start with + and country code.", "Phone Number", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            this.Close();
        }

        private void TxtPhoneNumber_KeyPress(object sender, KeyPressEventArgs e)
        {
            // Allow pasting
            if (char.IsControl(e.KeyChar))
                return;

            // Only allow numbers, spaces, and +
            var regex = MyRegex1();
            if (regex.IsMatch(e.KeyChar.ToString()))
            {
                e.Handled = true;
            }
        }

        private void TxtCountryCode_KeyPress(object sender, KeyPressEventArgs e)
        {
            // Allow pasting
            if (char.IsControl(e.KeyChar))
                return;

            // Only allow letters
            var regex = MyRegex();
            if (regex.IsMatch(e.KeyChar.ToString()))
            {
                e.Handled = true;
            }
        }

        private void TxtCountryCode_Leave(object sender, EventArgs e)
        {
            // Always uppercase
            txtCountryCode.Text = txtCountryCode.Text.ToUpper();
        }

        private void BtnCancel_Click(object sender, EventArgs e)
        {
            this.Canceled = true;
            this.Close();
        }

        [GeneratedRegex(@"[^a-zA-Z]")]
        private static partial Regex MyRegex();
        [GeneratedRegex(@"[^0-9\s\+]")]
        private static partial Regex MyRegex1();
    }
}
