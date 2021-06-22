using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using ColorSegmentationByKmean.algorithm;

namespace ColorSegmentationByKmean
{
    public partial class Form1 : Form
    {
        private string fileName = "";
        private int imageHeight = 0;
        private int imageWidth = 0;
        private double[,] Ximage = null;

        public Form1()
        {
            InitializeComponent();
        }

        private void DropPanel_DragDrop(object sender, DragEventArgs e)
        {
            // ドロップされたファイルの1枚のみ取得
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop, false);
            if (files.Length > 1) { MessageBox.Show("画像は最初の1枚だけ選択されます．", "注意", MessageBoxButtons.OK, MessageBoxIcon.Exclamation); }

            // ファイルの情報(+名前)を抜き出す
            string file = files[0];
            string fileName = System.IO.Path.GetFileName(file);
            textBox1.Text = fileName;
            this.Refresh();
            this.fileName = fileName.Substring(0, fileName.LastIndexOf('.'));

            // イメージに落とし込み，必要なデータを抽出する
            textBox2.Text = "入力画像変換中";
            this.Refresh();
            var image = new Bitmap(file);
            this.imageHeight = image.Height;
            this.imageWidth = image.Width;
            this.Ximage = new double[this.imageHeight * this.imageWidth, 3];
            for (int h = 0; h < imageHeight; h++)
            {
                for (int w = 0; w < imageWidth; w++)
                {
                    long index = ((long)h * imageWidth) + w;
                    Color color = image.GetPixel(w, h);
                    Ximage[index, 0] = color.R;
                    Ximage[index, 1] = color.G;
                    Ximage[index, 2] = color.B;
                }
            }
            image.Dispose();
            textBox2.Text = "入力画像変換終了";
            this.Refresh();
        }

        private void Button1_Click(object sender, EventArgs e)
        {
            if (this.Ximage != null)
            {
                textBox2.Text = "カラーセグメンテーション実行中";
                this.Refresh();
                // カラーセグメンテーションの実行と画像の保存
                for (int n_clusters = (int)numericUpDown1.Value; n_clusters <= (int)numericUpDown2.Value; n_clusters++)
                {
                    // Kmeansインスタンスの作成と実行（訓練）
                    var kmeans = new KMeans2D(n_clusters,
                        (int)numericUpDown3.Value, (int)numericUpDown4.Value);
                    kmeans.Fit(Ximage, isKMeanPP: checkBox1.Checked);


                    var fig = new Bitmap(imageWidth, imageHeight);
                    for (int h = 0; h < fig.Height; h++)
                    {
                        for (int w = 0; w < fig.Width; w++)
                        {
                            long index = ((long)h * fig.Width) + w;
                            double r = kmeans.cluster_centers[kmeans.labels[index], 0];
                            double g = kmeans.cluster_centers[kmeans.labels[index], 1];
                            double b = kmeans.cluster_centers[kmeans.labels[index], 2];
                            var color = Color.FromArgb(
                                double.IsNaN(r) ? 255 : (byte)r,
                                double.IsNaN(g) ? 255 : (byte)g,
                                double.IsNaN(b) ? 255 : (byte)b
                            );
                            fig.SetPixel(w, h, color);
                        }
                    }

                    // 保存
                    if (!Directory.Exists(this.fileName)) { Directory.CreateDirectory(this.fileName); }
                    string path = this.fileName + "/"
                        + this.fileName + "(k=" + n_clusters + ")." + domainUpDown1.Text;
                    System.Drawing.Imaging.ImageFormat saveFormat;
                    switch (domainUpDown1.Text)
                    {
                        case "png":
                            saveFormat = System.Drawing.Imaging.ImageFormat.Png;
                            break;
                        case "bmp":
                            saveFormat = System.Drawing.Imaging.ImageFormat.Bmp;
                            break;
                        default:
                            saveFormat = System.Drawing.Imaging.ImageFormat.Jpeg;
                            break;
                    }
                    fig.Save(path, saveFormat);
                    fig.Dispose();
                }
                textBox2.Text = "カラーセグメンテーション実行終了";
                this.Refresh();
            }
            else
            {
                textBox2.Text = "カラーセグメンテーション実行失敗";
                this.Refresh();
                MessageBox.Show("パネルに画像をドラッグ&ドロップしてください．", "注意", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
            }
        }

        private void DropPanel_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effect = DragDropEffects.All;
            }
            else
            {
                e.Effect = DragDropEffects.None;
            }
        }

        private void NumericUpDown2_ValueChanged(object sender, EventArgs e)
        {
            if (numericUpDown2.Value < numericUpDown1.Value)
            {
                numericUpDown1.Value = numericUpDown2.Value;
            }
        }

        private void NumericUpDown1_ValueChanged(object sender, EventArgs e)
        {
            if (numericUpDown1.Value > numericUpDown2.Value)
            {
                numericUpDown2.Value = numericUpDown1.Value;
            }
        }
    }
}
