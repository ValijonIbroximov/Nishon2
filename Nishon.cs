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
using System.IO;
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


        string snomi = string.Empty;
        string snarxi = string.Empty;
        Image simage = null;
        string smavjud = string.Empty;
        string sizoh = string.Empty;
        string skategoriya = string.Empty;
        string smx = string.Empty;
        string smy = string.Empty;


        string connectionString = string.Empty;


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
                // Bog'lanish muvaffaqiyatli, ehtiyotqismlar jadvalini tekshirish
                if (!checkTableExistence(connectionString))
                {
                    MessageBox.Show("Ma'lumotlar bazasida 'ehtiyotqismlar' jadvali mavjud emas. Jadvalni yaratilmoqda...");
                    createTable(connectionString);
                }
            }
            connectionString = loadConnectionString();

            //addproduct(pnlehtiyotqismlar);
        }

        public FrmPrincipal()
        {
            InitializeComponent();

            // HaarCascade yuklash
            try
            {
                face = new HaarCascade("haarcascade_frontalface_default.xml");
            }
            catch
            {
                MessageBox.Show("Haarcascade fayli topilmadi! XML faylni tekshiring.");
                return;
            }

            // Trening ma'lumotlarini o‘qish
            try
            {
                string Labelsinfo = File.ReadAllText(Application.StartupPath + "/TrainedFaces/TrainedLabels.txt");
                string[] Labels = Labelsinfo.Split('%');
                NumLabels = Convert.ToInt16(Labels[0]);
                ContTrain = NumLabels;

                for (int tf = 1; tf < NumLabels + 1; tf++)
                {
                    string LoadFaces = "face" + tf + ".bmp";
                    trainingImages.Add(new Image<Gray, byte>(Application.StartupPath + "/TrainedFaces/" + LoadFaces));
                    labels.Add(Labels[tf]);
                }
            }
            catch
            {
                MessageBox.Show("Database bo'sh. Yuz qo‘shing!", "Warning",
                                MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
            }
        }

        // --- Qidirish tugmasi ---
        private void btnSearchPerson_Click(object sender, EventArgs e)
        {
            try
            {
                if (grabber != null) grabber.Dispose(); // Avvalgi connectionni yopamiz
                picFace.Visible = false; // Oldingi yuzni yashiramiz
                grabber = new Capture();                // Kamera ishga tushiriladi
                grabber.QueryFrame();

                Application.Idle += new EventHandler(FrameGrabber); // Idle event qo‘shiladi
            }
            catch (Exception ex)
            {
                MessageBox.Show("Kameraga ulanishda xato: " + ex.Message);
            }
        }


        // --- Yuz qo‘shish tugmasi ---
        private void btnAddPerson_Click(object sender, EventArgs e)
        {
            try
            {
                picFace.Visible = true;
                if (string.IsNullOrEmpty(txtid.Text))
                {
                    MessageBox.Show("Yangi yuz qo‘shish uchun ID kiriting!");
                    return;
                }

                gray = grabber.QueryGrayFrame().Resize(320, 240, INTER.CV_INTER_CUBIC);

                // Yuz qidirish
                MCvAvgComp[][] facesDetected = gray.DetectHaarCascade(
                    face,
                    1.2,
                    10,
                    HAAR_DETECTION_TYPE.DO_CANNY_PRUNING,
                    new Size(26, 26));

                if (facesDetected[0].Length == 0)
                {
                    MessageBox.Show("Yuz aniqlanmadi. Kameraga yaqinroq turing.");
                    return;
                }

                // Birinchi yuzni olish
                MCvAvgComp f = facesDetected[0][0];
                TrainedFace = currentFrame.Copy(f.rect).Convert<Gray, byte>();
                TrainedFace = TrainedFace.Resize(100, 100, INTER.CV_INTER_CUBIC);

                // Training listga qo‘shish
                trainingImages.Add(TrainedFace);
                labels.Add(txtid.Text);

                // Rasmni ko‘rsatish
                picFace.Visible = true;
                picFace.Image = TrainedFace;

                // Faylga yozish
                string savePath = Application.StartupPath + "/TrainedFaces/";
                Directory.CreateDirectory(savePath);

                File.WriteAllText(savePath + "TrainedLabels.txt", trainingImages.Count.ToString() + "%");

                for (int i = 1; i <= trainingImages.Count; i++)
                {
                    trainingImages[i - 1].Save(savePath + "face" + i + ".bmp");
                    File.AppendAllText(savePath + "TrainedLabels.txt", labels[i - 1] + "%");
                }

                MessageBox.Show(txtid.Text + " yuz ma'lumotlar bazasiga qo‘shildi!");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Xato: " + ex.Message, "Training Fail",
                                MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
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
                    string databaseName = Interaction.InputBox("Ma'lumotlar bazasi nomini kiriting:", "Ma'lumotlar bazasi nomi", "avtoehtiyotqismlar");
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
            MessageBox.Show(connectionString, "Ma'lumotlar bazasi joylashuvi");
        }
        string selectId = string.Empty;
        private void btnDelete_Click(object sender, EventArgs e)
        {
            if (!string.IsNullOrEmpty(selectId))
            {
                //if (mid.Text == "ID: ")
                //{
                //    MessageBox.Show("Iltimos, birorta mahsulot tanlang!");
                //    return;
                //}
                // Ma'lumotlar bazasidan selectId ga mos ma'lumotni o'chirish
                DeleteData(selectId);
            }
        }

        private void btnAdd_Click(object sender, EventArgs e)
        {
            //snomi = mnomi.Text;
            //snarxi = vergultozalash(mnarxi);
            //simage = mrasmi.Image;
            //byte[] imageBytes = ImageToByteArray(simage);
            //smavjud = vergultozalash(mmavjud);
            //sizoh = mizoh.Text;
            //skategoriya = mkategoriya.Text;
            //smx = mx.Text;
            //smy = my.Text;

            // Ma'lumotlarning bo'sh emasligini tekshirish
            if (string.IsNullOrWhiteSpace(snomi) ||
                string.IsNullOrWhiteSpace(snarxi) ||
                simage == null ||
                string.IsNullOrWhiteSpace(smavjud) ||
                string.IsNullOrWhiteSpace(sizoh) ||
                string.IsNullOrWhiteSpace(skategoriya) ||
                string.IsNullOrWhiteSpace(smx) ||
                string.IsNullOrWhiteSpace(smy))
            {
                MessageBox.Show("Iltimos, barcha ma'lumotlarni to'ldiring.");
                return;
            }

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                try
                {
                    connection.Open();

                    // INSERT so'rovi tayyorlash
                    string insertQuery = @"
                    INSERT INTO ehtiyotqismlar (Nom, Narx, Rasm, Miqdor, Izoh, Kategoriya, koordinataX, koordinataY)
                    VALUES (@Nom, @Narx, @Rasm, @Miqdor, @Izoh, @Kategoriya, @koordinataX, @koordinataY)
                    ";

                    SqlCommand command = new SqlCommand(insertQuery, connection);
                    command.Parameters.AddWithValue("@Nom", snomi);
                    command.Parameters.AddWithValue("@Narx", snarxi);

                    // Rasmni byte[] massiviga aylantirish
                    command.Parameters.AddWithValue("@Rasm", picFace);

                    command.Parameters.AddWithValue("@Miqdor", smavjud);
                    command.Parameters.AddWithValue("@Izoh", sizoh);
                    command.Parameters.AddWithValue("@Kategoriya", skategoriya);
                    //command.Parameters.AddWithValue("@koordinataX", relativeX.ToString());
                    //command.Parameters.AddWithValue("@koordinataY", relativeY.ToString());

                    // So'rovni bajaring
                    command.ExecuteNonQuery();

                    MessageBox.Show("Ma'lumotlar bazasiga muvaffaqiyatli qo'shildi.");
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Xatolik: " + ex.Message);
                }
            }
        }
        private void btnUpgrade_Click(object sender, EventArgs e)
        {
            if (!string.IsNullOrEmpty(selectId))
            {
                //if (mid.Text == "ID: ")
                //{
                //    MessageBox.Show("Iltimos, birorta mahsulot tanlang!");
                //    return;
                //}
                // Ma'lumotlarni yangilash uchun selectId ga mos ma'lumotlarni o'zgaruvchilarga o'rnating
                //snomi = mnomi.Text;
                //snarxi = vergultozalash(mnarxi);
                //simage = mrasmi.Image;
                //smavjud = vergultozalash(mmavjud);
                //sizoh = mizoh.Text;
                //skategoriya = mkategoriya.Text;
                //smx = mx.Text;
                //smy = my.Text;
                if (string.IsNullOrWhiteSpace(snomi) ||
                string.IsNullOrWhiteSpace(snarxi) ||
                simage == null ||
                string.IsNullOrWhiteSpace(smavjud) ||
                string.IsNullOrWhiteSpace(sizoh) ||
                string.IsNullOrWhiteSpace(skategoriya) //||
                //string.IsNullOrWhiteSpace(relativeX.ToString()) ||
                //string.IsNullOrWhiteSpace(relativeY.ToString())
                )
                {
                    MessageBox.Show("Iltimos, barcha ma'lumotlarni to'ldiring.");
                    return;
                }
                // Ma'lumotlarni ma'lumotlar bazasida yangilash
                UpdateData(selectId);
            }
        }

        private void btnClear_Click(object sender, EventArgs e)
        {
            //mid.Text = "ID: ";
            //mnomi.Clear();
            //mnarxi.Clear();
            //mrasmi.Image = null;
            //mmavjud.Clear();
            //mizoh.Clear();
            //mkategoriya.Text = String.Empty;
            //mkategoriya.SelectedIndex = -1;
            //mx.Clear();
            //my.Clear();
        }

        private void adminOynasidanChiqishToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.Close();
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
                MessageBox.Show("Xatolik: " + ex.Message);
            }

            return isConnected;
        }

        private bool checkTableExistence(string connectionString)
        {
            bool tableExists = false;
            string tableName = "ehtiyotqismlar";

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

                    // CREATE TABLE so'rovi
                    string createTableQuery = @"
       CREATE TABLE [dbo].[ehtiyotqismlar]
       (
           [Id] INT NOT NULL PRIMARY KEY IDENTITY, 
           [Nom] VARCHAR(MAX) NULL, 
           [Narx] VARCHAR(MAX) NULL, 
           [Rasm] IMAGE NULL, 
           [Miqdor] VARCHAR(MAX) NULL, 
           [Izoh] VARCHAR(MAX) NULL, 
           [Kategoriya] VARCHAR(MAX) NULL, 
           [koordinataX] VARCHAR(MAX) NULL, 
           [koordinataY] VARCHAR(MAX) NULL
       )";
                    SqlCommand command = new SqlCommand(createTableQuery, connection);
                    command.ExecuteNonQuery();

                    MessageBox.Show("Ehtiyotqismlar jadvali muvaffaqiyatli yaratildi.");
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Xatolik: " + ex.Message);
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

        private void verguljoylash(object sender, KeyPressEventArgs e)
        {
            // Faqat raqamlarni qabul qilish
            if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar))
            {
                e.Handled = true; // Hodisa bajarilmaydi
            }
            if (sender is TextBox textBox)
            {
                //TextBox textBox = (TextBox)sender;
                string text = textBox.Text.Replace(",", ""); // Vergullarni olib tashlash

                // Yangi harf kiritilganda va matn uzunligi 0 dan katta bo'lsa
                if (text.Length > 0)
                {
                    int len = text.Length;
                    int groupCount = len / 3 - 1; // 3 ta raqamdan keyingi vergullar soni
                    int startIndex = len % 3; // Birinchi verguldan keyin keladigan raqamlar soni
                    StringBuilder sb = new StringBuilder(text);
                    if (char.IsDigit(e.KeyChar))
                    {
                        for (int i = groupCount; i >= 0; i--)
                        {
                            sb.Insert(startIndex + i * 3 + 1, ","); // Har 3 raqamdan keyin vergul qo'shish
                        }
                    }
                    else
                    {
                        for (int i = groupCount; i >= 0; i--)
                        {
                            sb.Insert(startIndex + i * 3, ","); // Har 3 raqamdan keyin vergul qo'shish
                        }
                    }
                    textBox.Text = sb.ToString();
                    textBox.SelectionStart = textBox.Text.Length;
                }
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
                    MessageBox.Show("Xatolik: " + ex.Message);
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
                    MessageBox.Show("Xatolik: " + ex.Message);
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
                MessageBox.Show("Xatolik yuz berdi: " + ex.Message);
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
                    MessageBox.Show("Fayl mavjud emas: " + fileName);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Xatolik yuz berdi: " + ex.Message);
            }

            return connectionString;
        }



        private void DeleteData(string id)
        {
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                try
                {
                    connection.Open();

                    // DELETE so'rovi tayyorlash
                    string deleteQuery = "DELETE FROM ehtiyotqismlar WHERE Id = @Id";

                    SqlCommand command = new SqlCommand(deleteQuery, connection);
                    command.Parameters.AddWithValue("@Id", id);

                    // So'rovni bajaring
                    int rowsAffected = command.ExecuteNonQuery();

                    if (rowsAffected > 0)
                    {
                        MessageBox.Show("Ma'lumot muvaffaqiyatli o'chirildi.");
                        // O'chirilgan ma'lumotni ko'rsatish uchun kerakli amallar
                    }
                    else
                    {
                        MessageBox.Show("O'chirishda xatolik yuz berdi. Ma'lumot topilmadi yoki o'chirilmadi.");
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Xatolik: " + ex.Message);
                }
            }
        }


        private void UpdateData(string id)
        {
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                try
                {
                    connection.Open();

                    // UPDATE so'rovi tayyorlash
                    string updateQuery = @"
                UPDATE ehtiyotqismlar 
                SET Nom = @Nom, 
                    Narx = @Narx, 
                    Rasm = @Rasm, 
                    Miqdor = @Miqdor, 
                    Izoh = @Izoh, 
                    Kategoriya = @Kategoriya, 
                    koordinataX = @koordinataX, 
                    koordinataY = @koordinataY 
                WHERE Id = @Id
            ";

                    SqlCommand command = new SqlCommand(updateQuery, connection);
                    command.Parameters.AddWithValue("@Nom", snomi);
                    command.Parameters.AddWithValue("@Narx", snarxi);

                    // Rasmni byte[] massiviga aylantirish
                    byte[] imageBytes = ImageToByteArray(simage);
                    command.Parameters.AddWithValue("@Rasm", imageBytes);

                    command.Parameters.AddWithValue("@Miqdor", smavjud);
                    command.Parameters.AddWithValue("@Izoh", sizoh);
                    command.Parameters.AddWithValue("@Kategoriya", skategoriya);
                    //command.Parameters.AddWithValue("@koordinataX", relativeX.ToString());
                    //command.Parameters.AddWithValue("@koordinataY", relativeY.ToString());
                    command.Parameters.AddWithValue("@Id", id);

                    // So'rovni bajaring
                    int rowsAffected = command.ExecuteNonQuery();

                    if (rowsAffected > 0)
                    {
                        MessageBox.Show("Ma'lumotlar muvaffaqiyatli yangilandi.");
                        // Yangilangan ma'lumotlarni ko'rsatish uchun kerakli amallar
                    }
                    else
                    {
                        MessageBox.Show("Yangilashda xatolik yuz berdi. Ma'lumot topilmadi yoki yangilanmadi.");
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Xatolik: " + ex.Message);
                }
            }
        }

        private void FrmPrincipal_FormClosing(object sender, FormClosingEventArgs e)
        {
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
                    MessageBox.Show("Xatolik: " + ex.Message);
                }
            }
        }




        // --- FrameGrabber (kamera oqimi) ---
        void FrameGrabber(object sender, EventArgs e)
        {
            try
            {
                // Frame olish
                currentFrame = grabber.QueryFrame().Resize(320, 240, INTER.CV_INTER_CUBIC);

                // Gray formati
                gray = currentFrame.Convert<Gray, Byte>();

                // Yuzlarni aniqlash
                MCvAvgComp[][] facesDetected = gray.DetectHaarCascade(
                    face,
                    1.2,
                    10,
                    HAAR_DETECTION_TYPE.DO_CANNY_PRUNING,
                    new Size(20, 20));

                if (facesDetected[0].Length > 0)
                {
                    MCvAvgComp f = facesDetected[0][0]; // Faqat birinchi yuzni olish

                    result = currentFrame.Copy(f.rect).Convert<Gray, byte>()
                              .Resize(100, 100, INTER.CV_INTER_CUBIC);

                    // Yuz atrofiga yashil to‘rtburchak chizish
                    currentFrame.Draw(f.rect, new Bgr(Color.Green), 2);

                    // Yuzni tanish
                    if (trainingImages.Count > 0)
                    {
                        MCvTermCriteria termCrit = new MCvTermCriteria(ContTrain, 0.001);

                        EigenObjectRecognizer recognizer = new EigenObjectRecognizer(
                            trainingImages.ToArray(),
                            labels.ToArray(),
                            3000,
                            ref termCrit);

                        name = recognizer.Recognize(result);

                        // Ismni chiqarish
                        currentFrame.Draw(name ?? "Unknown",
                            ref font, new Point(f.rect.X - 2, f.rect.Y - 2),
                            new Bgr(Color.Red));

                        // ID textboxga chiqarish
                        txtid.Text = string.IsNullOrEmpty(name) ? "Unknown" : name;
                    }

                    // Rasmni chiqarish
                    imageBoxFrameGrabber.Image = currentFrame;

                    // Faqat 1 marta ishlashini istasangiz → eventni o‘chirish:
                    Application.Idle -= FrameGrabber;
                }
                else
                {
                    // Yuz topilmasa ham dastur davom etadi
                    imageBoxFrameGrabber.Image = currentFrame;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("FrameGrabber xato: " + ex.Message);
            }
        }
    }
}
