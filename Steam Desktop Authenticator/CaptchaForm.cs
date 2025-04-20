using System;
using System.Windows.Forms;

namespace Steam_Desktop_Authenticator
{
    public partial class CaptchaForm : Form
    {
        public bool Canceled = false;
        public string CaptchaGID = "";
        public string CaptchaURL = "";
        public string CaptchaCode
        {
            get
            {
                return txtBox.Text;
            }
        }

        public CaptchaForm(string GID)
        {
            this.CaptchaGID = GID;
            this.CaptchaURL = "https://steamcommunity.com/public/captcha.php?gid=" + GID;
            InitializeComponent();
            this.pictureBoxCaptcha.Load(CaptchaURL);
        }

        private void BtnAccept_Click(object sender, EventArgs e)
        {
            this.Canceled = false;
            this.Close();
        }

        private void BtnCancel_Click(object sender, EventArgs e)
        {
            this.Canceled = true;
            this.Close();
        }
    }
}
