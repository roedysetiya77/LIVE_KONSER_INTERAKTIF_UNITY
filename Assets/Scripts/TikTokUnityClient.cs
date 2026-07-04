using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using SocketIOClient;
using UnityEngine.InputSystem; 
using TMPro; 

// ==========================================
// DATA STRUKTUR UNTUK MEMBACA JSON (BARU)
// ==========================================
[System.Serializable]
public class UnityTikTokPacket
{
    public string @event;   
    public string username; 
    public string detail;   
    public int amount;      
}

public class TikTokUnityClient : MonoBehaviour
{
    private SocketIOUnity socket;
    private Dictionary<string, GameObject> viewersMapDiUnity = new Dictionary<string, GameObject>();

    [Header("Pengaturan Server Node.js")]
    public string serverUrl = "https://game-tiktok.onrender.com";
    
    [HideInInspector] 
    public string tiktokUsername = ""; 
    [HideInInspector]
    public string tiktokPin = ""; 

    [Header("Referensi UI Input Manual")]
    public TMP_InputField usernameInputField; 
    public TMP_InputField pinInputField; 
    public UnityEngine.UI.Button connectButton;     
    public GameObject JudulGame; 
    public GameObject Footer;    

    [Header("Referensi Aktor Utama (Host)")]
    public Animator hostAnimator;

    [Header("Proteksi Spam Like Penonton")]
    public float likeCooldown = 0.5f; 
    private float nextLikeTime = 0f; 

    [Header("🎥 MULTI-MODE DRONE SINEMATIK (3 MODE AUTOMATIC)")]
    public Camera mainCamera;
    public float jedaGantiModeKamera = 15f;
    public float lebarAyunan = 10f; 
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
    public float kecepatanDroneMode3 = 0.12f; 
    public float tinggiDroneModeBelakang = 1.7f; 
    public float posisiZModeBelakang = 0.5f; 

    [Header("🕹️ PENGATURAN MANUAL MOUSE (SAAT DRONE OFF)")]
    public float kecepatanRotasiMouse = 0.2f;
    public float kecepatanZoomMouse = 5.0f;
    private float rotasiX = 0f;
    private float rotasiY = 0f;

    private bool isDroneCamOn = true;
    private Vector3 fokusKeHost = new Vector3(0.3f, 1.4f, -1.5f);
    private Vector3 fokusKePenonton = new Vector3(0.3f, 1.1f, -3.5f);
    private int modeKameraAktif = 0;
    private float timerGantiMode = 0f;
    private Vector3 velocityKamera = Vector3.zero;
    private float waktuRedam = 0.45f; 

    private readonly Queue<Action> mainThreadActions = new Queue<Action>();
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
            Vector3 rotasiAwal = mainCamera.transform.localRotation.eulerAngles;
            rotasiX = rotasiAwal.y;
            rotasiY = rotasiAwal.x;
        }
    }

    void Update()
    {
        lock (mainThreadActions)
        {
            while (mainThreadActions.Count > 0)
            {
                mainThreadActions.Dequeue().Invoke();
            }
        }

        if (mainCamera != null)
        {
            if (isDroneCamOn)
            {
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

            for (int i = daftarTeksPenonton.Count - 1; i >= 0; i--)
            {
                if (daftarTeksPenonton[i] != null)
                    daftarTeksPenonton[i].transform.rotation = mainCamera.transform.rotation;
                else
                    daftarTeksPenonton.RemoveAt(i); 
            }
        }

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
            Debug.Log("🕹️ DroneCam dinonaktifkan: Gunakan klik kanan mouse untuk geser cam!");
        }
    }

    public void MulaiKoneksiManual()
    {
        if (usernameInputField == null || string.IsNullOrEmpty(usernameInputField.text))
        {
            Debug.LogError("❌ Username TikTok tidak boleh kosong!");
            return;
        }

        tiktokUsername = usernameInputField.text.Trim();
        tiktokPin = pinInputField != null ? pinInputField.text.Trim() : ""; 
        
        SetupAndConnectSocket(); 
    }

    private void SetupAndConnectSocket()
    {
        string urlBersih = serverUrl.Trim();

        #if UNITY_WEBGL && !UNITY_EDITOR
        Debug.Log($"🔌 [WEBGL BRIDGE] Mengirim perintah koneksi mandiri untuk Room: {tiktokUsername}");
        try 
        {
            string dataPaketKoneksi = tiktokUsername + "," + tiktokPin;
            Application.ExternalCall("HubungkanSocketDariUnity", dataPaketKoneksi);
        } 
        catch (System.Exception ex) 
        {
            Debug.LogError($"❌ [BRIDGE ERROR] Gagal memanggil jembatan browser: {ex.Message}");
        }
        #else
        string alamatSecure = urlBersih.Replace("https://", "wss://").Replace("http://", "ws://");
        var uri = new Uri(alamatSecure);
        
        socket = new SocketIOUnity(uri, new SocketIOOptions
        {
            Query = new Dictionary<string, string> { { "room", tiktokUsername } },
            EIO = EngineIO.V4 
        });

        socket.OnConnected += (sender, e) => {
            EnqueueAction(() => {
                SinyalKoneksiSuksesDariBrowser("Terhubung via Editor!");
            });
            var dataEmitPC = new { username = tiktokUsername, pin = tiktokPin, room = tiktokUsername };
            socket.EmitAsync("connect-tiktok", dataEmitPC);
        };

        socket.On("tiktok-to-unity", response => {
            EnqueueAction(() => {
                if (response == null) return;
                try 
                {
                    string jsonString = response.GetValue<string>(0);
                    CoretaHadiahHandler(jsonString);
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[SOCKET BACKUP READ] Error: {ex.Message}");
                    string backupString = response.ToString();
                    CoretaHadiahHandler(backupString);
                }
            });
        });

        socket.On("tiktok-action", response => {
            EnqueueAction(() => {
                if (response == null) return;
                
                string jsonString = "";
                try 
                {
                    object dataMentah = response.GetValue<object>(0);
                    if (dataMentah == null) return;

                    if (dataMentah.GetType().ToString().Contains("JsonElement"))
                    {
                        jsonString = dataMentah.ToString();
                    }
                    else if (dataMentah is string textBiasa)
                    {
                        jsonString = textBiasa;
                    }
                    else
                    {
                        jsonString = dataMentah.ToString();
                    }

                    jsonString = jsonString.Trim();
                    if (jsonString.StartsWith("[")) jsonString = jsonString.Substring(1);
                    if (jsonString.EndsWith("]")) jsonString = jsonString.Substring(0, jsonString.Length - 1);

                    CoretaHadiahHandler(jsonString.Trim());
                }
                catch (System.Exception ex)
                    {
                    Debug.LogWarning($"[SOCKET EMERGENCY READ] Error: {ex.Message}");
                    string backupString = response.ToString().Trim();
                    if (backupString.StartsWith("[") && backupString.EndsWith("]")) 
                    backupString = backupString.Substring(1, backupString.Length - 2);
    
                    CoretaHadiahHandler(backupString);
                    }
            });
        });

        socket.OnAny((eventName, response) => {
            if (eventName == "connect_error" || eventName == "error" || eventName == "disconnect")
            {
                Debug.LogError($"❌ [SOCKET ALERT] Event Kegagalan: {eventName}");
            }
        });

        Debug.Log("🌐 [SOCKET] Memulai koneksi di Unity Editor...");
        socket.Connect();
        #endif
    }

    public void TerimaDataTikTokDariBrowser(string jsonMentah)
    {
        if (string.IsNullOrEmpty(jsonMentah)) return;

        EnqueueAction(() => {
            try 
            {
                CoretaHadiahHandler(jsonMentah.Trim());
            } 
            catch (System.Exception ex) 
            {
                Debug.LogError($"❌ [BRIDGE CORRUPT] Gagal memproses data browser: {ex.Message}");
            }
        });
    }

    public void SinyalKoneksiSuksesDariBrowser(string pesan)
    {
        Debug.Log($"<color=green><b>🔌 [BRIDGE CONNECTED]:</b> {pesan}</color>");
        
        if (usernameInputField != null) usernameInputField.gameObject.SetActive(false);
        if (pinInputField != null) pinInputField.gameObject.SetActive(false); 
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
    }

    private void EnqueueAction(Action action) { lock (mainThreadActions) { mainThreadActions.Enqueue(action); } }


    // Tambahkan fungsi ini di dalam script TikTokUnityClient.cs Anda
public void MatikanInputPINManual()
{
    if (pinInputField != null)
    {
        pinInputField.gameObject.SetActive(false);
        Debug.Log("🔒 [WEBGL FIX] Input PIN berhasil dimatikan secara manual lewat perintah jembatan browser!");
    }
    else
    {
        // Jika ternyata slotnya kosong, kita cari paksa berdasarkan nama objeknya di Hierarchy
        GameObject targetPIN = GameObject.Find("InputPIN");
        if (targetPIN != null)
        {
            targetPIN.SetActive(false);
            Debug.Log("🔒 [WEBGL FIX] Input PIN ditemukan secara paksa di Hierarchy dan berhasil dimatikan!");
        }
    }
}


    private void CoretaHadiahHandler(string jsonData)
    {
        try
        {
            string jsonBersih = jsonData.Trim();
            if (jsonBersih.StartsWith("[")) jsonBersih = jsonBersih.Substring(1);
            if (jsonBersih.EndsWith("]")) jsonBersih = jsonBersih.Substring(0, jsonBersih.Length - 1);
            jsonBersih = jsonBersih.Trim();

            UnityTikTokPacket packet = JsonUtility.FromJson<UnityTikTokPacket>(jsonBersih);
            if (packet == null || string.IsNullOrEmpty(packet.@event)) return;

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
                }
            };

            if (packet.@event == "join")
            {
                MelahirkanPenontonLokal();
            }
            else if (packet.@event == "like")
            {
                if (Time.time >= nextLikeTime)
                {
                    nextLikeTime = Time.time + likeCooldown;
                    GameObject penontonTarget = viewersMapDiUnity.ContainsKey(packet.username) ? viewersMapDiUnity[packet.username] : null;

                    if (penontonTarget != null)
                    {
                        PenontonKonserFX fxScript = penontonTarget.GetComponent<PenontonKonserFX>();
                        if (fxScript == null) fxScript = penontonTarget.AddComponent<PenontonKonserFX>();

                        if (fxScript.sedangAksi) return; 

                        Animator penontonAnim = penontonTarget.GetComponent<Animator>();
                        if (penontonAnim != null) 
                        {
                            int angkaRandom = UnityEngine.Random.Range(1, 4); 
                            string namaTrigger = "PicuDance0" + angkaRandom;
                            penontonAnim.SetTrigger(Animator.StringToHash(namaTrigger));
                        }
                    }
                }
            }
            else if (packet.@event == "gift")
            {
                int jumlahKoin = packet.amount;

                if (!viewersMapDiUnity.ContainsKey(packet.username))
                {
                    MelahirkanPenontonLokal();
                }

                if (viewersMapDiUnity.ContainsKey(packet.username))
                {
                    GameObject penontonGifter = viewersMapDiUnity[packet.username];
                    if (penontonGifter == null || hostAnimator == null) return;

                    PenontonKonserFX fxScript = penontonGifter.GetComponent<PenontonKonserFX>();
                    if (fxScript == null) fxScript = penontonGifter.AddComponent<PenontonKonserFX>();

                    Transform transHost = hostAnimator.transform;
                    fxScript.TerimaGift(jumlahKoin, transHost);
                }
            }
        }
        catch (System.Exception ex) 
        { 
            Debug.LogError($"❌ [ERROR HADIAH] Gagal: {ex.Message}"); 
        }
    }

    private void OnDestroy() 
    { 
        #if !UNITY_WEBGL || UNITY_EDITOR
        if (socket != null) socket.Disconnect(); 
        #endif
    }
}