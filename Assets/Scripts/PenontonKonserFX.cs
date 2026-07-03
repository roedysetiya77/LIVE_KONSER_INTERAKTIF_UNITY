using UnityEngine;
using System.Collections;
using UnityEngine.AI;

public class PenontonKonserFX : MonoBehaviour
{
    private Vector3 posisiBaseAwal;
    private Quaternion rotasiBaseAwal;
    private Animator animator;
    private NavMeshAgent navMeshAgent;
    private bool sedangAksi = false;

    void Awake()
    {
        animator = GetComponent<Animator>();
        navMeshAgent = GetComponent<NavMeshAgent>();
    }

    public void TerimaGift(int jumlahKoin, Transform targetHost)
    {
        // Log deteksi spam lock
        if (sedangAksi) 
        {
            Debug.LogWarning($"<color=yellow><b>[{gameObject.name} BLOCKED]</b></color> Mengabaikan gift karena penonton ini MASIH BERAKSI terbang/dansa saat ini.");
            return; 
        }

        posisiBaseAwal = transform.position;
        rotasiBaseAwal = transform.rotation;

        if (targetHost == null)
        {
            Debug.LogError($"<color=red><b>[{gameObject.name} ERROR]</b></color> TargetHost bernilai null! Pergerakan dibatalkan.");
            return;
        }

        Debug.Log($"<color=lime><b>[{gameObject.name} START AKSI]</b></color> Koin valid ({jumlahKoin}). Memulai Coroutine pergerakan...");

        if (jumlahKoin >= 1 && jumlahKoin <= 3)
        {
            StartCoroutine(ProsesTerbangDanPutar(jumlahKoin, targetHost));
        }
        else if (jumlahKoin >= 4)
        {
            StartCoroutine(ProsesDansaDiPanggung(targetHost));
        }
    }

    IEnumerator ProsesTerbangDanPutar(int jumlahPutaran, Transform targetHost)
    {
        sedangAksi = true;
        Debug.Log($"<color=white><b>[{gameObject.name} FLYING]</b></color> Fase 1: Lepas landas terbang ke atas Host.");

        if (navMeshAgent != null) 
        {
            navMeshAgent.enabled = false;
            Debug.Log($"<i>[{gameObject.name} INFO] NavMeshAgent dimatikan agar bisa terbang.</i>");
        }

        if (animator != null) animator.CrossFadeInFixedTime("Idle", 0.2f);

        // KODE BARU (Misal diturunkan jadi 2 meter):
        Vector3 posisiAtasHost = targetHost.position + new Vector3(0, 1.0f, 0);
        float durasiTerbang = 1.5f;
        float timer = 0f;
        Vector3 posisiStart = transform.position;

        while (timer < durasiTerbang)
        {
            timer += Time.deltaTime;
            transform.position = Vector3.Lerp(posisiStart, posisiAtasHost, timer / durasiTerbang);
            yield return null;
        }

        Debug.Log($"<color=white><b>[{gameObject.name} ORBIT]</b></color> Fase 2: Berputar mengelilingi Host sebanyak {jumlahPutaran} kali.");

        float kecepatanPutar = 50f;
        float totalDerajatTarget = jumlahPutaran * 360f;
        float derajatTercapai = 0f;
        float radius = 2.5f;

        while (derajatTercapai < totalDerajatTarget)
        {
            float tambahDerajat = kecepatanPutar * Time.deltaTime;
            derajatTercapai += tambahDerajat;

            float radian = derajatTercapai * Mathf.Deg2Rad;
            Vector3 offsetPosisi = new Vector3(Mathf.Sin(radian) * radius, 0, Mathf.Cos(radian) * radius);
            
            transform.position = posisiAtasHost + offsetPosisi;
            transform.LookAt(targetHost.position + new Vector3(0, 1.0f, 0));
            yield return null;
        }

        Debug.Log($"<color=white><b>[{gameObject.name} RETURNING]</b></color> Fase 3: Kembali pulang ke posisi koordinat asal: {posisiBaseAwal}");

        timer = 0f;
        posisiStart = transform.position;
        Quaternion rotasiStart = transform.rotation;

        while (timer < durasiTerbang)
        {
            timer += Time.deltaTime;
            float progress = timer / durasiTerbang;
            transform.position = Vector3.Lerp(posisiStart, posisiBaseAwal, progress);
            transform.rotation = Quaternion.Slerp(rotasiStart, rotasiBaseAwal, progress);
            yield return null;
        }

        transform.position = posisiBaseAwal;
        transform.rotation = rotasiBaseAwal;

        if (navMeshAgent != null) 
        {
            navMeshAgent.enabled = true;
            Debug.Log($"<i>[{gameObject.name} INFO] NavMeshAgent diaktifkan kembali di lantai.</i>");
        }

        Debug.Log($"<color=lime><b>[{gameObject.name} FINISH]</b></color> Seluruh aksi terbang SELESAI, status dialihkan ke IDLE.");
        sedangAksi = false;
    }

    IEnumerator ProsesDansaDiPanggung(Transform targetHost)
    {
        sedangAksi = true;
        Debug.Log($"<color=white><b>[{gameObject.name} TELEPORT]</b></color> Berteleportasi menuju panggung utama samping Host.");

        if (navMeshAgent != null) navMeshAgent.enabled = false;

        Vector3 posisiSampingHost = targetHost.position + new Vector3(2.0f, 0f, 0f);
        float durasiJalan = 1.0f;
        float timer = 0f;
        Vector3 posisiStart = transform.position;

        while (timer < durasiJalan)
        {
            timer += Time.deltaTime;
            transform.position = Vector3.Lerp(posisiStart, posisiSampingHost, timer / durasiJalan);
            yield return null;
        }

        transform.position = posisiSampingHost;
        transform.rotation = targetHost.rotation;

        Debug.Log($"<color=white><b>[{gameObject.name} DANCING]</b></color> Memulai urutan animasi tari otomatis.");
        if (animator != null)
        {
            string[] daftarDansa = { "PicuDance01", "PicuDance02", "PicuDance03" };
            foreach (string namaTrigger in daftarDansa)
            {
                animator.SetTrigger(namaTrigger);
                yield return new WaitForSeconds(4.0f); 
            }
        }

        Debug.Log($"<color=white><b>[{gameObject.name} RETURNING]</b></color> Kembali dari panggung ke koordinat awal.");
        timer = 0f;
        posisiStart = transform.position;
        Quaternion rotasiStart = transform.rotation;

        if (animator != null) animator.CrossFadeInFixedTime("Idle", 0.2f);

        while (timer < durasiJalan)
        {
            timer += Time.deltaTime;
            float progress = timer / durasiJalan;
            transform.position = Vector3.Lerp(posisiStart, posisiBaseAwal, progress);
            transform.rotation = Quaternion.Slerp(rotasiStart, rotasiBaseAwal, progress);
            yield return null;
        }

        transform.position = posisiBaseAwal;
        transform.rotation = rotasiBaseAwal;

        if (navMeshAgent != null) navMeshAgent.enabled = true;
        sedangAksi = false;
    }
}