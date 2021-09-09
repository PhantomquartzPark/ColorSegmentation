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
            // Preparation
            double[,] X = (double[,])orgX.Clone();
            int n_pixels   = X.GetLength(0); // i.e. H*W
            int n_channels = X.GetLength(1); // RGB = 3

            // Normalize
            for (int pixel = 0; pixel < n_pixels; pixel++)
            {
                for (int channel = 0; channel < n_channels; channel++)
                {
                    X[pixel, channel] /= 255;
                }
            }

            // k-means++
            if (isKMeanPP)
            {
                // Calculate First Cluster (Random Selection)
                double[] first_cluster = new double[n_channels];
                int first_index = random.Next(n_pixels);
                for (int channel = 0; channel < n_channels; channel++)
                {
                    first_cluster[channel] = X[first_index, channel];
                }
                this.clusters.Add(first_cluster);

                // Calculate Rest Cluster (Roulette Wheel Selection)
                if (this.n_clusters > 1)
                {
                    // Initialize min_distances (Distance between X and NearestCluster)
                    double[] min_distances = new double[n_pixels];
                    for (int pixel = 0; pixel < n_pixels; pixel++)
                    {
                        min_distances[pixel] = 0.0;
                        for (int channel = 0; channel < n_channels; channel++)
                        {
                            double dist = X[pixel, channel] - first_cluster[channel];
                            min_distances[pixel] += dist * dist;
                        }
                        //min_distances[pixel] = Math.Sqrt(min_distances[pixel]); // probably not so important
                    }

                    while ((this.clusters.Count < this.n_clusters) && (this.clusters.Count < n_pixels))
                    {
                        double sum_distances = 0.0;

                        // Update min_distances
                        for (int pixel = 0; pixel < n_pixels; pixel++)
                        {
                            double distance = 0.0;
                            for (int channel = 0; channel < n_channels; channel++)
                            {
                                double dist = X[pixel, channel] - this.clusters[this.clusters.Count - 1][channel];
                                distance += dist * dist;
                            }
                            //distance = Math.Sqrt(distance); // probably not so important
                            min_distances[pixel] = Math.Min(min_distances[pixel], distance);
                            sum_distances += distance;
                        }

                        // Calculate Probability
                        double[] probs = (double[])min_distances.Clone(); // probabilities
                        for (int pixel = 0; pixel < n_pixels; pixel++)
                        {
                            probs[pixel] /= sum_distances;
                        }

                        // Select new Cluster
                        double[] select_cluster = new double[n_channels];
                        int select_index = this.Select(probs);
                        for (int channel = 0; channel < n_channels; channel++)
                        {
                            select_cluster[channel] = X[select_index, channel];
                        }
                        this.clusters.Add(select_cluster);
                    }
                }
            }
            // k-means
            else
            {
                // Generate IndexList
                List<int> randlist = new List<int>(n_pixels);
                for (int i = 0; i < n_pixels; i++) { randlist.Add(i); }

                // Select Clusters
                int max_clusters = Math.Min(this.n_clusters, n_pixels);
                for (int i = 0; i < max_clusters; i++)
                {
                    int randlist_index = random.Next(0, randlist.Count - 1);

                    double[] select_cluster = new double[n_channels];
                    int select_index = randlist[randlist_index];
                    for (int channel = 0; channel < n_channels; channel++)
                    {
                        select_cluster[channel] = X[select_index, channel];
                    }
                    this.clusters.Add(select_cluster);
                    randlist.RemoveAt(randlist_index);
                }
            }

            // Labeling
            this.labels = new int[n_pixels];
            Labeling(X);

            int[] prev_labels = new int[n_pixels];
            for (int i = 0; i < prev_labels.Length; i++) { prev_labels[i] = 0; }
            this.cluster_centers = new double[this.n_clusters, X.GetLength(1)];

            // Training
            for (int count = 0; count < maxiter; count++)
            {
                // Check labels
                bool isSameLabels = true;
                for (int i = 0; i < n_pixels; i++)
                {
                    if (this.labels[i] != prev_labels[i])
                    {
                        isSameLabels = false;
                        break;
                    }
                }
                if (isSameLabels) { break; }

                // Initialize sum_X (summation of color value) and n_members
                double[,] sum_X = new double[this.n_clusters, n_channels];
                for (int i = 0; i < this.n_clusters; i++) {
                    for (int channel = 0; channel < n_channels; channel++)
                    {
                        sum_X[i, channel] = 0;
                    }
                }
                int[] n_members = new int[n_clusters];
                for (int i = 0; i < n_members.Length; i++) { n_members[i] = 0; }
                
                // Calculate sum_X and n_members
                for (int pixel = 0; pixel < n_pixels; pixel++)
                {
                    for (int channel = 0; channel < n_channels; channel++)
                    {
                        sum_X[this.labels[pixel], channel] += X[pixel, channel];
                    }
                    n_members[this.labels[pixel]]++;
                }

                // Calculate MeanValue (Update Centroid)
                for (int i = 0; i < n_clusters; i++)
                {
                    for (int j = 0; j < n_channels; j++)
                    {
                        this.cluster_centers[i, j] = sum_X[i, j] / n_members[i];
                    }
                }

                // Update labels
                prev_labels = this.labels;
                this.labels = new int[n_pixels]; // reinitialize
                Labeling(X);
            }

            // Inverse Normalize (Condition: this.cluster_centers[i, j] range from 0. to 1.)
            for (int i = 0; i < this.cluster_centers.GetLength(0); i++)
            {
                for (int channel = 0; channel < this.cluster_centers.GetLength(1); channel++)
                {
                    this.cluster_centers[i, channel] *= 255;
                }
            }
        }

        /// <summary>
        /// ラベリング用メソッド
        /// </summary>
        /// <param name="X"></param>
        private void Labeling(double[,] X)
        {
            // Calculate Distance and Labeling
            for (int i = 0; i < X.GetLength(0); i++)
            {
                double nearest_distance = double.MaxValue;
                int nearest_index = 0;
                for (int j = 0; j < this.clusters.Count; j++)
                {
                    // Calculate Distance
                    double distance = 0.0;
                    for (int k = 0; k < X.GetLength(1); k++)
                    {
                        double dist = X[i, k] - clusters[j][k];
                        distance += dist * dist;
                    }

                    // Record NearestDistance
                    if (distance < nearest_distance)
                    {
                        nearest_distance = distance;
                        nearest_index = j;
                    }
                }

                // Labeling
                this.labels[i] = nearest_index;
            }
        }

        /// <summary>
        /// 確率に基づいたindex取得のためのメソッド
        /// </summary>
        /// <param name="probs">probabilities</param>
        /// <returns></returns>
        private int Select(double[] probs)
        {
            double sum_probs = 0.0; // maybe close to 1. (include error)
            foreach (var prob in probs)
            {
                sum_probs += prob;
            }

            // Roulette Wheel Selection
            double roulette = this.random.NextDouble() * sum_probs;
            int index = 0;
            for (int i = 0; i < probs.Length; i++)
            {
                index = i;

                // found index if (roulette < 0) (or (roulette <= 0))
                roulette -= probs[i];
                if (roulette < 0) { break; }
            }

            return index;
        }
    }
}
