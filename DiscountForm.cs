using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;


namespace POSMilkbar
{
    public partial class DiscountForm : Form
    {
        public string EnteredCode { get; private set; }

        public DiscountForm()
        {
            InitializeComponent();
        }

        private void btnApply_Click(object sender, EventArgs e)
        {
            EnteredCode = txtCode.Text.Trim().ToUpper();
            this.DialogResult = DialogResult.OK;
            this.Close();
        }
    }
}
