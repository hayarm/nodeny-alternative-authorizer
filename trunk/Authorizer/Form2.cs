using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace Authorizer
{
    public partial class DomainWindow : Form
    {
        public DomainWindow(string domain)
        {
            InitializeComponent();
            textBoxDomain.Text = domain;
        }

        private void buttonSave_Click(object sender, EventArgs e)
        {
            try
            {
                Registry.LocalMachine.DeleteSubKey(@"SOFTWARE\AuthorizerDomain");
            }
            catch
            {
                // don't want to delete? I can live with that
            }
            RegistryKey domainKey = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\AuthorizerDomain");
            if (domainKey != null)
            {
                try
                {
                    domainKey.SetValue("domain", textBoxDomain.Text);
                }
                catch (Exception ex)
                {
                    return;
                }
                finally
                {
                    domainKey.Close();
                    this.DialogResult = DialogResult.OK;
                    this.Close();
                }
            }

            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        private void buttonCancel_Click(object sender, EventArgs e)
        {
            this.Close();
        }
    }
}
