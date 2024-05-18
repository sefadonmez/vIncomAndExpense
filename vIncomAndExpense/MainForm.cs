using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Data.SQLite;
using System.Drawing.Printing;
using System.IO;
using System.Xml.Linq;
using iTextSharp.text;
using iTextSharp.text.pdf;
using System.Net.Mail;
using System.Net;
using System.Xml;
using System.Diagnostics;
namespace vIncomAndExpense
{
    public partial class MainForm : Form
    {
        private const string ConnectionString = "Data Source=Database/data.db;Version=3;";
        public MainForm()
        {
            InitializeComponent();
            InitializeDatabase();
            LoadTransactions();
            UpdateTotals();
            txtAmount.KeyPress += new KeyPressEventHandler(txtAmount_KeyPress); // txtAmount için KeyPress olayına işleyici ekleme
            timer1.Interval = 1000; // Zamanlayıcı her bir saniyede bir etkinleştirilir
            timer1.Tick += timer1_Tick; // Zamanlayıcı etkin olduğunda çağrılacak olay
            timer1.Start(); // Zamanlayıcıyı başlat
        }

        public class SmtpSettings
        {
            public string Host { get; set; }
            public int Port { get; set; }
            public string Username { get; set; }
            public string Password { get; set; }
            public string FromAddress { get; set; }
            public string ToAddress { get; set; }
            public string Subject { get; set; }
        }
        public static void InitializeDatabase()
        {
            using (var connection = new SQLiteConnection(ConnectionString))
            {
                connection.Open();

                string createTableQuery = @"
                CREATE TABLE IF NOT EXISTS Transactions (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Type TEXT NOT NULL,
                    Amount REAL NOT NULL,
                    Description TEXT,
                    Date TEXT NOT NULL
                );";

                using (var command = new SQLiteCommand(createTableQuery, connection))
                {
                    command.ExecuteNonQuery();
                }
            }
        }

        private void UpdateTotals()
        {
            using (var connection = new SQLiteConnection(ConnectionString))
            {
                connection.Open();

                // Total Income
                string incomeQuery = "SELECT SUM(Amount) FROM Transactions WHERE Type='Income'";
                using (var incomeCommand = new SQLiteCommand(incomeQuery, connection))
                {
                    var result = incomeCommand.ExecuteScalar();
                    lblTotalIncome.Text = "Total Expense: " + (result != DBNull.Value ? Convert.ToDouble(result).ToString("C") : "0");
                }

                // Total Expense
                string expenseQuery = "SELECT SUM(Amount) FROM Transactions WHERE Type='Expense'";
                using (var expenseCommand = new SQLiteCommand(expenseQuery, connection))
                {
                    var result = expenseCommand.ExecuteScalar();
                    lblTotalExpense.Text = "Total Income: " + (result != DBNull.Value ? Convert.ToDouble(result).ToString("C") : "0");
                }
            }
        }

        private void LoadTransactions()
        {
            using (var connection = new SQLiteConnection(ConnectionString))
            {
                connection.Open();
                string selectQuery = "SELECT * FROM Transactions";

                using (var adapter = new SQLiteDataAdapter(selectQuery, connection))
                {
                    DataTable dataTable = new DataTable();
                    adapter.Fill(dataTable);
                    dataGridView.DataSource = dataTable;
                }
            }
        }

        private void ClearForm()
        {
            txtAmount.Clear();
            txtDescription.Clear();
            rbIncome.Checked = true;
        }

        private void btnAdd_Click(object sender, EventArgs e)
        {
            try
            {

                string type = rbIncome.Checked ? "Income" : "Expense";
                double amount = double.Parse(txtAmount.Text);
                string description = txtDescription.Text;
                string date = DateTime.Now.ToString("yyyy-MM-dd");

                using (var connection = new SQLiteConnection(ConnectionString))
                {
                    connection.Open();
                    string insertQuery = "INSERT INTO Transactions (Type, Amount, Description, Date) VALUES (@type, @amount, @description, @date)";

                    using (var command = new SQLiteCommand(insertQuery, connection))
                    {
                        command.Parameters.AddWithValue("@type", type);
                        command.Parameters.AddWithValue("@amount", amount);
                        command.Parameters.AddWithValue("@description", description);
                        command.Parameters.AddWithValue("@date", date);

                        command.ExecuteNonQuery();
                    }
                }

                LoadTransactions();
                UpdateTotals();
                ClearForm();

            }
            catch { }

        }

        private void btnDelete_Click(object sender, EventArgs e)
        {
            if (dataGridView.SelectedRows.Count > 0)
            {
                int selectedRowIndex = dataGridView.SelectedRows[0].Index;
                int id = Convert.ToInt32(dataGridView.Rows[selectedRowIndex].Cells["Id"].Value);

                using (var connection = new SQLiteConnection(ConnectionString))
                {
                    connection.Open();
                    string deleteQuery = "DELETE FROM Transactions WHERE Id=@id";

                    using (var command = new SQLiteCommand(deleteQuery, connection))
                    {
                        command.Parameters.AddWithValue("@id", id);
                        command.ExecuteNonQuery();
                    }
                }

                LoadTransactions();
                UpdateTotals();
            }
            else
            {
                MessageBox.Show("Please Select Datarow.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
        private string pdfFileName; // PDF dosyasının adını saklamak için değişken

        private void btnSaveToPDF_Click(object sender, EventArgs e)
        {
            SaveFileDialog saveFileDialog = new SaveFileDialog();
            saveFileDialog.Filter = "PDF Files|*.pdf";
            saveFileDialog.Title = "Save as PDF";
            saveFileDialog.FileName = "IncomeExpenseReport.pdf";

            if (saveFileDialog.ShowDialog() == DialogResult.OK)
            {
                pdfFileName = saveFileDialog.FileName;
                using (var connection = new SQLiteConnection(ConnectionString))
                {
                    connection.Open();
                    string selectQuery = "SELECT * FROM Transactions";

                    using (var adapter = new SQLiteDataAdapter(selectQuery, connection))
                    {
                        DataTable dataTable = new DataTable();
                        adapter.Fill(dataTable);

                        using (FileStream stream = new FileStream(saveFileDialog.FileName, FileMode.Create))
                        {
                            Document pdfDoc = new Document(PageSize.A4);
                            PdfWriter.GetInstance(pdfDoc, stream);
                            pdfDoc.Open();

                            // Title
                            var titleFont = FontFactory.GetFont("Arial", "10", Font.Bold);
                            Paragraph title = new Paragraph("Income and Expense Report", titleFont);
                            title.Alignment = Element.ALIGN_CENTER;
                            pdfDoc.Add(title);

                            pdfDoc.Add(new Paragraph(" ")); // Boşluk

                            PdfPTable table = new PdfPTable(dataTable.Columns.Count);
                            table.WidthPercentage = 100;

                            // Header
                            var headerFont = FontFactory.GetFont("Arial", "10", Font.Bold);
                            foreach (DataColumn column in dataTable.Columns)
                            {
                                PdfPCell cell = new PdfPCell(new Phrase(column.ColumnName, headerFont));
                                cell.BackgroundColor = BaseColor.LIGHT_GRAY;
                                table.AddCell(cell);
                            }

                            // Data
                            var cellFont = FontFactory.GetFont("Arial", 12);
                            foreach (DataRow row in dataTable.Rows)
                            {
                                foreach (var cell in row.ItemArray)
                                {
                                    table.AddCell(new Phrase(cell.ToString(), cellFont));
                                }
                            }

                            pdfDoc.Add(table);

                            // Totals
                            pdfDoc.Add(new Paragraph(" ")); // Boşluk
                            double totalIncome = 0;
                            double totalExpense = 0;

                            foreach (DataRow row in dataTable.Rows)
                            {
                                if (row["Type"].ToString() == "Income")
                                {
                                    totalIncome += Convert.ToDouble(row["Amount"]);
                                }
                                else if (row["Type"].ToString() == "Expense")
                                {
                                    totalExpense += Convert.ToDouble(row["Amount"]);
                                }
                            }

                            var totalFont = FontFactory.GetFont("Arial", "12", Font.Bold);
                            pdfDoc.Add(new Paragraph($"Total Income: {totalIncome:C}", totalFont));
                            pdfDoc.Add(new Paragraph($"Total Expense: {totalExpense:C}", totalFont));
                            pdfDoc.Add(new Paragraph($"Net Income: {(totalIncome - totalExpense):C}", totalFont));

                            pdfDoc.Close();
                            stream.Close();
                        }
                    }
                }
            }
        }

        private void btnSendEmail_Click(object sender, EventArgs e)
        {
            SmtpSettings smtpSettings = LoadSmtpSettings("smtpSettings.xml");

            if (smtpSettings == null)
            {
                MessageBox.Show("SMTP ayarları yüklenemedi.", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (string.IsNullOrEmpty(pdfFileName))
            {
                MessageBox.Show("Lütfen önce bir PDF dosyası oluşturun ve kaydedin.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                MailMessage mail = new MailMessage(smtpSettings.FromAddress, smtpSettings.ToAddress);
                mail.Subject = smtpSettings.Subject;
                mail.Body = "Gelir ve gider raporu ektedir.";

                Attachment attachment = new Attachment(pdfFileName);
                mail.Attachments.Add(attachment);

                SmtpClient smtpClient = new SmtpClient(smtpSettings.Host, smtpSettings.Port);
                smtpClient.UseDefaultCredentials = false;
                smtpClient.Credentials = new NetworkCredential(smtpSettings.Username, smtpSettings.Password);
                smtpClient.EnableSsl = true; // SSL/TLS şifreleme kullanılacaksa true olarak ayarlanmalı

                smtpClient.Send(mail);

                MessageBox.Show("Mail başarıyla gönderildi.", "Başarılı", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Mail gönderilirken bir hata oluştu: " + ex.Message, "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void txtAmount_KeyPress(object sender, KeyPressEventArgs e)
        {
            // Yalnızca rakamlar ve geri silme tuşu izin verilir
            if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar))
            {
                e.Handled = true;
            }
        }

        private SmtpSettings LoadSmtpSettings(string filePath)
        {
            SmtpSettings settings = new SmtpSettings();

            try
            {
                XmlDocument doc = new XmlDocument();
                doc.Load(filePath);

                settings.Host = doc.SelectSingleNode("/SmtpSettings/Host").InnerText;
                settings.Port = int.Parse(doc.SelectSingleNode("/SmtpSettings/Port").InnerText);
                settings.Username = doc.SelectSingleNode("/SmtpSettings/Username").InnerText;
                settings.Password = doc.SelectSingleNode("/SmtpSettings/Password").InnerText;
                settings.FromAddress = doc.SelectSingleNode("/SmtpSettings/FromAddress").InnerText;
                settings.ToAddress = doc.SelectSingleNode("/SmtpSettings/ToAddress").InnerText;
                settings.Subject = doc.SelectSingleNode("/SmtpSettings/Subject").InnerText;
            }
            catch (Exception ex)
            {
                MessageBox.Show("SMTP ayarları yüklenirken bir hata oluştu: " + ex.Message, "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            return settings;
        }
        private void MainForm_Load(object sender, EventArgs e)
        {

        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            toolStripStatusLabel1.Text = DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss"); // Güncel tarih ve saat bilgisini toolStripStatusLabel1 içine yazdır

        }

        private void editToolStripMenuItemEdit_Click(object sender, EventArgs e)
        {
            try
            {
                string xmlFilePath = "smtpSettings.xml"; // XML dosyasının yolu
                Process.Start("notepad.exe", xmlFilePath); // Notepad ile dosyayı aç
            }
            catch (Exception ex)
            {
                MessageBox.Show("Dosya açılırken bir hata oluştu: " + ex.Message, "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void testToolStripMenuItemTESTConnect_Click(object sender, EventArgs e)
        {
            SmtpSettings smtpSettings = LoadSmtpSettings("smtpSettings.xml");

            if (smtpSettings == null)
            {
                MessageBox.Show("SMTP ayarları yüklenemedi.", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            try
            {
                MailMessage mail = new MailMessage(smtpSettings.FromAddress, smtpSettings.ToAddress);
                mail.Subject = "SMTP Test";
                mail.Body = "Bu bir test e-postasıdır.";

                SmtpClient smtpClient = new SmtpClient(smtpSettings.Host, smtpSettings.Port);
                smtpClient.UseDefaultCredentials = false;
                smtpClient.Credentials = new NetworkCredential(smtpSettings.Username, smtpSettings.Password);
                smtpClient.EnableSsl = true; // SSL/TLS şifreleme kullanılacaksa true olarak ayarlanmalı

                smtpClient.Send(mail);

                MessageBox.Show("Test mail başarıyla gönderildi.", "Başarılı", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Test mail gönderilirken bir hata oluştu: " + ex.Message, "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}