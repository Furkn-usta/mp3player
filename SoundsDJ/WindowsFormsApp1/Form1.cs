using System;
using System.Collections.Generic;
using System.Windows.Forms;
using NAudio.Wave;
using NAudio.Dsp;
using NAudio.Wave.SampleProviders;

namespace WindowsFormsApp1
{
    public partial class Form1 : Form
    {
        private Mp3FileReader mp3Reader = null;
        private WaveOutEvent output = null;
        private VolumeSampleProvider volumeProvider = null;
        private List<string> musicFiles = new List<string>();
        private ISampleProvider sampleProvider;
        private Complex[] fftBuffer;
        private int fftPos;
        private int fftLength = 1024; // 1024-point FFT
        private int m = (int)Math.Log(1024, 2); // Log2(fftLength)
        private bool fftCalculated = false;
        private Timer fftTimer;
        private int currentTrackIndex = 0;

        public Form1()
        {
            InitializeComponent();
            InitializeFFT();

            // MessageBox.Show("Form1 Loaded"); // Debugging için ekledik
        }

        private float playbackSpeed = 1.0f; // Varsayılan hız: 1.0f (normal hız)

        private void InitializeFFT()
        {
            fftBuffer = new Complex[fftLength];
            fftTimer = new Timer();
            fftTimer.Interval = 50; // 50ms interval for FFT analysis
            fftTimer.Tick += FftTimer_Tick;
        }

        private void button1_Click(object sender, EventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "MP3 Files (*.mp3)|*.mp3";
            ofd.Multiselect = true; // Çoklu seçim için izin ver
            if (ofd.ShowDialog() != DialogResult.OK) return;

            foreach (var file in ofd.FileNames)
            {
                musicFiles.Add(file);
                listBox1.Items.Add(System.IO.Path.GetFileName(file));
            }

            label1.Text = "Total Music Files: " + musicFiles.Count.ToString();
        }

        private void button2_Click(object sender, EventArgs e)
        {
            if (output != null)
            {
                if (output.PlaybackState == PlaybackState.Playing) output.Pause();
                else if (output.PlaybackState == PlaybackState.Paused) output.Play();
            }
        }

        private void button3_Click(object sender, EventArgs e)
        {
            if (listBox1.SelectedIndex != -1)
            {
                currentTrackIndex = listBox1.SelectedIndex;
                PlayTrack(currentTrackIndex);
            }
        }
        private void PlayTrack(int trackIndex)
        {
            if (trackIndex >= 0 && trackIndex < musicFiles.Count)
            {
                DisposeWave();

                // Müzik dosyasını okuyan bir ISampleProvider oluşturun
                mp3Reader = new Mp3FileReader(musicFiles[trackIndex]);
                sampleProvider = mp3Reader.ToSampleProvider();

                // Ses kontrolü için VolumeSampleProvider kullanın
                volumeProvider = new VolumeSampleProvider(sampleProvider)
                {
                    Volume = volumeTrackBar.Value / 100.0f
                };

                // Hız değiştirme işlemi için OffsetSampleProvider oluşturun
                OffsetSampleProvider offsetSampleProvider = new OffsetSampleProvider(volumeProvider);
                offsetSampleProvider.SkipOver = TimeSpan.FromSeconds(0); // Başlangıçta atlanacak süre
                offsetSampleProvider.Take = TimeSpan.FromSeconds(mp3Reader.TotalTime.TotalSeconds); // Müziğin tüm süresi

                // WaveOutEvent nesnesi oluşturun ve müziği çalın
                output = new WaveOutEvent();
                output.PlaybackStopped += Output_PlaybackStopped; // Olayı buraya bağlayın
                output.Init(offsetSampleProvider);
                output.Play();

                trackBar1.Maximum = (int)mp3Reader.TotalTime.TotalSeconds;
                timer1.Start();
                fftTimer.Start();

                button2.Enabled = true;

                // Playback durumunu kontrol edelim
                //  MessageBox.Show("Playback state: " + output.PlaybackState); // Debugging için ekledik
            }
        }

        private void Output_PlaybackStopped(object sender, StoppedEventArgs e)
        {
            //  MessageBox.Show("Playback stopped!"); // Debugging için ekledik
            if (InvokeRequired)
            {
                Invoke(new Action<object, StoppedEventArgs>(Output_PlaybackStopped), sender, e);
                return;
            }

            // Move to the next track in the list
            currentTrackIndex++;
            if (currentTrackIndex < musicFiles.Count)
            {
                listBox1.SelectedIndex = currentTrackIndex;
                PlayTrack(currentTrackIndex);
            }
            else
            {
                // Stop timers if all tracks are played
                timer1.Stop();
                fftTimer.Stop();
                currentTrackIndex = 0; // Reset to the first track
                StopPlayback();
            }
        }

        private void DisposeWave()
        {
            if (output != null)
            {
                output.PlaybackStopped -= Output_PlaybackStopped; // Olayı kaldırın
                if (output.PlaybackState == PlaybackState.Playing) output.Stop();
                output.Dispose();
                output = null;
            }
            if (mp3Reader != null)
            {
                mp3Reader.Dispose();
                mp3Reader = null;
            }
            fftTimer.Stop();
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            DisposeWave();
        }

        private void StopPlayback()
        {
            // Çalma işlemini durdur
            DisposeWave();
            // Diğer gerekli işlemler...
        }

        private void trackBar1_Scroll(object sender, EventArgs e)
        {
            if (mp3Reader != null)
            {
                mp3Reader.CurrentTime = TimeSpan.FromSeconds(trackBar1.Value);
            }
            // trackBar değeri 10 olduğunda bir sonraki şarkıyı çal
            if (trackBar1.Value == 10)
            {
                // Şu an çalan şarkının index'ini bir artırarak bir sonraki şarkıyı çal
                currentTrackIndex++;
                if (currentTrackIndex < musicFiles.Count)
                {
                    listBox1.SelectedIndex = currentTrackIndex;
                    PlayTrack(currentTrackIndex);
                }
                else
                {
                    // Eğer sonraki şarkı yoksa, işlemi durdur
                    StopPlayback();
                }
            }
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            if (mp3Reader != null)
            {
                trackBar1.Value = (int)mp3Reader.CurrentTime.TotalSeconds;
            }
        }

        private void volumeTrackBar_Scroll(object sender, EventArgs e)
        {
            if (volumeProvider != null)
            {
                volumeProvider.Volume = volumeTrackBar.Value / 100.0f;
            }
        }

        private void FftTimer_Tick(object sender, EventArgs e)
        {
            /*
            if (sampleProvider != null)
            {
                float[] buffer = new float[fftLength];
                int samplesRead = sampleProvider.Read(buffer, 0, fftLength);

                if (samplesRead > 0)
                {
                    // Hızı kullanarak okunan örnek sayısını ayarlayın
                    int adjustedSampleCount = (int)(samplesRead / playbackSpeed);

                    for (int i = 0; i < adjustedSampleCount; i++)
                    {
                        fftBuffer[fftPos].X = (float)(buffer[i] * FastFourierTransform.HammingWindow(fftPos, fftLength));
                        fftBuffer[fftPos].Y = 0;
                        fftPos++;
                        if (fftPos >= fftLength)
                        {
                            fftPos = 0;
                            fftCalculated = true;
                        }
                    }

                    if (fftCalculated)
                    {
                        fftCalculated = false;
                        FastFourierTransform.FFT(true, m, fftBuffer);
                        float maxFreq = 0;
                        float maxAmplitude = float.MinValue;
                        for (int i = 0; i < fftBuffer.Length / 2; i++)
                        {
                            float amplitude = fftBuffer[i].X * fftBuffer[i].X + fftBuffer[i].Y * fftBuffer[i].Y;
                            if (amplitude > maxAmplitude)
                            {
                                maxAmplitude = amplitude;
                                maxFreq = i;
                            }
                        }
                        float freq = maxFreq * sampleProvider.WaveFormat.SampleRate / fftBuffer.Length;
                        labelFrequencies.Invoke((MethodInvoker)delegate
                        {
                            labelFrequencies.Text = $"Frequency: {freq} Hz";
                        });
                    }
                }
            }
            */
        }

        private void openWaveToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "MP3 Files (*.mp3)|*.mp3";
            ofd.Multiselect = true; // Çoklu seçim için izin ver
            if (ofd.ShowDialog() != DialogResult.OK) return;

            foreach (var file in ofd.FileNames)
            {
                musicFiles.Add(file);
                listBox1.Items.Add(System.IO.Path.GetFileName(file));
            }

            label1.Text = "Total Music Files: " + musicFiles.Count.ToString();
        }
    }
}
