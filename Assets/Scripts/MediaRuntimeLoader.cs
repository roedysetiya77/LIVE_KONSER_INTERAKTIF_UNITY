using UnityEngine;
using UnityEngine.Video;
using UnityEngine.Networking;
using System.Collections;
using System.Runtime.InteropServices; // WAJIB untuk WebGL DLL Import

#if !UNITY_WEBGL || UNITY_EDITOR
using SimpleFileBrowser; 
#endif

public class MediaRuntimeLoader : MonoBehaviour
{
    [Header("Komponen Output")]
    public VideoPlayer videoPlayer;   
    public AudioSource audioSource;   
    public MeshRenderer layarRenderer; 

    [Header("Material Panggung")]
    public Material matLayarVideo;    

    private string jalurVideo = "";
    private string jalurAudio = "";
    private WebCamTexture webcamTexture;
    private bool isCamActive = false;

    // Menghubungkan fungsi dari file picker .jslib khusus WebGL
    #if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void TriggerWebGLFilePicker(string objectName, string methodName, string fileType);
    #endif

    // 1. Fungsi pemicu untuk memilih VIDEO (.mp4)
    public void PilihFileVideo()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        // PAKSA WEBGL: Mengunci string target langsung ke objek Sistem_Media_Manager Anda
        TriggerWebGLFilePicker("Sistem_Media_Manager", "OnVideoFileSelected", "video/mp4");
#else
        // KHUSUS DI EDITOR PC / STANDALONE
        FileBrowser.SetFilters(true, new FileBrowser.Filter("Video MP4", ".mp4"));
        StartCoroutine(BukaDialogFileLokal(true));
#endif
    }

    // 2. Fungsi pemicu untuk memilih LAGU (.mp3)
    public void PilihFileAudio()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        // PAKSA WEBGL: Mengunci string target langsung ke objek Sistem_Media_Manager Anda
        TriggerWebGLFilePicker("Sistem_Media_Manager", "OnAudioFileSelected", "audio/mpeg, audio/mp3");
#else
        // KHUSUS DI EDITOR PC / STANDALONE
        FileBrowser.SetFilters(true, new FileBrowser.Filter("Lagu MP3", ".mp3"));
        StartCoroutine(BukaDialogFileLokal(false));
#endif
    }

#if !UNITY_WEBGL || UNITY_EDITOR
    // Coroutine ini sekarang diisolasi agar TIDAK AKAN PERNAH di-compile saat di-build ke WebGL
    IEnumerator BukaDialogFileLokal(bool isVideo)
    {
        string judul = isVideo ? "Pilih Video Konser (.mp4)" : "Pilih Lagu Konser (.mp3)";
        yield return FileBrowser.WaitForLoadDialog(FileBrowser.PickMode.Files, false, null, null, judul, "Load");

        if (FileBrowser.Success)
        {
            if (isVideo)
            {
                jalurVideo = FileBrowser.Result[0];
                Debug.Log("Video terpilih (PC Editor): " + jalurVideo);
            }
            else
            {
                jalurAudio = FileBrowser.Result[0];
                Debug.Log("Audio terpilih (PC Editor): " + jalurAudio);
            }
        }
    }
#endif

    // ================= FUNGSI TANGKAPAN DARI WEBGL BROWSER =================
    public void OnVideoFileSelected(string urlData)
    {
        jalurVideo = urlData; // Berisi URL Blob asli dari Browser (blob:http://...)
        Debug.Log("Video Terpilih Berhasil (WebGL Blob): " + jalurVideo);
    }

    public void OnAudioFileSelected(string urlData)
    {
        jalurAudio = urlData; // Berisi URL Blob asli dari Browser (blob:http://...)
        Debug.Log("Audio Terpilih Berhasil (WebGL Blob): " + jalurAudio);
    }

    // 3. Fungsi UTAMA untuk memutar Video & Audio secara serentak
    public void PutarKonser()
    {
        if (string.IsNullOrEmpty(jalurAudio))
        {
            Debug.LogError("Host belum memilih Lagu MP3! Lagu wajib diisi.");
            return;
        }

        if (!string.IsNullOrEmpty(jalurVideo))
        {
            MatikanKamera(); 
            videoPlayer.Stop();
            audioSource.Stop();

            videoPlayer.renderMode = VideoRenderMode.RenderTexture; 
            videoPlayer.source = VideoSource.Url;
            videoPlayer.url = jalurVideo; // Langsung membaca URL Blob lokal web browser
            videoPlayer.Play();
            Debug.Log("Memutar Video Baru + MP3 Baru.");
        }
        else if (isCamActive)
        {
            audioSource.Stop(); 
            videoPlayer.Stop();
            videoPlayer.renderMode = VideoRenderMode.APIOnly; 
            Debug.Log("Webcam AKTIF. Memaksa VideoPlayer mengalah.");
        }
        else
        {
            videoPlayer.Stop();
            audioSource.Stop();

            videoPlayer.renderMode = VideoRenderMode.RenderTexture; 
            videoPlayer.source = VideoSource.VideoClip; 
            videoPlayer.Play();
            Debug.Log("Menggunakan Video Bawaan Unity.");
        }

        StartCoroutine(MuatDanPutarAudio(jalurAudio));
    }

    IEnumerator MuatDanPutarAudio(string path)
    {
        string urlAudio = path;

#if !UNITY_WEBGL || UNITY_EDITOR
        if (!urlAudio.StartsWith("file://")) {
            urlAudio = "file://" + path;
        }
#endif

        using (UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip(urlAudio, AudioType.MPEG))
        {
            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.ConnectionError || www.result == UnityWebRequest.Result.ProtocolError)
            {
                Debug.LogError("Gagal memuat MP3: " + www.error);
            }
            else
            {
                AudioClip audioClip = DownloadHandlerAudioClip.GetContent(www);
                audioSource.clip = audioClip;

                videoPlayer.Play();
                audioSource.Play();
                Debug.Log("Konser Berjalan! Video dan MP3 sinkron.");
            }
        }
    }

    // ================= FUNGSI WEBCAM (Tetap Dipertahankan) =================
    public void ToggleKameraKomputer()
    {
        if (isCamActive)
        {
            MatikanKamera();
        }
        else
        {
            videoPlayer.Stop();
            audioSource.Stop();

            RenderTexture rt = videoPlayer.targetTexture;
            if (rt != null)
            {
                RenderTexture activeBefore = RenderTexture.active;
                RenderTexture.active = rt;
                GL.Clear(true, true, Color.black); 
                RenderTexture.active = activeBefore;
            }
            else
            {
                Debug.LogError("Target Texture Kosong!");
                return;
            }

            if (WebCamTexture.devices.Length == 0)
            {
                Debug.LogError("Tidak ada webcam terdeteksi!");
                return;
            }

            webcamTexture = new WebCamTexture(WebCamTexture.devices[0].name, 1280, 720, 30);
            webcamTexture.Play();
            isCamActive = true;

            StartCoroutine(SalinWebcamKeRenderTexture());
            Debug.Log("Live Cam Aktif.");
        }
    }

    IEnumerator SalinWebcamKeRenderTexture()
    {
        RenderTexture rt = videoPlayer.targetTexture;
        while (isCamActive)
        {
            if (webcamTexture != null && webcamTexture.didUpdateThisFrame)
            {
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

    void OnDisable()
    {
        MatikanKamera();
    }
}