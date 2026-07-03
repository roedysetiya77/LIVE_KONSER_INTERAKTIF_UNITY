using UnityEngine;
using UnityEngine.Video;
using UnityEngine.Networking;
using System.Collections;
using System.IO;
using SimpleFileBrowser; // Pastikan package SimpleFileBrowser sudah di-install

public class MediaRuntimeLoader : MonoBehaviour
{
    [Header("Komponen Output")]
    public VideoPlayer videoPlayer;   // Masukkan objek Cube.1_9 (Layar)
    public AudioSource audioSource;   // Masukkan komponen AudioSource panggung
    public MeshRenderer layarRenderer; // Tarik objek Cube.1_9 ke sini juga

    [Header("Material Panggung")]
    public Material matLayarVideo;    // Tarik file Mat_Layar_Video ke sini

    private string jalurVideo = "";
    private string jalurAudio = "";
    private WebCamTexture webcamTexture;
    private bool isCamActive = false;

    // 1. Fungsi pemicu untuk memilih VIDEO (.mp4)
    public void PilihFileVideo()
    {
        FileBrowser.SetFilters(true, new FileBrowser.Filter("Video MP4", ".mp4"));
        StartCoroutine(BukaDialogFile(true));
    }

    // 2. Fungsi pemicu untuk memilih LAGU (.mp3)
    public void PilihFileAudio()
    {
        FileBrowser.SetFilters(true, new FileBrowser.Filter("Lagu MP3", ".mp3"));
        StartCoroutine(BukaDialogFile(false));
    }

    // Jembatan pembaca file dari windows/device
    IEnumerator BukaDialogFile(bool isVideo)
    {
        string judul = isVideo ? "Pilih Video Konser (.mp4)" : "Pilih Lagu Konser (.mp3)";
        yield return FileBrowser.WaitForLoadDialog(FileBrowser.PickMode.Files, false, null, null, judul, "Load");

        if (FileBrowser.Success)
        {
            if (isVideo)
            {
                jalurVideo = FileBrowser.Result[0];
                Debug.Log("Video terpilih: " + jalurVideo);
            }
            else
            {
                jalurAudio = FileBrowser.Result[0];
                Debug.Log("Audio terpilih: " + jalurAudio);
            }
        }
    }

    // 3. Fungsi UTAMA untuk memutar Video & Audio secara serentak
   // 3. Fungsi UTAMA untuk memutar Video & Audio secara serentak
    public void PutarKonser()
    {
        // VALIDASI: Lagu MP3 wajib diisi
        if (string.IsNullOrEmpty(jalurAudio))
        {
            Debug.LogError("Host belum memilih Lagu MP3! Lagu wajib diisi.");
            return;
        }

        // KONDISI 1: Jika Host memilih VIDEO BARU dari komputer (.mp4)
        if (!string.IsNullOrEmpty(jalurVideo))
        {
            MatikanKamera(); // Matikan webcam karena mau memutar video baru
            videoPlayer.Stop();
            audioSource.Stop();

            videoPlayer.renderMode = VideoRenderMode.RenderTexture; // Pastikan render mode aktif
            videoPlayer.source = VideoSource.Url;
            videoPlayer.url = jalurVideo;
            videoPlayer.Play();
            Debug.Log("Memutar Video Baru + MP3 Baru.");
        }
        // KONDISI 2: JIKA WEBCAM SEDANG AKTIF (Kunci total VideoPlayer agar tidak nge-glitch)
        else if (isCamActive)
        {
            audioSource.Stop(); 
            
            // PAKSA VideoPlayer ke mode APIOnly agar tidak menimpa RenderTexture dengan layar hitam/blank bawaan Windows
            videoPlayer.Stop();
            videoPlayer.renderMode = VideoRenderMode.APIOnly; 
            
            Debug.Log("Webcam AKTIF. Memaksa VideoPlayer mengalah ke mode APIOnly agar gambar Webcam aman.");
        }
        // KONDISI 3: Jika tidak ada video baru dan webcam mati (Gunakan Video Bawaan Unity)
        else
        {
            videoPlayer.Stop();
            audioSource.Stop();

            videoPlayer.renderMode = VideoRenderMode.RenderTexture; // Kembalikan render mode
            videoPlayer.source = VideoSource.VideoClip; 
            videoPlayer.Play();
            Debug.Log("Menggunakan Video Bawaan Unity karena Host tidak memilih video baru / webcam mati.");
        }

        // Selalu muat dan putar file MP3 eksternal di akhir
        StartCoroutine(MuatDanPutarAudio(jalurAudio));
    }

    IEnumerator MuatDanPutarAudio(string path)
    {
        // Format path lokal komputer agar bisa dibaca WebRequest
        string urlAudio = "file://" + path;

        using (UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip(urlAudio, AudioType.MPEG))
        {
            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.ConnectionError || www.result == UnityWebRequest.Result.ProtocolError)
            {
                Debug.LogError("Gagal memuat MP3: " + www.error);
            }
            else
            {
                // Ambil hasil konversi file MP3 dari komputer menjadi clip Unity
                AudioClip audioClip = DownloadHandlerAudioClip.GetContent(www);
                audioSource.clip = audioClip;

                // MAIN KAN BERSAMAAN!
                videoPlayer.Play();
                audioSource.Play();
                Debug.Log("Konser Berjalan! Video dan MP3 sinkron.");
            }
        }
    }

// ================= FUNGSI FIX: WEBCAM DAN CLEAR VIDEO LAMA =================
    public void ToggleKameraKomputer()
    {
        if (isCamActive)
        {
            MatikanKamera();
        }
        else
        {
            // 1. Hentikan total Video Player dan Audio
            videoPlayer.Stop();
            audioSource.Stop();

            // 2. AMBIL DAN BERSIHKAN RENDER TEXTURE (Hapus sisa video lama yang membeku)
            RenderTexture rt = videoPlayer.targetTexture;
            if (rt != null)
            {
                RenderTexture activeBefore = RenderTexture.active;
                RenderTexture.active = rt;
                GL.Clear(true, true, Color.black); // Paksa layar jadi hitam bersih total
                RenderTexture.active = activeBefore;
            }
            else
            {
                Debug.LogError("Kolom Target Texture pada Video Player Anda KOSONG!");
                return;
            }

            if (WebCamTexture.devices.Length == 0)
            {
                Debug.LogError("Tidak ada kamera/webcam yang terdeteksi di komputer Host!");
                return;
            }

            // 3. Nyalakan Webcam
            webcamTexture = new WebCamTexture(WebCamTexture.devices[0].name, 1280, 720, 30);
            webcamTexture.Play();
            isCamActive = true;

            // 4. Mulai salin gambar webcam ke Render Texture yang sudah bersih
            StartCoroutine(SalinWebcamKeRenderTexture());
            Debug.Log("Live Cam Host Berhasil Ditampilkan di Panggung Tanpa Tumpukan Video!");
        }
    }

    // Coroutine untuk menyalin gambar webcam (Tetap sama seperti sebelumnya)
   // Coroutine untuk menyalin gambar webcam dengan fitur MIRRORING (Dibalik horizontal)
    IEnumerator SalinWebcamKeRenderTexture()
    {
        RenderTexture rt = videoPlayer.targetTexture;
        while (isCamActive)
        {
            if (webcamTexture != null && webcamTexture.didUpdateThisFrame)
            {
                // Menggunakan Vector2(-1, 1) dan offset (1, 0) untuk membalik gambar secara horizontal (Mirroring)
                Vector2 scale = new Vector2(-1, 1);
                Vector2 offset = new Vector2(1, 0);
                
                Graphics.Blit(webcamTexture, rt, scale, offset);
            }
            yield return null;
        }
    }

    private void MatikanKamera()
    {
        isCamActive = false;

        if (webcamTexture != null)
        {
            if (webcamTexture.isPlaying) webcamTexture.Stop();
            webcamTexture = null;
        }

        // Saat webcam dimatikan, bersihkan layar sekali lagi agar bersih sebelum video baru diputar
        if (videoPlayer != null)
        {
            RenderTexture rt = videoPlayer.targetTexture;
            if (rt != null)
            {
                RenderTexture activeBefore = RenderTexture.active;
                RenderTexture.active = rt;
                GL.Clear(true, true, Color.black);
                RenderTexture.active = activeBefore;
            }
            videoPlayer.renderMode = VideoRenderMode.RenderTexture;
        }
    }

    // Pastikan kamera mati saat game di-close agar tidak crash
    void OnDisable()
    {
        MatikanKamera();
    }
}