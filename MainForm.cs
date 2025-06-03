using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using System.Windows.Forms;

namespace POSMilkbar
{
    

    public partial class MainForm : Form
    {
        string userRole;
        private decimal appliedDiscount = 0;
        private bool isPromoApplied = false;

        public MainForm(string role)
        {
            InitializeComponent();
            userRole = role;

            if (userRole.Equals("Team Member", StringComparison.OrdinalIgnoreCase))
            {
               
                tabMain.TabPages.Remove(tabInventory);
                
            }
            if (userRole.Equals("System Admin", StringComparison.OrdinalIgnoreCase))
            {

                tabMain.TabPages.Remove(tabReports);
            }
            InitializeCartColumns();
            LoadProducts();
        }

        private void InitializeCartColumns()
        {
            dgvCart.Columns.Clear();
            dgvCart.Columns.Add("ProductIDCol", "Product ID");
            dgvCart.Columns["ProductIDCol"].Visible = false;
            dgvCart.Columns.Add("ProductNameCol", "Product Name");
            dgvCart.Columns.Add("PriceCol", "Unit Price");
            dgvCart.Columns.Add("QuantityCol", "Quantity");
            dgvCart.Columns.Add("TotalCol", "Total");
        }

        private void LoadProducts()
        {
            string connectionString = @"Data Source=(LocalDB)\MSSQLLocalDB;AttachDbFilename=C:\USERS\MARZA\DOCUMENTS\POSMB\POSMILKBAR.MDF;Integrated Security=True";

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();
                string query = "SELECT ProductID, Name FROM Products";
                SqlCommand cmd = new SqlCommand(query, conn);
                SqlDataReader reader = cmd.ExecuteReader();

                Dictionary<int, string> productList = new Dictionary<int, string>();
                while (reader.Read())
                {
                    int id = Convert.ToInt32(reader["ProductID"]);
                    string name = reader["Name"].ToString();
                    productList.Add(id, name);
                }

                cmbProducts.DataSource = new BindingSource(productList, null);
                cmbProducts.DisplayMember = "Value";
                cmbProducts.ValueMember = "Key";
            }
        }

        private void btnAddToCart_Click(object sender, EventArgs e)
        {
            if (cmbProducts.SelectedItem == null || string.IsNullOrWhiteSpace(txtQuantity.Text))
            {
                MessageBox.Show("Please select a product and enter quantity.");
                return;
            }

            int productId = ((KeyValuePair<int, string>)cmbProducts.SelectedItem).Key;
            string productName = ((KeyValuePair<int, string>)cmbProducts.SelectedItem).Value;

            if (!int.TryParse(txtQuantity.Text, out int quantity) || quantity <= 0)
            {
                MessageBox.Show("Please enter a valid quantity.");
                return;
            }

            string connectionString = @"Data Source=(LocalDB)\MSSQLLocalDB;AttachDbFilename=C:\USERS\MARZA\DOCUMENTS\POSMB\POSMILKBAR.MDF;Integrated Security=True";

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();
                string query = "SELECT Price, Stock FROM Products WHERE ProductID = @id";
                SqlCommand cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@id", productId);

                SqlDataReader reader = cmd.ExecuteReader();
                if (reader.Read())
                {
                    decimal price = (decimal)reader["Price"];
                    int stock = (int)reader["Stock"];

                    if (quantity > stock)
                    {
                        MessageBox.Show("Not enough stock available.");
                        return;
                    }

                    decimal total = price * quantity;
                    dgvCart.Rows.Add(productId, productName, price, quantity, total);
                    lblTotal.Text = $"Grand Total: ${CalculateTotal():F2}";
                }
            }
        }

        private void btnApplyPromo_Click(object sender, EventArgs e)
        {
            string code = "";
            Dictionary<string, decimal> discountCodes = new Dictionary<string, decimal>()
            {
                { "SAVE10", 10 },
                { "SUMMER20", 20 },
                { "FREEDAY", 100 }
            };

            using (DiscountForm form = new DiscountForm())
            {
                if (form.ShowDialog() == DialogResult.OK)
                {
                    code = form.EnteredCode;
                    if (!string.IsNullOrEmpty(code) && discountCodes.ContainsKey(code))
                    {
                        appliedDiscount = discountCodes[code];
                        MessageBox.Show($"Promo code applied! ({appliedDiscount}% off)");
                    }
                    else
                    {
                        appliedDiscount = 0;
                        MessageBox.Show("Invalid promo code. No discount will be applied.");
                    }
                }
            }

            decimal totalBefore = CalculateTotal();
            decimal totalAfter = totalBefore * (1 - appliedDiscount / 100);
            lblTotal.Text = $"Grand Total: ${totalAfter:F2} (Discount: {appliedDiscount}%)";
        }
        private void btnCheckout_Click(object sender, EventArgs e)
        {
            if (dgvCart.Rows.Count <= 1)
            {
                MessageBox.Show("Cart is empty. Add items before checkout.");
                return;
            }

            // Step 1: If promo hasn’t been handled yet, show promo dialog now
            if (!isPromoApplied)
            {
                string code = "";
                Dictionary<string, decimal> discountCodes = new Dictionary<string, decimal>()
        {
            { "SAVE10", 10 },
            { "SUMMER20", 20 },
            { "FREEDAY", 100 }
        };

                using (DiscountForm form = new DiscountForm())
                {
                    var result = form.ShowDialog();

                    if (result == DialogResult.Cancel)
                    {
                        MessageBox.Show("Promo code entry cancelled.");
                        // Mark promo as “handled” so we don’t loop back here
                        isPromoApplied = true;
                        return;
                    }

                    if (result == DialogResult.OK)
                    {
                        code = form.EnteredCode;
                        if (!string.IsNullOrEmpty(code) && discountCodes.ContainsKey(code))
                        {
                            appliedDiscount = discountCodes[code];
                            isPromoApplied = true;

                            decimal totalBefore = CalculateTotal();
                            decimal totalAfter = totalBefore * (1 - appliedDiscount / 100);
                            lblTotal.Text = $"Grand Total: ${totalAfter:F2} (Discount: {appliedDiscount}%)";

                            MessageBox.Show("Promo code applied! Click Pay Now again to complete the transaction.");
                            return;
                        }
                        else
                        {
                            MessageBox.Show("Invalid promo code. Try again.");
                            return;
                        }
                    }
                }
            }


            
            string connectionString = @"Data Source=(LocalDB)\MSSQLLocalDB;AttachDbFilename=C:\USERS\MARZA\DOCUMENTS\POSMB\POSMILKBAR.MDF;Integrated Security=True";

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();
                SqlTransaction transaction = conn.BeginTransaction();

                try
                {
                    decimal totalBeforeDiscount = CalculateTotal();
                    decimal discountedTotal = totalBeforeDiscount * (1 - appliedDiscount / 100);

                    string insertTransaction =
                        "INSERT INTO Transactions (Date, TotalAmount) " +
                        "OUTPUT INSERTED.TransactionID " +
                        "VALUES (@Date, @TotalAmount)";
                    SqlCommand cmdTransaction = new SqlCommand(insertTransaction, conn, transaction);
                    cmdTransaction.Parameters.AddWithValue("@Date", DateTime.Now);
                    cmdTransaction.Parameters.AddWithValue("@TotalAmount", discountedTotal);

                    int transactionID = (int)cmdTransaction.ExecuteScalar();

                    foreach (DataGridViewRow row in dgvCart.Rows)
                    {
                        if (row.IsNewRow) continue;

                        int productId = Convert.ToInt32(row.Cells["ProductIDCol"].Value);
                        int quantity = Convert.ToInt32(row.Cells["QuantityCol"].Value);
                        decimal price = Convert.ToDecimal(row.Cells["PriceCol"].Value);

                        SqlCommand cmdItem = new SqlCommand(
                            "INSERT INTO TransactionItems (TransactionID, ProductID, Quantity, Price) " +
                            "VALUES (@TID, @PID, @Qty, @Price)", conn, transaction);
                        cmdItem.Parameters.AddWithValue("@TID", transactionID);
                        cmdItem.Parameters.AddWithValue("@PID", productId);
                        cmdItem.Parameters.AddWithValue("@Qty", quantity);
                        cmdItem.Parameters.AddWithValue("@Price", price);
                        cmdItem.ExecuteNonQuery();

                        SqlCommand cmdStock = new SqlCommand(
                            "UPDATE Products SET Stock = Stock - @Qty WHERE ProductID = @PID", conn, transaction);
                        cmdStock.Parameters.AddWithValue("@Qty", quantity);
                        cmdStock.Parameters.AddWithValue("@PID", productId);
                        cmdStock.ExecuteNonQuery();
                    }

                    transaction.Commit();
                    MessageBox.Show("Checkout complete!");

                    DialogResult receiptOption = MessageBox.Show(
                        "Print receipt?",
                        "Receipt",
                        MessageBoxButtons.OKCancel,
                        MessageBoxIcon.Question);

                    if (receiptOption == DialogResult.OK)
                        MessageBox.Show("Your receipt has been printed.");
                    else
                        MessageBox.Show("THANKS FOR GOING PAPERLESS!");

                    dgvCart.Rows.Clear();
                    lblTotal.Text = "Grand Total: $0.00";
                    appliedDiscount = 0;
                    isPromoApplied = false;
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    MessageBox.Show("Error during checkout: " + ex.Message);
                }
            }
        }





        private decimal CalculateTotal()
        {
            decimal total = 0;
            foreach (DataGridViewRow row in dgvCart.Rows)
            {
                if (!row.IsNewRow && row.Cells["TotalCol"].Value != null)
                {
                    total += Convert.ToDecimal(row.Cells["TotalCol"].Value);
                }
            }
            return total;
        }
        private void lblTotal_Click(object sender, EventArgs e) { }

        private void dgvCart_CellContentClick(object sender, DataGridViewCellEventArgs e) { }

        private void tabReports_Click(object sender, EventArgs e) { }

        private void label4_Click(object sender, EventArgs e) { }

        private void dateTimePicker1_ValueChanged_1(object sender, EventArgs e) { }

        private void label2_Click(object sender, EventArgs e) { }

        private void dateTimePicker1_ValueChanged(object sender, EventArgs e) { }

        private void label1_Click(object sender, EventArgs e) { }

    
        private void btnGenerateReport_Click(object sender, EventArgs e)
        {
            LoadFilteredReports();
        }

        private void LoadFilteredReports()
        {
            string connectionString = @"Data Source=(LocalDB)\MSSQLLocalDB;AttachDbFilename=C:\USERS\MARZA\DOCUMENTS\POSMB\POSMILKBAR.MDF;Integrated Security=True";
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();

                string query = @"
            SELECT 
                t.TransactionID, 
                t.Date, 
                p.Name AS ProductName, 
                ti.Quantity, 
                ti.Price, 
                (ti.Quantity * ti.Price) AS Total
            FROM Transactions t
            INNER JOIN TransactionItems ti ON t.TransactionID = ti.TransactionID
            INNER JOIN Products p ON ti.ProductID = p.ProductID
            WHERE t.Date BETWEEN @Start AND @End
            ORDER BY t.Date DESC";

                SqlCommand cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@Start", dtStart.Value.Date);
                cmd.Parameters.AddWithValue("@End", dtEnd.Value.Date.AddDays(1).AddSeconds(-1));

                SqlDataAdapter adapter = new SqlDataAdapter(cmd);
                DataTable dt = new DataTable();
                adapter.Fill(dt);

                

                dgvReports.AutoGenerateColumns = true;
                dgvReports.DataSource = dt;

                decimal total = 0;
                foreach (DataRow row in dt.Rows)
                {
                    total += Convert.ToDecimal(row["Total"]);
                }
                lblReportSummary.Text = $"Total Sales: ${total:F2}";
            }
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            LoadInventoryData(); // auto-loads products into grid
        }
        private void LoadInventoryData()
        {
            try
            {
                string connString = @"Data Source=(LocalDB)\MSSQLLocalDB;AttachDbFilename=C:\USERS\MARZA\DOCUMENTS\POSMB\POSMILKBAR.MDF;Integrated Security=True";
                string query = "SELECT * FROM Products";

                using (SqlConnection conn = new SqlConnection(connString))
                {
                    conn.Open();
                    SqlDataAdapter adapter = new SqlDataAdapter(query, conn);
                    DataTable dt = new DataTable();
                    adapter.Fill(dt);
                    dgvInventory.DataSource = dt;
                }

                dgvInventory.AutoGenerateColumns = true;
                MessageBox.Show("Inventory loaded.");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error: " + ex.Message);
            }
        }







        public MainForm()
        {
            InitializeComponent();
            LoadInventoryData();
        }
        private void dgvInventory_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0)
            {
                DataGridViewRow row = dgvInventory.Rows[e.RowIndex];
                txtProductName.Text = row.Cells["Name"].Value.ToString();
                txtProductPrice.Text = row.Cells["Price"].Value.ToString();
                txtProductStock.Text = row.Cells["Stock"].Value.ToString();
            }
        }

        private void btnUpdateProduct_Click(object sender, EventArgs e)
        {
            string name = txtProductName.Text.Trim();
            string priceText = txtProductPrice.Text.Trim();
            string stockText = txtProductStock.Text.Trim();

            if (string.IsNullOrEmpty(name) || !decimal.TryParse(priceText, out decimal price) || !int.TryParse(stockText, out int stock))
            {
                MessageBox.Show("Please enter a valid product name, price, and stock.");
                return;
            }

            string connString = @"Data Source=(LocalDB)\MSSQLLocalDB;AttachDbFilename=C:\USERS\MARZA\DOCUMENTS\POSMB\POSMILKBAR.MDF;Integrated Security=True";

            using (SqlConnection conn = new SqlConnection(connString))
            {
                conn.Open();

                // Check if product with the same name exists
                SqlCommand checkCmd = new SqlCommand("SELECT COUNT(*) FROM Products WHERE Name = @name", conn);
                checkCmd.Parameters.AddWithValue("@name", name);
                int count = (int)checkCmd.ExecuteScalar();

                if (count > 0)
                {
                    // Update stock if it exists
                    SqlCommand updateCmd = new SqlCommand("UPDATE Products SET Stock = Stock + @addStock WHERE Name = @name", conn);
                    updateCmd.Parameters.AddWithValue("@addStock", stock);
                    updateCmd.Parameters.AddWithValue("@name", name);
                    updateCmd.ExecuteNonQuery();

                    MessageBox.Show("Existing product updated.");
                }
                else
                {
                    // new product
                    SqlCommand insertCmd = new SqlCommand("INSERT INTO Products (Name, Price, Stock) VALUES (@name, @price, @stock)", conn);
                    insertCmd.Parameters.AddWithValue("@name", name);
                    insertCmd.Parameters.AddWithValue("@price", price);
                    insertCmd.Parameters.AddWithValue("@stock", stock);
                    insertCmd.ExecuteNonQuery();

                    MessageBox.Show("New product added.");
                }
            }

            LoadInventoryData();
        }


        private void label4_Click_1(object sender, EventArgs e)
        {

        }



        private void btnDelete_Click(object sender, EventArgs e)
        {
            if (dgvCart.SelectedRows.Count > 0)
            {
                foreach (DataGridViewRow row in dgvCart.SelectedRows)
                {
                    dgvCart.Rows.Remove(row);
                }
            }
            else
            {
                MessageBox.Show("Please select a row to delete.");
            }
        }

    }
}

