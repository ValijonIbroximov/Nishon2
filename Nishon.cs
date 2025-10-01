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
        // --- O'zgaruvchilar ---
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

        // --- Form Load ---

        private void FrmPrincipal_Load(object sender, EventArgs e)
        {
            // Rasmlarni yumaloq shaklga keltirish uchun (agar kerak bo‘lsa)
            CirclePic(g6); CirclePic(g7); CirclePic(g8); CirclePic(g9); CirclePic(g10);
            CirclePic(r6); CirclePic(r7); CirclePic(r8); CirclePic(r9); CirclePic(r10);
            this.WindowState = FormWindowState.Maximized;
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
