using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using SocketIOClient;
using UnityEngine.InputSystem; 
using TMPro; 

public class TikTokUnityClient : MonoBehaviour
{
    private SocketIOUnity socket;
    private Dictionary<string, GameObject> viewersMapDiUnity = new Dictionary<string, GameObject>();

    [Header("Pengaturan Server Node.js")]
    public string serverUrl = "https://game-tiktok.onrender.com";
    
    [HideInInspector] 
    public string tiktokUsername = ""; 

    [Header("Referensi UI Input Manual")]
    public TMP_InputField usernameInputField; 
    public UnityEngine.UI.Button connectButton;    
    public GameObject JudulGame; // Bisa diisi objek Text/UI Judul
    public GameObject Footer;    // Bisa diisi objek Text/UI Footer

    [Header("Referensi Aktor Utama (Host)")]
    public Animator hostAnimator;

    [Header("Proteksi Spam Like Penonton")]
    public float likeCooldown = 0.5f; 
    private float nextLikeTime = 0f; 

    [Header("🎥 MULTI-MODE DRONE SINEMATIK (3 MODE AUTOMATIC)")]
    [Tooltip("Masukkan Main Camera Anda dari jendela Hierarchy ke sini")]
    public Camera mainCamera;
    
    [Tooltip("Waktu (detik) sebelum kamera berganti mode otomatis")]
    public float jedaGantiModeKamera = 15f;

    [Tooltip("Lebar jangkauan drone ke kiri dan kanan (Berlaku global untuk kestabilan panggung)")]
    public float lebarAyunan = 10f; 
    
    [Tooltip("Seberapa dekat kamera nge-zoom saat mentok di ujung kiri/kanan (Mode 1 & Mode 2)")]
    public float kekuatanZoomIn = 3.5f;

    [Header("📐 SETINGAN MODE 1: MENGHADAP HOST (DARI DEPAN)")]
    public float kecepatanDroneMode1 = 0.3f;
    public float tinggiDroneModeHost = 3.5f;
    public float jarakMundurZModeHost = -14f;

    [Header("📐 SETINGAN MODE 2: MENGHADAP PENONTON (DARI ATAS HOST)")]
    public float kecepatanDroneMode2 = 0.3f;
    public float tinggiDroneModePenonton = 2.8f;
    public float posisiZModePenonton = -1.0f;

    [Header("📐 SETINGAN MODE 3: BELAKANG HOST / OTS (MELIHAT PUNGGUNG & PENONTON)")]
    [Tooltip("Kecepatan ayunan Mode 3 sengaja dibuat lambat agar wajah penonton jelas")]
    public float kecepatanDroneMode3 = 0.12f; 
    [Tooltip("Tinggi kamera sejajar pundak/kepala Host")]
    public float tinggiDroneModeBelakang = 1.7f; 
    [Tooltip("Posisi kamera mundur di belakang Host (Angka positif berarti di belakang panggung Z)")]
    public float posisiZModeBelakang = 0.5f; 

    [Header("🕹️ PENGATURAN MANUAL MOUSE (SAAT DRONE OFF)")]
    public float kecepatanRotasiMouse = 0.2f;
    public float kecepatanZoomMouse = 5.0f;
    private float rotasiX = 0f;
    private float rotasiY = 0f;

    // Status Saklar Drone Cam
    private bool isDroneCamOn = true;

    // Koordinat Target Fokus Masing-Masing Mode
    private Vector3 fokusKeHost = new Vector3(0.3f, 1.4f, -1.5f);
    private Vector3 fokusKePenonton = new Vector3(0.3f, 1.1f, -3.5f);

    // Status Mode Kamera Sekarang (0 = Menghadap Host, 1 = Menghadap Penonton, 2 = Belakang Host)
    private int modeKameraAktif = 0;
    private float timerGantiMode = 0f;

    // Variabel peredam transisi halus (Anti patah-patah)
    private Vector3 velocityKamera = Vector3.zero;
    private float waktuRedam = 0.45f; 

    private readonly Queue<Action> mainThreadActions = new Queue<Action>();

    // List internal untuk memantau semua komponen text penonton agar bisa diputar otomatis (Anti-Terbalik)
    private List<TMP_Text> daftarTeksPenonton = new List<TMP_Text>();

    void Start()
    {
        if (connectButton != null)
        {
            connectButton.onClick.AddListener(MulaiKoneksiManual);
        }
        timerGantiMode = jedaGantiModeKamera; 
        
        if (mainCamera != null)
        {
            // Ambil rotasi awal kamera sebagai titik start manual mode
            Vector3 rotasiAwal = mainCamera.transform.localRotation.eulerAngles;
            rotasiX = rotasiAwal.y;
            rotasiY = rotasiAwal.x;
        }
    }

    void Update()
    {
        // 1. Eksekusi Antrean Data dari Server
        lock (mainThreadActions)
        {
            while (mainThreadActions.Count > 0)
            {
                mainThreadActions.Dequeue().Invoke();
            }
        }

        // 2. LOGIKA KENDALIAN KAMERA (DRONE VS MANUAL MOUSE)
        if (mainCamera != null)
        {
            if (isDroneCamOn)
            {
                // ================= DRONE CAM AKTIF (OTOMATIS) =================
                timerGantiMode -= Time.deltaTime;
                if (timerGantiMode <= 0f)
                {
                    modeKameraAktif = (modeKameraAktif + 1) % 3;
                    timerGantiMode = jedaGantiModeKamera;  
                    Debug.Log($"🎥 [SUTRADARA KAMERA] Pindah ke Mode Sinematik: {modeKameraAktif}");
                }

                float targetX = 0f;
                float targetY = 0f;
                float targetZ = 0f;
                Vector3 targetMataFokus = Vector3.zero;

                if (modeKameraAktif == 0)
                {
                    float gelombangSinus = Mathf.Sin(Time.time * kecepatanDroneMode1);
                    float faktorUjungMentok = Mathf.Pow(Mathf.Abs(gelombangSinus), 4f);
                    targetX = fokusKeHost.x + (gelombangSinus * lebarAyunan);
                    targetY = tinggiDroneModeHost + Mathf.Sin(Time.time * 0.7f) * 0.4f;
                    targetZ = jarakMundurZModeHost + (faktorUjungMentok * kekuatanZoomIn);
                    targetMataFokus = fokusKeHost;
                }
                else if (modeKameraAktif == 1)
                {
                    float gelombangSinus = Mathf.Sin(Time.time * kecepatanDroneMode2);
                    float faktorUjungMentok = Mathf.Pow(Mathf.Abs(gelombangSinus), 4f);
                    targetX = fokusKePenonton.x + (gelombangSinus * lebarAyunan);
                    targetY = tinggiDroneModePenonton + Mathf.Sin(Time.time * 0.5f) * 0.2f;
                    targetZ = posisiZModePenonton - (faktorUjungMentok * kekuatanZoomIn);
                    targetMataFokus = fokusKePenonton;
                }
                else
                {
                    float gelombangSinus = Mathf.Sin(Time.time * kecepatanDroneMode3);
                    targetX = fokusKeHost.x + (gelombangSinus * (lebarAyunan * 0.4f)); 
                    targetY = tinggiDroneModeBelakang + Mathf.Sin(Time.time * 0.3f) * 0.1f; 
                    targetZ = posisiZModeBelakang;
                    targetMataFokus = fokusKePenonton;
                }

                Vector3 posisiTargetBaru = new Vector3(targetX, targetY, targetZ);
                mainCamera.transform.position = Vector3.SmoothDamp(mainCamera.transform.position, posisiTargetBaru, ref velocityKamera, waktuRedam);

                Vector3 arahKeTarget = targetMataFokus - mainCamera.transform.position;
                if (arahKeTarget != Vector3.zero)
                {
                    Quaternion rotasiFokus = Quaternion.LookRotation(arahKeTarget);
                    mainCamera.transform.rotation = Quaternion.Slerp(mainCamera.transform.rotation, rotasiFokus, Time.deltaTime * 4f);
                }
            }
            else
            {
                // ================= DRONE CAM OFF (MANUAL MOUSE) =================
                if (Mouse.current != null && Mouse.current.rightButton.isPressed)
                {
                    Vector2 deltaMouse = Mouse.current.delta.ReadValue();
                    rotasiX += deltaMouse.x * kecepatanRotasiMouse;
                    rotasiY -= deltaMouse.y * kecepatanRotasiMouse;
                    rotasiY = Mathf.Clamp(rotasiY, -20f, 80f); 

                    mainCamera.transform.rotation = Quaternion.Euler(rotasiY, rotasiX, 0f);
                }

                if (Mouse.current != null)
                {
                    float nilaiScroll = Mouse.current.scroll.ReadValue().y;
                    if (nilaiScroll != 0)
                    {
                        mainCamera.transform.Translate(0, 0, nilaiScroll * kecepatanZoomMouse * Time.deltaTime, Space.Self);
                    }
                }
            }

            // Papan nama penonton tetap menghadap lensa dalam kondisi apa pun
            for (int i = daftarTeksPenonton.Count - 1; i >= 0; i--)
            {
                if (daftarTeksPenonton[i] != null)
                    daftarTeksPenonton[i].transform.rotation = mainCamera.transform.rotation;
                else
                    daftarTeksPenonton.RemoveAt(i); 
            }
        }

        // 4. Logika Deteksi Input Keyboard Dance Host
        if (hostAnimator != null && Keyboard.current != null)
        {
            if (Keyboard.current.digit1Key.wasPressedThisFrame || Keyboard.current.numpad1Key.wasPressedThisFrame) hostAnimator.SetTrigger("PicuDance01");
            else if (Keyboard.current.digit2Key.wasPressedThisFrame || Keyboard.current.numpad2Key.wasPressedThisFrame) hostAnimator.SetTrigger("PicuDance02");
            else if (Keyboard.current.digit3Key.wasPressedThisFrame || Keyboard.current.numpad3Key.wasPressedThisFrame) hostAnimator.SetTrigger("PicuDance03");
            else if (Keyboard.current.digit4Key.wasPressedThisFrame || Keyboard.current.numpad4Key.wasPressedThisFrame) hostAnimator.SetTrigger("PicuDance04");
            else if (Keyboard.current.digit5Key.wasPressedThisFrame || Keyboard.current.numpad5Key.wasPressedThisFrame) hostAnimator.SetTrigger("PicuDance05");
            else if (Keyboard.current.digit6Key.wasPressedThisFrame || Keyboard.current.numpad6Key.wasPressedThisFrame) hostAnimator.SetTrigger("PicuGangnam");
        }
    }

    public void ToggleDroneCam(bool statusSaklar)
    {
        isDroneCamOn = statusSaklar;
        if (isDroneCamOn)
        {
            timerGantiMode = jedaGantiModeKamera; 
            Debug.Log("🎥 DroneCam diaktifkan: Mode Otomatis Berjalan Kembali.");
        }
        else
        {
            Vector3 rotasiSaatIni = mainCamera.transform.localRotation.eulerAngles;
            rotasiX = rotasiSaatIni.y;
            rotasiY = rotasiSaatIni.x;
            Debug.Log("🕹️ DroneCam dinonaktifkan: Silakan klik kanan tahan untuk geser & gunakan scroll roda mouse untuk Zoom!");
        }
    }

    public void MulaiKoneksiManual()
    {
        if (usernameInputField == null || string.IsNullOrEmpty(usernameInputField.text))
        {
            Debug.LogError("❌ Username TikTok tidak boleh kosong! Silakan ketik dulu.");
            return;
        }

        tiktokUsername = usernameInputField.text.Trim();
        SetupAndConnectSocket(); 
    }

private void SetupAndConnectSocket()
    {
        
        
        // Memastikan alamat menggunakan protokol wss:// (Secure WebSocket) khusus untuk WebGL online
        string alamatSecure = serverUrl.Replace("https://", "wss://").Replace("http://", "ws://");
        var uri = new Uri(alamatSecure);
        
        socket = new SocketIOUnity(uri, new SocketIOOptions
        {
            Query = new Dictionary<string, string> { { "room", tiktokUsername } },
            EIO = EngineIO.V4 
        });

        

        socket.OnConnected += (sender, e) => {
            EnqueueAction(() => {
                if (usernameInputField != null) usernameInputField.gameObject.SetActive(false);
                if (connectButton != null) connectButton.gameObject.SetActive(false);
                if (JudulGame != null) JudulGame.gameObject.SetActive(false);
                if (Footer != null) Footer.gameObject.SetActive(false);

                GameObject[] paraPenonton = GameObject.FindGameObjectsWithTag("Penonton");
                if (paraPenonton != null && paraPenonton.Length > 0)
                {
                    foreach (GameObject penonton in paraPenonton)
                    {
                        Animator anim = penonton.GetComponent<Animator>();
                        if (anim != null) { try { anim.CrossFadeInFixedTime("Idle", 0.2f); } catch { } }
                    }
                }
            });
            socket.EmitAsync("register-unity", "Unity_Concert_Client");
        };

        // 1. PERBAIKAN TOTAL: Membaca elemen pertama dari struktur SocketIOResponse
        // 1. PERBAIKAN TOTAL: Log diletakkan sebelum proses penyaringan data!
        socket.On("tiktok-to-unity", response => {
            EnqueueAction(() => {
                if (response == null) return;
                
                try 
                {
                    // Ambil isi teks murni dari indeks ke-0 response Socket.io
                    string jsonString = response.GetValue<string>(0);
                    
                    // ====================================================================
                    // GERBANG UTAMA: LOG INI DIJAMIN MUNCUL JIKA SERVER MENGIRIM APAPUN!
                    // ====================================================================
                    Debug.Log($"<color=purple><b>[SOCKET INCOMING RAW]:</b></color> {jsonString}");
                    // ====================================================================
                    
                    CoretaHadiahHandler(jsonString);
                }
                catch (System.Exception ex)
                {
                    string backupString = response.ToString();
                    Debug.LogWarning($"[SOCKET BACKUP READ]: {backupString}. Error: {ex.Message}");
                    CoretaHadiahHandler(backupString);
                }
            });
        });

        // ====================================================================
        // PINTO UTAMA: TIKTOK-ACTION YANG SUDAH SEPATUH DENGAN JsonElement (System.Text.Json)
        // ====================================================================
            // ====================================================================
        // PINTU UTAMA: TIKTOK-ACTION (STRUKTUR SUPER AMAN - PASTI LOLOS COMPILE)
        // ====================================================================
        socket.On("tiktok-action", response => {
            EnqueueAction(() => {
                if (response == null) return;
                
                string jsonString = "";
                try 
                {
                    // Ambil sebagai tipe data 'object' dasar agar bisa dicek null secara normal
                    object dataMentah = response.GetValue<object>(0);
                    if (dataMentah == null) return;

                    // Menggunakan string refleksi nama tipe untuk menghindari error kaku compiler C#
                    string namaTipeData = dataMentah.GetType().ToString();

                    if (namaTipeData.Contains("JsonElement"))
                    {
                        // Skenario 1: Jika library mengembalikan JsonElement (System.Text.Json)
                        // Kita konversi paksa ke string JSON murni menggunakan ToString() bawaan JsonElement
                        jsonString = dataMentah.ToString();
                        Debug.Log($"<color=yellow><b>[SOCKET JSON-ELEMENT READ]:</b></color> {jsonString}");
                    }
                    else if (dataMentah is string textBiasa)
                    {
                        // Skenario 2: Jika data terdeteksi berupa string teks biasa
                        jsonString = textBiasa;
                        Debug.Log($"<color=orange><b>[SOCKET STRING ACTION]:</b></color> {jsonString}");
                    }
                    else
                    {
                        // Skenario 3: Untuk tipe objek lainnya (misal JObject Newtonsoft)
                        jsonString = dataMentah.ToString();
                        Debug.Log($"<color=cyan><b>[SOCKET OBJECT READ]:</b></color> {jsonString}");
                    }

                    // Pembersihan karakter kurung siku penahan array jika dikirim dalam bentuk array []
                    jsonString = jsonString.Trim();
                    if (jsonString.StartsWith("[")) jsonString = jsonString.Substring(1);
                    if (jsonString.EndsWith("]")) jsonString = jsonString.Substring(0, jsonString.Length - 1);

                    CoretaHadiahHandler(jsonString.Trim());
                }
                catch (System.Exception ex)
                {
                    // Proteksi Darurat Terakhir jika pemrosesan di atas crash
                    string backupString = response.ToString().Trim();
                    if (backupString.StartsWith("[") && backupString.EndsWith("]")) 
                        backupString = backupString.Substring(1, backupString.Length - 2);
                    
                    Debug.LogWarning($"[SOCKET EMERGENCY READ]: {backupString}. Error: {ex.Message}");
                    CoretaHadiahHandler(backupString);
                }
            });
        });

        // Listener Detektif Error Koneksi
        socket.OnAny((eventName, response) => {
            if (eventName == "connect_error" || eventName == "error" || eventName == "disconnect")
            {
                Debug.LogError($"❌ [SOCKET ALERT] Event Kegagalan Terdeteksi: {eventName} | Data: {response}");
            }
        });

        // === GANTI socket.Connect(); DENGAN BLOK DI BAWAH INI ===
        
        Debug.Log("🌐 [SOCKET] Memulai prosedur koneksi aman WebGL...");
        
        #if UNITY_WEBGL && !UNITY_EDITOR
        // Menggunakan StartCoroutine atau jalankan via Task tanpa memblokir thread utama WebGL
        System.Threading.Tasks.Task.Run(async () => {
            try {
                await socket.ConnectAsync();
                Debug.Log("🌐 [SOCKET] ConnectAsync sukses dieksekusi.");
            } catch (System.Exception ex) {
                Debug.LogError($"❌ [SOCKET] Gagal ConnectAsync di WebGL: {ex.Message}");
            }
        });
        #else
        // Untuk Unity Editor tetap gunakan Connect standar bawaan library Anda
        socket.Connect();
        Debug.Log("🌐 [SOCKET] PC/Editor Connect dipicu.");
        #endif
    }
    }

   private void EnqueueAction(Action action) { lock (mainThreadActions) { mainThreadActions.Enqueue(action); } }

private void CoretaHadiahHandler(string jsonData)
    {
        try
        {
            // Pembersihan karakter kurung siku jika terdeteksi pembungkusan array mentah
            string jsonBersih = jsonData.Trim();
            if (jsonBersih.StartsWith("[")) jsonBersih = jsonBersih.Substring(1);
            if (jsonBersih.EndsWith("]")) jsonBersih = jsonBersih.Substring(0, jsonBersih.Length - 1);
            jsonBersih = jsonBersih.Trim();

            // Parsing JSON dari Node.js ke Object C# Unity
            UnityTikTokPacket packet = JsonUtility.FromJson<UnityTikTokPacket>(jsonBersih);
            if (packet == null || string.IsNullOrEmpty(packet.@event)) return;

            // ========================================================
            // FUNGSI LOKAL: Dibuat khusus agar proses Spawn bisa dipanggil berulang
            // ========================================================
            System.Action MelahirkanPenontonLokal = () => {
                if (viewersMapDiUnity.ContainsKey(packet.username)) return;
                string prefabYangDipilih = UnityEngine.Random.Range(0, 2) == 0 ? "Penonton1" : "Penonton2";
                GameObject prefabMentah = Resources.Load<GameObject>(prefabYangDipilih);

                if (prefabMentah != null)
                {
                    Vector3 posisiSpawn = new Vector3(UnityEngine.Random.Range(-4f, 4f), 0.1f, -3.05f);
                    GameObject penontonBaru = prefabYangDipilih == "Penonton2" ? 
                        Instantiate(prefabMentah, posisiSpawn, Quaternion.Euler(-11f, -15f, 5f)) : 
                        Instantiate(prefabMentah, posisiSpawn, Quaternion.identity);

                    if (prefabYangDipilih == "Penonton2") penontonBaru.transform.localScale = new Vector3(0.7f, 0.7f, 0.7f);
                    penontonBaru.name = "Penonton_" + packet.username;
                    penontonBaru.tag = "Penonton"; 

                    if (penontonBaru.GetComponent<PenontonKonserFX>() == null)
                    {
                        penontonBaru.AddComponent<PenontonKonserFX>();
                    }

                    var komponenTeks = penontonBaru.GetComponentInChildren<TMP_Text>();
                    if (komponenTeks != null)
                    {
                        komponenTeks.text = "@" + packet.username;
                        komponenTeks.color = Color.yellow; 
                        if (!daftarTeksPenonton.Contains(komponenTeks)) daftarTeksPenonton.Add(komponenTeks);
                    }
                    viewersMapDiUnity.Add(packet.username, penontonBaru);
                    Debug.Log($"<color=green>👤 <b>[SPAWN SUCCESS]</b></color> @{packet.username} berhasil masuk arena konser.");
                }
            };
            // ========================================================

            // 1. LOGIKA VIEWER JOIN -> SPAWN KARAKTER
            if (packet.@event == "join")
            {
                MelahirkanPenontonLokal();
            }
            
            // 2. LOGIKA LIKE -> BERJOGET DI TEMPAT
            else if (packet.@event == "like")
            {
                if (Time.time >= nextLikeTime)
                {
                    nextLikeTime = Time.time + likeCooldown;
                    GameObject penontonTarget = viewersMapDiUnity.ContainsKey(packet.username) ? viewersMapDiUnity[packet.username] : null;

                    if (penontonTarget != null)
                    {
                        Animator penontonAnim = penontonTarget.GetComponent<Animator>();
                        if (penontonAnim != null) penontonAnim.SetTrigger("PicuDance0" + UnityEngine.Random.Range(1, 4));
                    }
                }
            }

            // 3. LOGIKA GIFT (COIN) -> DENGAN AUTO-SPAWN JIKA USER BELUM TERDAFTAR JOIN
            else if (packet.@event == "gift")
            {
                int jumlahKoin = packet.amount;
                Debug.Log($"<color=cyan><b>[1. SOCKET RECEIVED]</b></color> Data gift terurai valid -> User: @{packet.username}, Gift: {packet.detail}, Jumlah Koin: {jumlahKoin}");

                // PENGAMAN JIKA USER BELUM SPAWN (Auto-Spawn Darurat)
                if (!viewersMapDiUnity.ContainsKey(packet.username))
                {
                    Debug.LogWarning($"<color=orange><b>[AUTO-SPAWN]</b></color> @{packet.username} melakukan Gift sebelum memicu Join. Melahirkan karakter darurat...");
                    MelahirkanPenontonLokal();
                }

                if (viewersMapDiUnity.ContainsKey(packet.username))
                {
                    GameObject penontonGifter = viewersMapDiUnity[packet.username];
                    
                    if (penontonGifter == null)
                    {
                        Debug.LogError($"<color=red><b>[ERR-A]</b></color> Username @{packet.username} terdaftar di Map, tetapi objek fisiknya Null!");
                        return;
                    }

                    if (hostAnimator == null)
                    {
                        Debug.LogError($"<color=red><b>[ERR-B]</b></color> Slot 'Host Animator' kosong di Inspector!");
                        return;
                    }

                    PenontonKonserFX fxScript = penontonGifter.GetComponent<PenontonKonserFX>();
                    if (fxScript == null)
                    {
                        fxScript = penontonGifter.AddComponent<PenontonKonserFX>();
                    }

                    Transform transHost = hostAnimator.transform;
                    Debug.Log($"<color=green><b>[2. CALLING FX]</b></color> Memicu fungsi TerimaGift() pada karakter: {penontonGifter.name}");
                    
                    fxScript.TerimaGift(jumlahKoin, transHost);
                }
            }
        }
        catch (System.Exception ex) 
        { 
            Debug.LogError($"❌ [LOG ERROR HADIAH] Gagal memproses data kado: {ex.Message}"); 
        }
    }

    private void OnDestroy() { if (socket != null) socket.Disconnect(); }
}

[System.Serializable]
public class UnityTikTokPacket
{
    public string @event;     // Memetakan "event"
    public string username;   // Memetakan "username"
    public string detail;     // Memetakan "detail" (nama gift)
    public int amount;        // Memetakan "amount" (jumlah repeat koin gift)
}