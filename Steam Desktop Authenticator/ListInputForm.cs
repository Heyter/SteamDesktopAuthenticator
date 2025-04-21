using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace Steam_Desktop_Authenticator
{
    public partial class ListInputForm : Form
    {
        public ListInputForm(List<string> options)
        {
            this.DoubleBuffered = true;
            Items = options;
            InitializeComponent();
        }

        public int SelectedIndex;
        private readonly List<string> Items;

        private void ListInputForm_Load(object sender, EventArgs e)
        {
            for (int i = 0; i < Items.Count; i++)
            {
                string item = Items[i];
                lbItems.Items.Add(item);
            }
        }

        private void BtnAccept_Click(object sender, EventArgs e)
        {
            if (lbItems.SelectedIndex != -1)
            {
                SelectedIndex = lbItems.SelectedIndex;
                this.Close();
            }
        }

        private void BtnCancel_Click(object sender, EventArgs e)
        {
            this.Close();
        }
    }
}
