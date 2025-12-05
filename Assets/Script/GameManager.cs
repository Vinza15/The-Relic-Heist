using UnityEngine;
using TMPro; 

public class GameManager : MonoBehaviour
{
    public static GameManager instance;

    [Header("Settings")]
    public int totalKeysNeeded = 3;
    public int currentKeys = 0;

    [Header("Spawn System (BARU)")]
    public GameObject keyPrefab; // Masukkan Prefab Kunci ke sini
    public Transform[] spawnPoints; // Masukkan semua Point1, Point2, dst ke sini

    [Header("UI References")]
    public TextMeshProUGUI keyText; 
    public GameObject exitDoor; 

    void Awake()
    {
        if (instance == null) instance = this;
        else Destroy(gameObject);
    }

    void Start()
    {
        SpawnRandomKeys(); // Panggil fungsi spawn saat game mulai
        UpdateUI();
    }

    // --- FUNGSI BARU: SPAWN RANDOM ---
    void SpawnRandomKeys()
    {
        // Cek keamanan: Jangan sampai minta 3 kunci tapi cuma bikin 2 titik spawn
        if (spawnPoints.Length < totalKeysNeeded)
        {
            Debug.LogError("KURANG TITIK SPAWN! Buat lebih banyak Spawn Point.");
            return;
        }

        // 1. ALGORITMA FISHER-YATES SHUFFLE (Mengocok Array)
        // Kita acak urutan posisi spawnPoints[]
        for (int i = 0; i < spawnPoints.Length; i++)
        {
            Transform temp = spawnPoints[i];
            int randomIndex = Random.Range(i, spawnPoints.Length);
            spawnPoints[i] = spawnPoints[randomIndex];
            spawnPoints[randomIndex] = temp;
        }

        // 2. SPAWN KUNCI
        // Karena array sudah dikocok, kita cukup ambil 3 urutan pertama (index 0, 1, 2)
        // Kunci akan muncul di lokasi yang sudah teracak tersebut.
        for (int i = 0; i < totalKeysNeeded; i++)
        {
            Instantiate(keyPrefab, spawnPoints[i].position, Quaternion.identity);
        }
    }
    // ---------------------------------

    public void AddKey()
    {
        currentKeys++;
        UpdateUI();

        if (currentKeys >= totalKeysNeeded)
        {
            OpenExitDoor();
        }
    }

    void UpdateUI()
    {
        if (keyText != null)
        {
            keyText.text = "Keys: " + currentKeys + " / " + totalKeysNeeded;
        }
    }

    void OpenExitDoor()
    {
        Debug.Log("PINTU TERBUKA!");
        if (exitDoor != null)
        {
            exitDoor.SetActive(false); 
        }
    }
}