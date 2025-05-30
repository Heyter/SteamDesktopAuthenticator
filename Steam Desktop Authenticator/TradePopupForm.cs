﻿using SteamAuth;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace Steam_Desktop_Authenticator
{
    public partial class TradePopupForm : Form
    {
        private SteamGuardAccount acc;
        private List<Confirmation> confirms = [];
        private bool deny2, accept2;

        public TradePopupForm()
        {
            this.DoubleBuffered = true;
            InitializeComponent();
            lblStatus.Text = "";
        }

        public SteamGuardAccount Account
        {
            get { return acc; }
            set { acc = value; lblAccount.Text = acc.AccountName; }
        }

        public Confirmation[] Confirmations
        {
            get { return [.. confirms]; }
            set { confirms = new List<Confirmation>(value); }
        }

        private void TradePopupForm_Load(object sender, EventArgs e)
        {
            this.Location = (Point)Size.Subtract(Screen.GetWorkingArea(this).Size, this.Size);
        }

        private void BtnAccept_Click(object sender, EventArgs e)
        {
            if (!accept2)
            {
                // Allow user to confirm first
                lblStatus.Text = "Press Accept again to confirm";
                btnAccept.BackColor = Color.FromArgb(128, 255, 128);
                accept2 = true;
            }
            else
            {
                lblStatus.Text = "Accepting...";
                _ = acc.AcceptConfirmation(confirms[0]);
                confirms.RemoveAt(0);
                Reset();
            }
        }

        private void BtnDeny_Click(object sender, EventArgs e)
        {
            if (!deny2)
            {
                lblStatus.Text = "Press Deny again to confirm";
                btnDeny.BackColor = Color.FromArgb(255, 255, 128);
                deny2 = true;
            }
            else
            {
                lblStatus.Text = "Denying...";
                _ = acc.DenyConfirmation(confirms[0]);
                confirms.RemoveAt(0);
                Reset();
            }
        }

        private void Reset()
        {
            deny2 = false;
            accept2 = false;
            btnAccept.BackColor = Color.FromArgb(192, 255, 192);
            btnDeny.BackColor = Color.FromArgb(255, 255, 192);

            btnAccept.Text = "Accept";
            btnDeny.Text = "Deny";
            lblAccount.Text = "";
            lblStatus.Text = "";

            if (confirms.Count == 0)
            {
                this.Hide();
            }
            else
            {
                //TODO: Re-add confirmation description support to SteamAuth.
                lblDesc.Text = "Confirmation";
            }
        }

        public void Popup()
        {
            Reset();
            this.Show();
        }
    }
}
