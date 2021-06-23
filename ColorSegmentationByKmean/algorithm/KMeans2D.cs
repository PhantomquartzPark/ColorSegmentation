using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ColorSegmentationByKmean.algorithm
{
    class KMeans2D
    {
        public readonly int n_clusters;
        public readonly int random_state;
        public readonly int maxiter;
        private readonly Random random;
        private readonly List<double[]> clusters;

        public int[] labels;
        public double[,] cluster_centers;

        public KMeans2D(int n_clusters, int random_state = 42, int maxiter = 100)
        {
            this.n_clusters = n_clusters;
            this.random_state = random_state;
            this.maxiter = maxiter;

            this.random = new Random(random_state);
            this.clusters = new List<double[]>(n_clusters);
        }

        public void Fit(double[,] orgX, bool isKMeanPP = true)
        {
            // 最大値が255という前提で正規化（果たして必要か…？）
            var X = (double[,])orgX.Clone();
            for (int i = 0; i < X.GetLength(0); i++)
            {
                for (int j = 0; j < X.GetLength(1); j++)
                {
                    X[i,j] /= 255;
                }
            }

            // k-means++
            if (isKMeanPP)
            {
                // 最初のクラスタを定める
                var first_cluster = new double[X.GetLength(1)];
                var first_index = random.Next(X.GetLength(0));
                for (int i = 0; i < X.GetLength(1); i++)
                {
                    first_cluster[i] = X[first_index, i];
                }
                clusters.Add(first_cluster);

                // 残りのクラスタを定める
                if (n_clusters > 1)
                {
                    var p_cumsum = new double[X.GetLength(0)];
                    for (int i = 0; i < p_cumsum.Length; i++) { p_cumsum[i] = 0.0; }
                    var div_cumsum = 0.0;
                    while ((clusters.Count < n_clusters) && (clusters.Count < X.GetLength(0)))
                    {
                        var next_cluster = new double[X.GetLength(1)];
                        //var p = new double[X.GetLength(0)];
                        //double div = 0.0;
                        for (int i = 0; i < X.GetLength(0); i++)
                        {
                            for (int j = 0; j < X.GetLength(1); j++)
                            {
                                // 新たに追加されたセントロイドの分を足して保持しておく
                                double dx2 = Math.Pow(X[i, j] - clusters[clusters.Count - 1][j], 2);
                                p_cumsum[i] += dx2;
                                div_cumsum += dx2;
                                /*
                                // 元コード
                                foreach (var c in clusters)
                                {
                                    double dx2 = Math.Pow(X[i, j] - c[j], 2);
                                    p[i] += dx2;
                                    div += dx2;
                                }*/
                            }
                        }
                        // 確率を用いて新たなセントロイドを決める
                        var p = (double[])p_cumsum.Clone();
                        double div = div_cumsum;
                        for (int i = 0; i < X.GetLength(0); i++)
                        {
                            p[i] /= div;
                        }
                        var next_index = Choice(p); // ルーレット選択
                        for (int i = 0; i < X.GetLength(1); i++)
                        {
                            next_cluster[i] = X[next_index, i];
                        }
                        clusters.Add(next_cluster);
                    }
                }
            }
            // k-means
            else
            {
                // 規定のクラスタ数とデータ数を比較し，少ない方をセントロイドの上限個数とする
                var maxcluster = Math.Min(n_clusters, X.GetLength(0));

                // indexと格納値が同じリストを用意し，重複なしの乱数を生成する
                var randlist = new List<int>(X.GetLength(0));
                for (int i = 0; i < X.GetLength(0); i++) { randlist.Add(i); }
                
                // 上限個数分取り出してセントロイドを決めていく
                for (int i = 0; i < maxcluster; i++)
                {
                    var next_cluster = new double[X.GetLength(1)];
                    var choose_index = random.Next(0, randlist.Count - 1);
                    var next_index = randlist[choose_index];
                    randlist.RemoveAt(choose_index);        // 該当箇所は削除する
                    for (int j = 0; j < X.GetLength(1); j++)
                    {
                        next_cluster[j] = X[next_index, j];
                    }
                    clusters.Add(next_cluster);
                }
            }
            

            // ラベルの初期化，ラベル付け
            this.labels = new int[X.GetLength(0)];
            Labeling(X);

            // その他必要物の準備（前のラベルの状態，セントロイドの初期化）
            var prev_labels = new int[X.GetLength(0)];
            for (int i = 0; i < prev_labels.Length; i++) { prev_labels[i] = 0; }
            this.cluster_centers = new double[this.n_clusters, X.GetLength(1)];

            // 訓練箇所
            for (int count = 0; count < maxiter; count++)
            {
                // ラベルを確認し，前の状態から変化が無ければ強制終了
                bool isSameLabels = true;
                for (int i = 0; i < X.GetLength(0); i++)
                {
                    if (this.labels[i] != prev_labels[i])
                    {
                        isSameLabels = false;
                        break;
                    }
                }
                if (isSameLabels) { break; }

                // XXとn_membersを初期化（クラスタごとの総和と，
                // そこから平均（クラスタ重心）を算出するために用いるメンバ数）
                var XX = new double[n_clusters, X.GetLength(1)];
                for (int i = 0; i < XX.GetLength(0); i++) {
                    for (int j = 0; j < XX.GetLength(1); j++)
                    {
                        XX[i, j] = 0;
                    }
                }
                var n_members = new int[n_clusters];
                for (int i = 0; i < n_members.Length; i++) { n_members[i] = 0; }
                
                // ラベルを見ながらXXとn_membersを更新
                for (int i = 0; i < X.GetLength(0); i++)
                {
                    for (int j = 0; j < X.GetLength(1); j++)
                    {
                        XX[this.labels[i], j] += X[i, j];
                    }
                    n_members[this.labels[i]]++;
                }
                // 平均を算出してセントロイド（クラスタ重心）を更新
                for (int i = 0; i < n_clusters; i++)
                {
                    for (int j = 0; j < X.GetLength(1); j++)
                    {
                        this.cluster_centers[i, j] = XX[i, j] / n_members[i];
                    }
                }
                // 現ラベルを過去のもの（pre_labels）とする
                prev_labels = this.labels;
                this.labels = new int[X.GetLength(0)]; // 再初期化
                //Array.Copy(this.labels, 0, prev_labels, 0, this.labels.Length);
                Labeling(X);
            }

            // 最大値が255という前提で正規化を解除する
            for (int i = 0; i < cluster_centers.GetLength(0); i++)
            {
                for (int j = 0; j < cluster_centers.GetLength(1); j++)
                {
                    cluster_centers[i, j] *= 255;
                }
            }
        }

        /// <summary>
        /// ラベリング用メソッド
        /// </summary>
        /// <param name="X"></param>
        private void Labeling(double[,] X)
        {
            // 距離計算とラベル付け
            for (int i = 0; i < X.GetLength(0); i++)
            {
                double nearest_dist = double.MaxValue;
                int nearest_index = 0;
                for (int j = 0; j < clusters.Count; j++)
                {
                    // 距離計算
                    double tmpdist = 0.0;
                    for (int k = 0; k < X.GetLength(1); k++)
                    {
                        double dif = X[i, k] - clusters[j][k];
                        tmpdist += dif * dif;
                    }

                    // より近いところを候補として残す
                    if (tmpdist < nearest_dist)
                    {
                        nearest_dist = tmpdist;
                        nearest_index = j;
                    }
                }
                // 最後まで残った候補を基にラベル付けする
                this.labels[i] = nearest_index;
            }
        }

        /// <summary>
        /// 確率に基づいたindex取得のためのメソッド
        /// </summary>
        /// <param name="p"></param>
        /// <returns></returns>
        private int Choice(double[] p)
        {
            double sum = 0.0;
            foreach (var pp in p)
            {
                sum += pp;
            }

            // ルーレット方式でindexを決める
            // （以下のようにrouletteの値にsumをかけても，
            // pのクローンを作って各要素をsumで割る2通りがあるはず
            // （後者の方が乱数間の隙間ができにくい…？））
            double roulette = this.random.NextDouble() * sum;
            int index = 0;
            for (int i = 0; i < p.Length; i++)
            {
                index = i;
                // rouletteを引いて0未満（又は以下）になったら
                // そこが対象だったということ
                roulette -= p[i];
                if (roulette < 0) { break; }
            }

            return index;
        }
    }
}
