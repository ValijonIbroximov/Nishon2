using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using Microsoft.VisualBasic;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MultiFaceRec
{
    public partial class FrmPrincipal : Form
    {
        /// <summary>
        /// //////////////////////////////////////////////////////////////////////
        /// // --- O'zgaruvchilar ---
        /// //////////////////////////////////////////////////////////////////////
        /// </summary>

        Image<Bgr, Byte> currentFrame;            // Kamera freymi (rangli)
        Capture grabber;                          // Kamera capture obyekt
        HaarCascade face;                         // Yuzni aniqlash uchun model
        MCvFont font = new MCvFont(FONT.CV_FONT_HERSHEY_TRIPLEX, 0.5d, 0.5d); // Matn yozish fonti

        Image<Gray, byte> result, TrainedFace = null; // Yuzlar
        Image<Gray, byte> gray = null;

        List<Image<Gray, byte>> trainingImages = new List<Image<Gray, byte>>(); // Treningdagi yuzlar
        List<string> labels = new List<string>();   // Yuzlarga tegishli label (ism yoki ID)
        int ContTrain, NumLabels;
        string name;                                // Taniqlangan foydalanuvchi ismi


        string id = string.Empty;
        string familiya = string.Empty;
        string ism = string.Empty;
        string sharif = string.Empty;
        string unvoni = string.Empty;
        string bolinma = string.Empty;
        string haqida = string.Empty;

        string s1 = string.Empty;
        string s2 = string.Empty;
        string s3 = string.Empty;

        string n1 = string.Empty;
        string n2 = string.Empty;
        string n3 = string.Empty;

        string ball = string.Empty;
        string baho = string.Empty;

        // yangi maydonlar
        string sball = string.Empty;              // string qilib saqlaymiz (keyin int.TryParse qilsangiz bo‘ladi)
        string nball = string.Empty;              // string qilib saqlaymiz (keyin int.TryParse qilsangiz bo‘ladi)
        string songgiotishsanasi = string.Empty;  // foydalanuvchi "dd.MM.yyyy" formatida kiritadi
        string otishdavomiyligi = string.Empty;   // foydalanuvchi "hh:mm:ss" formatida kiritadi

        Image image = null;



        string connectionString = string.Empty;


        private int retryCount = 0;
        private Timer faceTimer;

        // Serial port ulanish tezligi
        private const int DefaultBaudRate = 9600;

        // Port tanlanmaganligi haqidagi xabar
        private const string NoPortSelectedText = "Port tanlanmagan yoki mavjud emas.";

        // Portlarni tekshirish oralig‘i (millisekundlarda)
        private const int PortCheckIntervalMs = 2000;

        // SerialPort obyekti
        private SerialPort serialPort;

        // Port kuzatish uchun taymer
        private Timer portCheckTimer;

        // Flag: hozir portlar ro‘yxati yangilanmoqda
        private bool isUpdatingPorts = false;

        // Oxirgi tanlangan port nomi
        private string lastSelectedPort = string.Empty;

        // Oldingi portlar ro‘yxati
        private string[] lastKnownPorts = Array.Empty<string>();




        /// <summary>
        /// //////////////////////////////////////////////////////////////////////
        // --- Form ---
        /// //////////////////////////////////////////////////////////////////////
        /// </summary>

        private void FrmPrincipal_Load(object sender, EventArgs e)
        {
            // Rasmlarni yumaloq shaklga keltirish uchun (agar kerak bo‘lsa)
            CirclePic(g6); CirclePic(g7); CirclePic(g8); CirclePic(g9); CirclePic(g10);
            CirclePic(r6); CirclePic(r7); CirclePic(r8); CirclePic(r9); CirclePic(r10);
            this.WindowState = FormWindowState.Maximized;
            // Asosiy.cs dan connection stringni olish
            connectionString = loadConnectionString();

            // Ma'lumotlar bazasiga ulanishni tekshirish
            if (!checkDatabaseConnection(connectionString))
            {
                MessageBox.Show("Ma'lumotlar bazasi joylashuvi aniqlanmadi.\n" +
                        "Ma'lumotlar bazasini qayta bog'lang yoki yarating!",
                        "Xatolik", MessageBoxButtons.OK, MessageBoxIcon.Error);
                malumotlarBazasiBilanBoglashToolStripMenuItem_Click(sender, e);
            }
            else
            {
                // Bog'lanish muvaffaqiyatli, users jadvalini tekshirish
                if (!checkTableExistence(connectionString))
                {
                    ShowNotification("Ma'lumotlar bazasida 'users' jadvali mavjud emas. Jadvalni yaratilmoqda...");
                    createTable(connectionString);
                }
            }
            connectionString = loadConnectionString();
            LoadTrainingDataFromDB();
        }

        public FrmPrincipal()
        {
            InitializeComponent();
            InitializePortComboBox();
            StartPortWatcher();

            ShowNotification(NoPortSelectedText); // lblStatus → Label

            // HaarCascade yuklash
            try
            {
                face = new HaarCascade("haarcascade_frontalface_default.xml");
            }
            catch
            {
                ShowNotification("Haarcascade fayli topilmadi! XML faylni tekshiring.");
                return;
            }

            // 🔹 Avval connection stringni yuklaymiz
            connectionString = loadConnectionString();

            if (string.IsNullOrEmpty(connectionString))
            {
                ShowNotification("Ulanish satri topilmadi. Ma’lumotlar bazasini ulang.");
                return;
            }

            // 🔹 Endi DB dan training ma’lumotlarini yuklaymiz
            try
            {
                LoadTrainingDataFromDB();
            }
            catch (Exception ex)
            {
                ShowNotification("Database bo'sh yoki ulanishda xato: " + ex.Message);
            }
        }



        // --- Qidirish tugmasi ---
        private void btnSearchPerson_Click(object sender, EventArgs e)
        {
            try
            {
                btnClear_Click(null, EventArgs.Empty);
                if (grabber != null) grabber.Dispose(); // Avvalgi connectionni yopamiz
                picFace.Visible = false; // Oldingi yuzni yashiramiz
                grabber = new Capture();                // Kamera ishga tushiriladi
                grabber.QueryFrame();

                Application.Idle += new EventHandler(FrameGrabber); // Idle event qo‘shiladi
            }
            catch (Exception ex)
            {
                ShowNotification("Kameraga ulanishda xato: " + ex.Message);
            }
        }


        // --- Yuz qo‘shish tugmasi ---
        private void btnAddPerson_Click(object sender, EventArgs e)
        {
            try
            {
                if (grabber == null)
                {
                    ShowNotification("Kamera ishga tushmagan!");
                    return;
                }

                if (!picFace.Visible || picFace.Image == null)
                {
                    ShowNotification("Avval yuzni aniqlang (Take bosib)!");
                    return;
                }

                // 🔹 Hozircha hisoblash funksiyasi (keyinchalik to‘liq qilamiz)
                string sball = "0";
                string nball = "0";
                string ball = "0";
                string baho = "0";
                DateTime songgiotishsanasi = DateTime.Now.Date;
                TimeSpan otishdavomiyligi = TimeSpan.Zero;

                // Rasmni byte[] ga aylantirish
                byte[] imageBytes = ImageToByteArray(picFace.Image);

                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();

                    // INSERT so‘rovi (id ustuni kiritilmaydi)
                    string insertQuery = @"
                INSERT INTO users
                (familiya, ism, sharif, unvoni, bolinma, haqida,
                 s1, s2, s3, n1, n2, n3, ball, baho, sball, nball,
                 songgiotishsanasi, otishdavomiyligi, image)
                OUTPUT INSERTED.id
                VALUES
                (@familiya, @ism, @sharif, @unvoni, @bolinma, @haqida,
                 @s1, @s2, @s3, @n1, @n2, @n3, @ball, @baho, @sball, @nball,
                 @songgiotishsanasi, @otishdavomiyligi, @image)";

                    int newId;
                    using (SqlCommand cmd = new SqlCommand(insertQuery, connection))
                    {
                        cmd.Parameters.AddWithValue("@familiya", txtfamiliya.Text.Trim());
                        cmd.Parameters.AddWithValue("@ism", txtism.Text.Trim());
                        cmd.Parameters.AddWithValue("@sharif", txtsharif.Text.Trim());
                        cmd.Parameters.AddWithValue("@unvoni", txtunvoni.Text.Trim());
                        cmd.Parameters.AddWithValue("@bolinma", txtbolinma.Text.Trim());
                        cmd.Parameters.AddWithValue("@haqida", txthaqida.Text.Trim());
                        cmd.Parameters.AddWithValue("@s1", txts1.Text.Trim());
                        cmd.Parameters.AddWithValue("@s2", txts2.Text.Trim());
                        cmd.Parameters.AddWithValue("@s3", txts3.Text.Trim());
                        cmd.Parameters.AddWithValue("@n1", txtn1.Text.Trim());
                        cmd.Parameters.AddWithValue("@n2", txtn2.Text.Trim());
                        cmd.Parameters.AddWithValue("@n3", txtn3.Text.Trim());
                        cmd.Parameters.AddWithValue("@ball", ball);
                        cmd.Parameters.AddWithValue("@baho", baho);
                        cmd.Parameters.AddWithValue("@sball", sball);
                        cmd.Parameters.AddWithValue("@nball", nball);
                        cmd.Parameters.AddWithValue("@songgiotishsanasi", songgiotishsanasi);
                        cmd.Parameters.AddWithValue("@otishdavomiyligi", otishdavomiyligi);
                        cmd.Parameters.AddWithValue("@image", imageBytes);

                        newId = Convert.ToInt32(cmd.ExecuteScalar());
                    }

                    // 🔹 Yangi ID ni txtid ga chiqaramiz
                    txtid.Text = newId.ToString();

                    // 🔹 Qo‘shilgan ma’lumotlarni o‘qib formaga chiqaramiz
                    string selectQuery = "SELECT * FROM users WHERE id=@id";
                    using (SqlCommand cmd = new SqlCommand(selectQuery, connection))
                    {
                        cmd.Parameters.AddWithValue("@id", newId);
                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                txtfamiliya.Text = reader["familiya"].ToString();
                                txtism.Text = reader["ism"].ToString();
                                txtsharif.Text = reader["sharif"].ToString();
                                txtunvoni.Text = reader["unvoni"].ToString();
                                txtbolinma.Text = reader["bolinma"].ToString();
                                txthaqida.Text = reader["haqida"].ToString();
                                txts1.Text = reader["s1"].ToString();
                                txts2.Text = reader["s2"].ToString();
                                txts3.Text = reader["s3"].ToString();
                                txtn1.Text = reader["n1"].ToString();
                                txtn2.Text = reader["n2"].ToString();
                                txtn3.Text = reader["n3"].ToString();
                                txtball.Text = reader["ball"].ToString();
                                txtbaho.Text = reader["baho"].ToString();
                                txtsball.Text = reader["sball"].ToString();
                                txtnball.Text = reader["nball"].ToString();
                                txtsonggiotishsanasi.Text = Convert.ToDateTime(reader["songgiotishsanasi"]).ToString("dd.MM.yyyy");
                                txtotishdavomiyligi.Text = reader["otishdavomiyligi"].ToString();

                                if (!(reader["image"] is DBNull))
                                {
                                    byte[] imgBytes = (byte[])reader["image"];
                                    picFace.Image = ByteArrayToImage(imgBytes);
                                }
                            }
                        }
                    }
                }

                ShowNotification("Yangi foydalanuvchi bazaga qo‘shildi!");
            }
            catch (Exception ex)
            {
                ShowNotification("Xato: " + ex.Message);
            }
        }




        private void malumotlarBazasiBilanBoglashToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenFileDialog openFileDialog1 = new OpenFileDialog();
            openFileDialog1.Filter = "MDF files (*.mdf)|*.mdf|All files (*.*)|*.*";
            openFileDialog1.Title = "Ma'lumotlar bazasini tanlang";

            if (openFileDialog1.ShowDialog() == DialogResult.OK)
            {
                string databaseFileName = openFileDialog1.FileName;

                // Tanlangan fayl mavjudligini tekshirish
                if (System.IO.File.Exists(databaseFileName))
                {
                    // Ma'lumotlar bazasiga ulanish
                    connectToDatabase(databaseFileName);
                    connectionString = databaseFileName;
                }
                else
                {
                    // Fayl mavjud emas, yangi ma'lumotlar bazasini yaratish
                    DialogResult result = MessageBox.Show("Tanlangan ma'lumotlar bazasi fayli topilmadi. Yangi ma'lumotlar bazasini yaratishni xohlaysizmi?", "Ma'lumotlar bazasi mavjud emas", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

                    if (result == DialogResult.Yes)
                    {
                        // Trigger creation of a new database
                        yangiMalumotlarBazasiYaratishToolStripMenuItem_Click(sender, e);
                    }
                }
            }
        }


        private void yangiMalumotlarBazasiYaratishToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // Papka joylashuvini tanlash
            using (FolderBrowserDialog folderBrowser = new FolderBrowserDialog())
            {
                DialogResult result = folderBrowser.ShowDialog();
                if (result == DialogResult.OK && !string.IsNullOrWhiteSpace(folderBrowser.SelectedPath))
                {
                    string folderPath = folderBrowser.SelectedPath;

                    // Ma'lumotlar bazasi nomini so'rash
                    string databaseName = Interaction.InputBox("Ma'lumotlar bazasi nomini kiriting:", "Ma'lumotlar bazasi nomi", "NishonData");
                    if (string.IsNullOrWhiteSpace(databaseName))
                        return; // Foydalanuvchi nom kiritmagan

                    // Ma'lumotlar bazasini yaratish
                    string databaseFileName = Path.Combine(folderPath, databaseName + ".mdf");
                    createDatabase(databaseFileName);
                }
            }
        }
        private void Malumotlarbazasijoylashuvinikorish_Click(object sender, EventArgs e)
        {
            ShowNotification("Ma'lumotlar bazasi joylashuvi: " + connectionString);
        }
        private void btnDelete_Click(object sender, EventArgs e)
        {
            string currentId = txtid.Text.Trim();
            LoadUserDataById(currentId, out bool b); // ID bo‘yicha ma'lumotlarni yuklash

            if (!string.IsNullOrEmpty(currentId))
            {
                // Ma'lumotlar bazasidan txtid dagi qiymatga mos yozuvni o‘chiradi
                DeleteData(currentId);
            }
            else
            {
                ShowNotification("O‘chirish uchun ID kiritilmagan!");
            }
        }


        private void btnAdd_Click(object sender, EventArgs e)
        {
            // TextBox qiymatlarini global o‘zgaruvchilarga yuklash
            familiya = txtfamiliya.Text.Trim();
            ism = txtism.Text.Trim();
            sharif = txtsharif.Text.Trim();
            unvoni = txtunvoni.Text.Trim();
            bolinma = txtbolinma.Text.Trim();
            haqida = txthaqida.Text.Trim();
            s1 = txts1.Text.Trim();
            s2 = txts2.Text.Trim();
            s3 = txts3.Text.Trim();
            n1 = txtn1.Text.Trim();
            n2 = txtn2.Text.Trim();
            n3 = txtn3.Text.Trim();
            ball = txtball.Text.Trim();
            baho = txtbaho.Text.Trim();
            image = picFace.Image;

            // Tekshiruv
            if (string.IsNullOrWhiteSpace(familiya) ||
                string.IsNullOrWhiteSpace(ism) ||
                image == null)
            {
                ShowNotification("Familiya, ism va rasm kiritilishi shart!");
                return;
            }

            // Rasm byte[] ga o‘tkazish
            byte[] imageBytes = ImageToByteArray(image);

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                try
                {
                    connection.Open();

                    string insertQuery = @"
                INSERT INTO users (familiya, ism, sharif, unvoni, bolinma, haqida, s1, s2, s3, n1, n2, n3, ball, baho, image)
                VALUES (@familiya, @ism, @sharif, @unvoni, @bolinma, @haqida, @s1, @s2, @s3, @n1, @n2, @n3, @ball, @baho, @image);
                SELECT SCOPE_IDENTITY();";  // qo‘shilgan ID ni olish

                    SqlCommand cmd = new SqlCommand(insertQuery, connection);
                    cmd.Parameters.AddWithValue("@familiya", familiya);
                    cmd.Parameters.AddWithValue("@ism", ism);
                    cmd.Parameters.AddWithValue("@sharif", sharif);
                    cmd.Parameters.AddWithValue("@unvoni", unvoni);
                    cmd.Parameters.AddWithValue("@bolinma", bolinma);
                    cmd.Parameters.AddWithValue("@haqida", haqida);
                    cmd.Parameters.AddWithValue("@s1", s1);
                    cmd.Parameters.AddWithValue("@s2", s2);
                    cmd.Parameters.AddWithValue("@s3", s3);
                    cmd.Parameters.AddWithValue("@n1", n1);
                    cmd.Parameters.AddWithValue("@n2", n2);
                    cmd.Parameters.AddWithValue("@n3", n3);
                    cmd.Parameters.AddWithValue("@ball", ball);
                    cmd.Parameters.AddWithValue("@baho", baho);
                    cmd.Parameters.AddWithValue("@image", imageBytes);

                    // Ma'lumotlarni qo‘shib ID ni olish
                    int newId = Convert.ToInt32(cmd.ExecuteScalar());

                    ShowNotification("Yangi foydalanuvchi qo‘shildi. ID = " + newId);
                }
                catch (Exception ex)
                {
                    ShowNotification("Xatolik: " + ex.Message);
                }
            }
        }
        private void btnUpgrade_Click(object sender, EventArgs e)
        {
            try
            {
                id = txtid.Text.Trim();
                if (!string.IsNullOrEmpty(id))
                {
                    familiya = txtfamiliya.Text.Trim();
                    ism = txtism.Text.Trim();
                    sharif = txtsharif.Text.Trim();
                    unvoni = txtunvoni.Text.Trim();
                    bolinma = txtbolinma.Text.Trim();
                    haqida = txthaqida.Text.Trim();
                    s1 = txts1.Text.Trim();
                    s2 = txts2.Text.Trim();
                    s3 = txts3.Text.Trim();
                    n1 = txtn1.Text.Trim();
                    n2 = txtn2.Text.Trim();
                    n3 = txtn3.Text.Trim();
                    ball = txtball.Text.Trim();
                    baho = txtbaho.Text.Trim();
                    sball = txtsball.Text.Trim();
                    nball = txtnball.Text.Trim();
                    songgiotishsanasi = txtsonggiotishsanasi.Text.Trim();
                    otishdavomiyligi = txtotishdavomiyligi.Text.Trim();
                    image = picFace.Image;

                    if (string.IsNullOrWhiteSpace(familiya) ||
                        string.IsNullOrWhiteSpace(ism) ||
                        image == null)
                    {
                        ShowNotification("Familiya, ism va rasm kiritilishi shart!");
                        return;
                    }

                    UpdateData(id);
                }
                else
                {
                    ShowNotification("Yangilash uchun avval foydalanuvchi tanlang.");
                }
            }
            catch (Exception ex)
            {
                ShowNotification("Xatolik: " + ex.Message);
            }
        }





        private void btnClear_Click(object sender, EventArgs e)
        {
            txtid.Clear();
            txtfamiliya.Clear();
            txtism.Clear();
            txtsharif.Clear();
            txtunvoni.Clear();
            txtbolinma.Clear();
            txthaqida.Clear();
            txts1.Clear();
            txts2.Clear();
            txts3.Clear();
            txtn1.Clear();
            txtn2.Clear();
            txtn3.Clear();
            txtball.Clear();
            txtbaho.Clear();
            txtsball.Clear();
            txtnball.Clear();
            txtsonggiotishsanasi.Clear();
            txtotishdavomiyligi.Clear();
            id = string.Empty;
            familiya = string.Empty;
            ism = string.Empty;
            sharif = string.Empty;
            unvoni = string.Empty;
            bolinma = string.Empty;
            haqida = string.Empty;
            s1 = string.Empty;
            s2 = string.Empty;
            s3 = string.Empty;
            n1 = string.Empty;
            n2 = string.Empty;
            n3 = string.Empty;
            ball = string.Empty;
            baho = string.Empty;
            sball = string.Empty;
            nball = string.Empty;
            songgiotishsanasi = string.Empty;
            otishdavomiyligi = string.Empty;
            image = null;


            // Rasmni tozalash
            picFace.Image = null;

            // Fokusni rasmga berish (qulaylik uchun)
            picFace.Focus();
            //picFace.Visible = false;
        }




        /// <summary>
        /// //////////////////////////////////////////////////////////////////////
            // --- Funksiyalar ---
            /// //////////////////////////////////////////////////////////////////////
            /// </summary>

        private bool checkDatabaseConnection(string connectionString)
        {
            bool isConnected = false;

            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    isConnected = true;
                    //MessageBox.Show("Ma'lumotlar bazasiga muvaffaqiyatli ulanildi.");
                }
            }
            catch (Exception ex)
            {
                ShowNotification("Xatolik: " + ex.Message);
            }

            return isConnected;
        }

        private bool checkTableExistence(string connectionString)
        {
            bool tableExists = false;
            string tableName = "users";

            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();

                    // Jadval mavjudligini tekshirish
                    string query = $"SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = '{tableName}'";
                    SqlCommand command = new SqlCommand(query, connection);
                    int count = (int)command.ExecuteScalar();

                    tableExists = (count > 0);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Xatolik: " + ex.Message, "Xatolik", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            return tableExists;
        }

        private void createTable(string connectionString1)
        {
            using (SqlConnection connection = new SqlConnection(connectionString1))
            {
                try
                {
                    connection.Open();

                    // Jadval mavjudligini tekshirish
                    string checkQuery = @"SELECT COUNT(*) 
                                  FROM INFORMATION_SCHEMA.TABLES 
                                  WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = 'users'";
                    int exists;
                    using (SqlCommand checkCmd = new SqlCommand(checkQuery, connection))
                    {
                        exists = (int)checkCmd.ExecuteScalar();
                    }

                    if (exists == 0)
                    {
                        string createTableQuery = @"
CREATE TABLE [dbo].[users]
(
    [id] INT IDENTITY(1,1) PRIMARY KEY,   -- ✅ Avtomatik ID
    [familiya] NVARCHAR(100) NULL,
    [ism] NVARCHAR(50) NULL,
    [sharif] NVARCHAR(50) NULL,
    [unvoni] NVARCHAR(50) NULL,
    [bolinma] NVARCHAR(100) NULL,
    [haqida] NVARCHAR(MAX) NULL,
    [s1] NVARCHAR(50) NULL,
    [s2] NVARCHAR(50) NULL,
    [s3] NVARCHAR(50) NULL,
    [n1] NVARCHAR(50) NULL,
    [n2] NVARCHAR(50) NULL,
    [n3] NVARCHAR(50) NULL,
    [ball] NVARCHAR(50) NULL,
    [baho] NVARCHAR(50) NULL,
    [sball] NVARCHAR(50) NULL,
    [nball] NVARCHAR(50) NULL,
    [songgiotishsanasi] DATE NULL,
    [otishdavomiyligi] TIME NULL,
    [image] VARBINARY(MAX) NULL
)";


                        using (SqlCommand command = new SqlCommand(createTableQuery, connection))
                        {
                            command.ExecuteNonQuery();
                        }
                        ShowNotification("users jadvali muvaffaqiyatli yaratildi.");
                    }
                    else
                    {
                        // Agar jadval mavjud bo‘lsa, yangi ustunlarni qo‘shish
                        string[] newColumns = {
                    "sball NVARCHAR(50)",
                    "nball NVARCHAR(50)",
                    "songgiotishsanasi DATE",
                    "otishdavomiyligi TIME"
                };

                        foreach (var col in newColumns)
                        {
                            string alterQuery = $@"
                    IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
                                   WHERE TABLE_NAME = 'users' AND COLUMN_NAME = '{col.Split(' ')[0]}')
                    BEGIN
                        ALTER TABLE [dbo].[users] ADD {col}
                    END";

                            using (SqlCommand alterCmd = new SqlCommand(alterQuery, connection))
                            {
                                alterCmd.ExecuteNonQuery();
                            }
                        }

                        ShowNotification("users jadvaliga yangi ustunlar qo‘shildi (agar mavjud bo‘lmasa).");
                    }
                }
                catch (Exception ex)
                {
                    ShowNotification("Xatolik: " + ex.Message);
                }
            }
        }


        // Image obyektini byte[] massiviga aylantiruvchi metod
        private byte[] ImageToByteArray(Image image)
        {
            using (MemoryStream memoryStream = new MemoryStream())
            {
                image.Save(memoryStream, System.Drawing.Imaging.ImageFormat.Png);
                return memoryStream.ToArray();
            }
        }

        private Image ByteArrayToImage(byte[] byteArray)
        {
            using (MemoryStream memoryStream = new MemoryStream(byteArray))
            {
                Image image = Image.FromStream(memoryStream);
                return image;
            }
        }

        private void faqatraqam(object sender, KeyPressEventArgs e)
        {
            // Faqat raqamlarni qabul qilish
            if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar))
            {
                e.Handled = true; // Hodisa bajarilmaydi
            }
        }

        // PictureBox ni yumaloq shaklga keltirish
        public void CirclePic(PictureBox pb)
        {
            GraphicsPath gp = new GraphicsPath();
            gp.AddEllipse(0, 0, pb.Width - 1, pb.Height - 1);
            pb.Region = new Region(gp);
            pb.SizeMode = PictureBoxSizeMode.StretchImage;
        }

        // --- Konstruktor ---

        private void connectToDatabase(string databaseFileName)
        {
            string connectionString = $@"Data Source=(LocalDB)\MSSQLLocalDB;AttachDbFilename={databaseFileName};Integrated Security=True;Connect Timeout=30";

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                try
                {
                    connection.Open();
                    saveConnectionString(connectionString);
                    //MessageBox.Show("Ma'lumotlar bazasiga muvaffaqiyatli ulanildi.");
                }
                catch (Exception ex)
                {
                    ShowNotification("Xatolik: " + ex.Message);
                }
            }
        }

        private void createDatabase(string databaseFileName)
        {
            string connectionString = $@"Data Source=(LocalDB)\MSSQLLocalDB;Integrated Security=True;";
            string databaseName = System.IO.Path.GetFileNameWithoutExtension(databaseFileName);
            string connectionString1 = string.Empty;

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                try
                {
                    connection.Open();

                    // CREATE DATABASE so'rovi
                    string createDatabaseQuery = $"CREATE DATABASE {databaseName} ON PRIMARY (NAME={databaseName}, FILENAME='{databaseFileName}')";
                    SqlCommand command = new SqlCommand(createDatabaseQuery, connection);
                    command.ExecuteNonQuery();

                    //MessageBox.Show("Ma'lumotlar bazasi muvaffaqiyatli yaratildi.");

                    // Ma'lumotlar bazasi yaratildi, endi bog'lanish stringini saqlash
                    connectionString1 = $"Data Source=(LocalDB)\\MSSQLLocalDB;Integrated Security=True;Initial Catalog={databaseName}";

                    // saveConnectionString funksiyasiga connectionString1 ni yuborish
                    saveConnectionString(connectionString1);

                    createTable(connectionString1);
                }
                catch (Exception ex)
                {
                    ShowNotification("Xatolik: " + ex.Message);
                }
            }
        }

        private void saveConnectionString(string connectionString)
        {
            string fileName = "connectstring.txt";

            try
            {
                // Faylni yaratish yoki ochish
                FileStream fs = new FileStream(fileName, FileMode.OpenOrCreate, FileAccess.Write);
                StreamWriter sw = new StreamWriter(fs);

                // Faylga connectionString yozish
                sw.WriteLine(connectionString);

                // Resurslarni tozalash
                sw.Close();
                fs.Close();

                //MessageBox.Show("ConnectionString faylga saqlandi: " + fileName);
            }
            catch (Exception ex)
            {
                ShowNotification("Xatolik yuz berdi: " + ex.Message);
            }
        }

        private string loadConnectionString()
        {
            string fileName = "connectstring.txt";
            string connectionString = "";

            try
            {
                // Faylni tekshirish
                if (File.Exists(fileName))
                {
                    // Faylni ochish
                    FileStream fs = new FileStream(fileName, FileMode.Open, FileAccess.Read);
                    StreamReader sr = new StreamReader(fs);

                    // Fayldan ConnectionString o'qish
                    connectionString = sr.ReadLine();

                    // Resurslarni tozalash
                    sr.Close();
                    fs.Close();

                    //MessageBox.Show("ConnectionString fayldan yuklandi: " + fileName);
                }
                else
                {
                    ShowNotification("Fayl mavjud emas: " + fileName);
                }
            }
            catch (Exception ex)
            {
                ShowNotification("Xatolik yuz berdi: " + ex.Message);
            }

            return connectionString;
        }



        private void DeleteData(string id)
        {
            // Avval foydalanuvchidan tasdiq olish
            DialogResult confirm = MessageBox.Show(
                $"Siz haqiqatdan ham {id} raqamli foydalanuvchini o‘chirmoqchimisiz?",
                "O‘chirishni tasdiqlash",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (confirm != DialogResult.Yes)
            {
                // Agar Yes bosilmasa, funksiya tugaydi
                return;
            }

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                try
                {
                    connection.Open();

                    string deleteQuery = "DELETE FROM users WHERE Id = @Id";
                    SqlCommand command = new SqlCommand(deleteQuery, connection);
                    command.Parameters.AddWithValue("@Id", id);

                    int rowsAffected = command.ExecuteNonQuery();

                    if (rowsAffected > 0)
                    {
                        ShowNotification("Ma'lumot muvaffaqiyatli o‘chirildi.");
                        // O‘chirildi – formadagi maydonlarni tozalash foydali
                        btnClear_Click(null, EventArgs.Empty);
                    }
                    else
                    {
                        ShowNotification("Ma'lumot topilmadi yoki o‘chirilmadi.");
                    }
                }
                catch (Exception ex)
                {
                    ShowNotification("Xatolik: " + ex.Message);
                }
            }
        }




        private void UpdateData(string id)
        {
            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();

                    string updateQuery = @"
UPDATE users 
SET familiya = @familiya,
    ism = @ism,
    sharif = @sharif,
    unvoni = @unvoni,
    bolinma = @bolinma,
    haqida = @haqida,
    s1 = @s1,
    s2 = @s2,
    s3 = @s3,
    n1 = @n1,
    n2 = @n2,
    n3 = @n3,
    ball = @ball,
    baho = @baho,
    sball = @sball,
    nball = @nball,
    songgiotishsanasi = @songgiotishsanasi,
    otishdavomiyligi = @otishdavomiyligi,
    image = @image
WHERE id = @id;
";

                    using (SqlCommand cmd = new SqlCommand(updateQuery, connection))
                    {
                        // Matn maydonlari (NULL bo'lsa DB NULL yuboriladi)
                        cmd.Parameters.Add("@familiya", SqlDbType.NVarChar, 100).Value = string.IsNullOrWhiteSpace(familiya) ? (object)DBNull.Value : familiya;
                        cmd.Parameters.Add("@ism", SqlDbType.NVarChar, 50).Value = string.IsNullOrWhiteSpace(ism) ? (object)DBNull.Value : ism;
                        cmd.Parameters.Add("@sharif", SqlDbType.NVarChar, 50).Value = string.IsNullOrWhiteSpace(sharif) ? (object)DBNull.Value : sharif;
                        cmd.Parameters.Add("@unvoni", SqlDbType.NVarChar, 50).Value = string.IsNullOrWhiteSpace(unvoni) ? (object)DBNull.Value : unvoni;
                        cmd.Parameters.Add("@bolinma", SqlDbType.NVarChar, 100).Value = string.IsNullOrWhiteSpace(bolinma) ? (object)DBNull.Value : bolinma;
                        cmd.Parameters.Add("@haqida", SqlDbType.NVarChar, -1).Value = string.IsNullOrWhiteSpace(haqida) ? (object)DBNull.Value : (object)haqida;
                        cmd.Parameters.Add("@s1", SqlDbType.NVarChar, 50).Value = string.IsNullOrWhiteSpace(s1) ? (object)DBNull.Value : s1;
                        cmd.Parameters.Add("@s2", SqlDbType.NVarChar, 50).Value = string.IsNullOrWhiteSpace(s2) ? (object)DBNull.Value : s2;
                        cmd.Parameters.Add("@s3", SqlDbType.NVarChar, 50).Value = string.IsNullOrWhiteSpace(s3) ? (object)DBNull.Value : s3;
                        cmd.Parameters.Add("@n1", SqlDbType.NVarChar, 50).Value = string.IsNullOrWhiteSpace(n1) ? (object)DBNull.Value : n1;
                        cmd.Parameters.Add("@n2", SqlDbType.NVarChar, 50).Value = string.IsNullOrWhiteSpace(n2) ? (object)DBNull.Value : n2;
                        cmd.Parameters.Add("@n3", SqlDbType.NVarChar, 50).Value = string.IsNullOrWhiteSpace(n3) ? (object)DBNull.Value : n3;
                        cmd.Parameters.Add("@ball", SqlDbType.NVarChar, 50).Value = string.IsNullOrWhiteSpace(ball) ? (object)DBNull.Value : ball;
                        cmd.Parameters.Add("@baho", SqlDbType.NVarChar, 50).Value = string.IsNullOrWhiteSpace(baho) ? (object)DBNull.Value : baho;

                        // yangi maydonlar
                        cmd.Parameters.Add("@sball", SqlDbType.NVarChar, 50).Value = string.IsNullOrWhiteSpace(sball) ? (object)DBNull.Value : sball;
                        cmd.Parameters.Add("@nball", SqlDbType.NVarChar, 50).Value = string.IsNullOrWhiteSpace(nball) ? (object)DBNull.Value : nball;

                        // Sana (dd.MM.yyyy) -> DATE
                        if (!string.IsNullOrWhiteSpace(songgiotishsanasi)
                            && DateTime.TryParseExact(songgiotishsanasi, "dd.MM.yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsedDate))
                        {
                            cmd.Parameters.Add("@songgiotishsanasi", SqlDbType.Date).Value = parsedDate.Date;
                        }
                        else
                        {
                            cmd.Parameters.Add("@songgiotishsanasi", SqlDbType.Date).Value = DBNull.Value;
                        }

                        // Vaqt (hh:mm:ss) -> TIME
                        if (!string.IsNullOrWhiteSpace(otishdavomiyligi)
                            && TimeSpan.TryParse(otishdavomiyligi, out TimeSpan parsedTime))
                        {
                            cmd.Parameters.Add("@otishdavomiyligi", SqlDbType.Time).Value = parsedTime;
                        }
                        else
                        {
                            cmd.Parameters.Add("@otishdavomiyligi", SqlDbType.Time).Value = DBNull.Value;
                        }

                        // Image -> VARBINARY(MAX)
                        if (image != null)
                        {
                            byte[] imageBytes = ImageToByteArray(image);
                            cmd.Parameters.Add("@image", SqlDbType.VarBinary, -1).Value = imageBytes;
                        }
                        else
                        {
                            cmd.Parameters.Add("@image", SqlDbType.VarBinary, -1).Value = DBNull.Value;
                        }

                        // **MUHIM**: id parametri — shu yerda qo'shiladi (error shu sabab edi)
                        cmd.Parameters.Add("@id", SqlDbType.NVarChar, 50).Value = id;

                        int rowsAffected = cmd.ExecuteNonQuery();

                        if (rowsAffected > 0)
                            ShowNotification("Foydalanuvchi ma'lumotlari muvaffaqiyatli yangilandi.");
                        else
                            ShowNotification("Yangilashda xatolik: foydalanuvchi topilmadi.");

                    } // using cmd
                } // using connection
            }
            catch (Exception ex)
            {
                ShowNotification("Xatolik: " + ex.Message);
            }
        }


        private void FrmPrincipal_FormClosing(object sender, FormClosingEventArgs e)
        {
            ClosePort();
            if (portCheckTimer != null)
            {
                portCheckTimer.Stop();
                portCheckTimer.Dispose();
                portCheckTimer = null;
            }
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                try
                {
                    connection.Close();
                    saveConnectionString(connectionString);
                    //MessageBox.Show("Ma'lumotlar bazasiga muvaffaqiyatli ulanildi.");
                }
                catch (Exception ex)
                {
                    ShowNotification("Xatolik: " + ex.Message);
                }
            }
        }


        private void btnTake_Click(object sender, EventArgs e)
        {
            try
            {
                btnTake.Enabled = false; // kutayotgan paytda bosilmasin

                if (grabber == null)
                {
                    grabber = new Capture();
                    grabber.QueryFrame();
                }

                lblMsg.Text = "Yuz qidirilmoqda...";
                Application.Idle -= TakeFaceFrame;
                Application.Idle += TakeFaceFrame;
            }
            catch (Exception ex)
            {
                lblMsg.Text = "Xatolik: " + ex.Message;
                btnTake.Enabled = true;
            }
        }

        private void TakeFaceFrame(object sender, EventArgs e)
        {
            currentFrame = grabber.QueryFrame().Resize(320, 240, INTER.CV_INTER_CUBIC);
            gray = currentFrame.Convert<Gray, Byte>();

            MCvAvgComp[][] facesDetected = gray.DetectHaarCascade(
                face,
                1.1,
                5,
                HAAR_DETECTION_TYPE.DO_CANNY_PRUNING,
                new Size(50, 50));

            if (facesDetected[0].Length > 0)
            {
                MCvAvgComp f = facesDetected[0][0];
                result = currentFrame.Copy(f.rect).Convert<Gray, byte>()
                          .Resize(100, 100, INTER.CV_INTER_CUBIC);
                Application.Idle -= FrameGrabber;
                picFace.Visible = true;
                picFace.Image = result.ToBitmap();
                imageBoxFrameGrabber.Image = currentFrame;

                ShowNotification("Yuz topildi ✅");

                Application.Idle -= TakeFaceFrame;
                btnTake.Enabled = true; // qayta yoqiladi
            }
            else
            {
                picFace.Visible = false;
                ShowNotification("Yuz aniqlanmadi, kuting...");
                imageBoxFrameGrabber.Image = currentFrame;
            }
        }


        private async void ShowNotification(string message = "")
        {
            lblMsg.Text = message;
            await Task.Delay(3000); // 2 sekund kutadi
            lblMsg.Text = "";
        }


        private void LoadTrainingDataFromDB()
        {
            trainingImages.Clear();
            labels.Clear();

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();
                string query = "SELECT id, image FROM users WHERE image IS NOT NULL";
                using (SqlCommand cmd = new SqlCommand(query, conn))
                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        string personId = reader["id"].ToString();

                        if (reader["image"] != DBNull.Value)
                        {
                            byte[] imgBytes = (byte[])reader["image"];
                            if (imgBytes.Length > 0)
                            {
                                using (MemoryStream ms = new MemoryStream(imgBytes))
                                {
                                    using (Bitmap bmp = new Bitmap(ms))
                                    {
                                        // O‘qilgan rasmni Gray formatga o‘tkazib, qayta o‘lchaymiz
                                        Image<Gray, byte> faceImg = new Image<Gray, byte>(bmp)
                                                                        .Resize(100, 100, Emgu.CV.CvEnum.INTER.CV_INTER_CUBIC);

                                        trainingImages.Add(faceImg);
                                        labels.Add(personId);
                                    }
                                }
                            }
                        }
                    }
                }
            }

            // Yangi ma’lumotlar sonini belgilash
            NumLabels = labels.Count;
            ContTrain = trainingImages.Count;
        }

        private void lblMsg_Click(object sender, EventArgs e)
        {
            Clipboard.SetText(lblMsg.Text);
        }




        // --- FrameGrabber (kamera oqimi) ---

        // 🔹 id bo‘yicha ma’lumotlarni o‘qib, formadagi maydonlarga to‘ldiradi
        private void LoadUserDataById(string userId, out bool foundInDb)
        {
            foundInDb = false; // default qiymat
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();
                string query = "SELECT * FROM users WHERE id=@id";
                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@id", userId);
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            foundInDb = true;

                            txtfamiliya.Text = reader["familiya"].ToString();
                            txtism.Text = reader["ism"].ToString();
                            txtsharif.Text = reader["sharif"].ToString();
                            txtunvoni.Text = reader["unvoni"].ToString();
                            txtbolinma.Text = reader["bolinma"].ToString();
                            txthaqida.Text = reader["haqida"].ToString();

                            txts1.Text = reader["s1"].ToString();
                            txts2.Text = reader["s2"].ToString();
                            txts3.Text = reader["s3"].ToString();
                            txtn1.Text = reader["n1"].ToString();
                            txtn2.Text = reader["n2"].ToString();
                            txtn3.Text = reader["n3"].ToString();

                            txtball.Text = reader["ball"].ToString();
                            txtbaho.Text = reader["baho"].ToString();

                            txtsball.Text = reader["sball"].ToString();
                            txtnball.Text = reader["nball"].ToString();

                            if (!(reader["songgiotishsanasi"] is DBNull))
                            {
                                DateTime sana = Convert.ToDateTime(reader["songgiotishsanasi"]);
                                txtsonggiotishsanasi.Text = sana.ToString("dd.MM.yyyy");
                            }
                            else
                            {
                                txtsonggiotishsanasi.Text = "";
                            }

                            txtotishdavomiyligi.Text = reader["otishdavomiyligi"].ToString();

                            if (!(reader["image"] is DBNull))
                            {
                                byte[] imgBytes = (byte[])reader["image"];
                                picFace.Image = ByteArrayToImage(imgBytes);
                            }
                        }
                    }
                }
            }
        }



        void FrameGrabber(object sender, EventArgs e)
        {
            try
            {
                currentFrame = grabber.QueryFrame().Resize(320, 240, INTER.CV_INTER_CUBIC);
                gray = currentFrame.Convert<Gray, Byte>();

                MCvAvgComp[][] facesDetected = gray.DetectHaarCascade(
                    face, 1.1, 10,
                    Emgu.CV.CvEnum.HAAR_DETECTION_TYPE.DO_CANNY_PRUNING,
                    new Size(50, 50));

                if (facesDetected[0].Length > 0 && trainingImages.Count > 0)
                {
                    MCvAvgComp f = facesDetected[0][0];
                    result = currentFrame.Copy(f.rect).Convert<Gray, byte>()
                              .Resize(100, 100, INTER.CV_INTER_CUBIC);

                    currentFrame.Draw(f.rect, new Bgr(Color.Green), 2);

                    // --- Tanish ---
                    MCvTermCriteria termCrit = new MCvTermCriteria(ContTrain, 0.001);
                    EigenObjectRecognizer recognizer = new EigenObjectRecognizer(
                        trainingImages.ToArray(),
                        labels.ToArray(),
                        3000,
                        ref termCrit);

                    name = recognizer.Recognize(result);

                    if (!string.IsNullOrEmpty(name))
                    {
                        txtid.Text = name;

                        bool found;
                        LoadUserDataById(name, out found);

                        // 🔴 Agar DB da mavjud bo‘lsa -> to‘xtatamiz
                        if (found)
                        {
                            Application.Idle -= FrameGrabber;
                        }
                        else
                        {
                            ShowNotification("Yuz tanildi, lekin DB da mavjud emas. Qidirilmoqda...");
                        }
                    }

                    currentFrame.Draw(name ?? "Unknown",
                        ref font, new Point(f.rect.X - 2, f.rect.Y - 2),
                        new Bgr(Color.Red));

                    imageBoxFrameGrabber.Image = currentFrame;
                    picFace.Visible = true;
                    picFace.Image = result.Bitmap;
                }
                else
                {
                    imageBoxFrameGrabber.Image = currentFrame;
                    picFace.Visible = false;
                }
            }
            catch (Exception ex)
            {
                ShowNotification("FrameGrabber xato: " + ex.Message);
            }
        }

        /// <summary>
        /// Portlar ro‘yxatini boshlang‘ich holatga keltirish
        /// </summary>
        private void InitializePortComboBox()
        {
            isUpdatingPorts = true;
            cmbPorts.Items.Clear(); // cmbPorts → ComboBox
            cmbPorts.Items.Add("Tanlang...");
            cmbPorts.SelectedIndex = 0;
            RefreshPortList();
            isUpdatingPorts = false;
        }

        /// <summary>
        /// Portlarni avtomatik kuzatish taymerini ishga tushirish
        /// </summary>
        private void StartPortWatcher()
        {
            portCheckTimer = new Timer();
            portCheckTimer.Interval = PortCheckIntervalMs;
            portCheckTimer.Tick += (s, e) =>
            {
                var currentPorts = SerialPort.GetPortNames();

                var added = currentPorts.Except(lastKnownPorts);
                foreach (var port in added)
                {
                    ShowNotification($"Yangi port qo‘shildi: {port}");
                    RefreshPortList();
                }

                var removed = lastKnownPorts.Except(currentPorts);
                foreach (var port in removed)
                {
                    ShowNotification($"Port o‘chirildi: {port}");
                    RefreshPortList();
                }

                lastKnownPorts = currentPorts;
            };
            portCheckTimer.Start();
        }

        /// <summary>
        /// Portlar ro‘yxatini yangilash
        /// </summary>
        private void RefreshPortList()
        {
            try
            {
                isUpdatingPorts = true;
                string[] ports = SerialPort.GetPortNames();

                string currentSelection = cmbPorts.SelectedItem?.ToString();

                cmbPorts.Items.Clear();
                cmbPorts.Items.Add("Tanlang...");

                foreach (string port in ports)
                {
                    cmbPorts.Items.Add(port);
                }

                if (!string.IsNullOrEmpty(lastSelectedPort) && ports.Contains(lastSelectedPort))
                    cmbPorts.SelectedItem = lastSelectedPort;
                else if (!string.IsNullOrEmpty(currentSelection) && ports.Contains(currentSelection))
                    cmbPorts.SelectedItem = currentSelection;
                else
                {
                    cmbPorts.SelectedIndex = 0;
                    ShowNotification(NoPortSelectedText);
                }
            }
            catch (Exception ex)
            {
                ShowNotification($"Portlarni yangilashda xato: {ex.Message}");
            }
            finally
            {
                isUpdatingPorts = false;
            }
        }

        /// <summary>
        /// Port tanlanganda ishga tushadi
        /// </summary>
        private void cmbPorts_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (isUpdatingPorts) return;

            if (cmbPorts.SelectedIndex == 0)
            {
                if (!string.IsNullOrEmpty(lastSelectedPort))
                {
                    ShowNotification($"{lastSelectedPort} portidan chiqildi");
                    lastSelectedPort = string.Empty;
                }
                ClosePort();
                ShowNotification(NoPortSelectedText);
                return;
            }

            if (cmbPorts.SelectedItem != null)
            {
                lastSelectedPort = cmbPorts.SelectedItem.ToString();
                OpenPort(lastSelectedPort);
            }
        }

        /// <summary>
        /// Belgilangan portga ulanish
        /// </summary>
        private void OpenPort(string portName)
        {
            ClosePort();
            try
            {
                serialPort = new SerialPort(portName, DefaultBaudRate);
                serialPort.DataReceived += SerialPort_DataReceived;
                serialPort.Open();
                ShowNotification($"{portName} portiga ulandi!");
            }
            catch (Exception ex)
            {
                ShowNotification($"Portni ochishda xato: {ex.Message}");
                cmbPorts.SelectedIndex = 0;
                ShowNotification(NoPortSelectedText);
            }
        }

        /// <summary>
        /// Portdan ma’lumot kelganda ishlaydi
        /// </summary>
        private void SerialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            try
            {
                if (serialPort == null || !serialPort.IsOpen) return;

                string data = serialPort.ReadLine().Trim();

                this.Invoke(new Action(() =>
                {
                    RedTrue(Convert.ToInt16(data.Trim()));
                    string notification = $"Portdan ma’lumot keldi: {data}";
                    ShowNotification(notification);
                    // Shu yerda DB ga yozish yoki boshqa logika qo‘shish mumkin
                }));
            }
            catch (Exception ex)
            {
                this.Invoke(new Action(() =>
                {
                    ShowNotification($"O‘qishda xato: {ex.Message}");
                    ShowNotification(NoPortSelectedText);
                }));
            }
        }

        /// <summary>
        /// Portni yopish
        /// </summary>
        private void ClosePort()
        {
            if (serialPort != null)
            {
                try
                {
                    if (serialPort.IsOpen)
                    {
                        serialPort.DataReceived -= SerialPort_DataReceived;
                        serialPort.Close();
                    }
                    serialPort.Dispose();
                }
                catch { }
                finally
                {
                    serialPort = null;
                }
            }
        }

        private async void RedTrue(int n)
        {
            switch (n)
            {
                case 5:
                    r5.Visible = true;
                    await Task.Delay(500);
                    r5.Visible = false;
                    break;
                case 6:
                    r6.Visible = true;
                    await Task.Delay(500);
                    r6.Visible = false;
                    break;
                case 7:
                    r7.Visible = true;
                    await Task.Delay(500);
                    r7.Visible = false;
                    break;
                case 8:
                    r8.Visible = true;
                    await Task.Delay(500);
                    r8.Visible = false;
                    break;
                case 9:
                    r9.Visible = true;
                    await Task.Delay(500);
                    r9.Visible = false;
                    break;
                case 10:
                    r10.Visible = true;
                    await Task.Delay(500);
                    r10.Visible = false;
                    break;
            }
        }


    }
}
