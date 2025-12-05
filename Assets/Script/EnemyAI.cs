using UnityEngine;
using UnityEngine.AI; // Wajib untuk NavMesh
using System; // Wajib untuk System.Action (Events)

[RequireComponent(typeof(NavMeshAgent))]
public class EnemyAI : MonoBehaviour
{
    // === SISTEM ALERT (KOOPERATIF) ===
    public static event Action<Vector3> OnPlayerSpotted;

    // === FSM (TUGAS 1) ===
    public enum State
    {
        Patrol,      // Berjalan santai antar waypoint
        Investigate, // Mencari di sekitar titik terakhir
        Chase        // Melihat player, mengejar
    }
    public State currentState;

    [Header("Referensi Komponen")]
    public Transform player;
    public Transform eyePosition; // Titik "mata" / "senter" AI
    private NavMeshAgent agent;

    [Header("Settings Patroli")]
    public Transform[] patrolPoints;
    private int currentPatrolIndex;

    [Header("Settings Deteksi (Senter)")]
    public float viewDistance = 20f; // Jarak pandang maksimum
    [Range(0, 180)]
    public float viewAngle = 90f;  // Sudut pandang (lebar senter)
    public LayerMask obstacleMask; // Set ke Layer "Obstacle" di Inspector
    
    [Header("Settings Pengejaran")]
    public float timeToInvestigate = 8f; 
    public float searchRadius = 5f; 
    public float chaseGraceTime = 2.0f; 
    private float investigateTimer;
    private float graceTimer; 
    private Vector3 lastKnownPlayerPosition;

    [Header("Settings Visual Model")]
    public GameObject normalGhostModel; // Drag model hantu biasa ke sini
    public GameObject strongGhostModel; // Drag model hantu kuat ke sini

    [Header("Settings Jarak Dekat (Proximity)")]
    public float proximityRadius = 3.0f; // Radius 360 derajat

    // === COOP MOVEMENT ===
    [Header("Settings Coop (Taktikal)")]
    [Tooltip("0 = Kejar lurus. 2 = Agak ke kanan. -2 = Agak ke kiri.")]
    public float flankOffset = 0f;

    // --- Manajemen Event (Mendengar Teman) ---
    void OnEnable()
    {
        OnPlayerSpotted += HandlePlayerSpotted;
    }

    void OnDisable()
    {
        OnPlayerSpotted -= HandlePlayerSpotted;
    }

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();

        // Otomatis cari Player jika belum di-set
        if (player == null)
        {
            try {
                player = GameObject.FindGameObjectWithTag("Player").transform;
            } catch {
                Debug.LogError("Player tidak ditemukan! Pastikan Player punya Tag 'Player'");
            }
        }

        // === PROBABILITY & MODEL SWITCHING (TUGAS 3) ===
        
        // Pastikan semua model mati dulu di awal untuk reset
        if(normalGhostModel != null) normalGhostModel.SetActive(false);
        if(strongGhostModel != null) strongGhostModel.SetActive(false);

        // Lempar dadu (0-100)
        int roll = UnityEngine.Random.Range(0, 100);

        if (roll < 30) // 30% Peluang Hantu KUAT
        {
            // Set Stats
            agent.speed = 5.5f; // Lebih Cepat
            agent.acceleration = 12f;
            
            // Set Model KUAT (Nyala)
            if (strongGhostModel != null) 
            {
                strongGhostModel.SetActive(true);
            }
            
            // (Opsional) Ubah nama biar jelas di debug
            gameObject.name = "Enemy_Strong";
        }
        else // 70% Peluang Hantu BIASA
        {
            // Set Stats
            agent.speed = 3.5f; // Normal
            
            // Set Model BIASA (Nyala)
            if (normalGhostModel != null) 
            {
                normalGhostModel.SetActive(true);
            }
            
            gameObject.name = "Enemy_Normal";
        }

        // Mulai FSM di state Patrol
        currentState = State.Patrol;
        GotoNextPatrolPoint();
    }

    void Update()
    {
        switch (currentState)
        {
            case State.Patrol:
                PatrolUpdate();
                break;
            case State.Investigate:
                InvestigateUpdate();
                break;
            case State.Chase:
                ChaseUpdate();
                break;
        }
    }

    // --- LOGIKA FSM STATE ---

    void PatrolUpdate()
    {
        if (!agent.pathPending && agent.remainingDistance < 0.5f)
        {
            GotoNextPatrolPoint(); // Pergi ke titik patroli random berikutnya
        }

        if (CanSeePlayer())
        {
            TransitionToState(State.Chase);
        }
    }

    // BARU: Logika Investigasi di-upgrade
    void InvestigateUpdate()
    {
        // 1. Timer utama investigasi tetap berjalan
        investigateTimer -= Time.deltaTime;
        if (investigateTimer <= 0)
        {
            // Waktu habis, menyerah dan kembali patroli
            TransitionToState(State.Patrol);
            return;
        }

        // 2. Prioritas tertinggi: Apakah player terlihat lagi?
        if (CanSeePlayer())
        {
            TransitionToState(State.Chase);
            return;
        }

        // 3. BARU: "Search Around"
        // Jika sudah sampai di titik pencarian, cari titik acak baru di sekitarnya
        if (!agent.pathPending && agent.remainingDistance < 0.5f)
        {
            Vector3 randomSearchPoint = GetRandomNavMeshPoint(lastKnownPlayerPosition, searchRadius);
            agent.SetDestination(randomSearchPoint);
        }
    }

    // BARU: Logika Chase di-upgrade
    void ChaseUpdate()
    {
        // 1. Sensor: Apakah masih melihat Player?
        if (CanSeePlayer())
        {
            // Jika Ya: Reset grace timer dan terus update lokasi terakhir player
            graceTimer = chaseGraceTime;
            lastKnownPlayerPosition = player.position; // Selalu update posisi
        }
        else
        {
            // Jika Tidak (Player baru saja hilang di tikungan):
            // Mulai hitung mundur "Grace Period"
            graceTimer -= Time.deltaTime;
            if (graceTimer <= 0)
            {
                // Waktu grace period habis, baru menyerah ke Investigasi
                TransitionToState(State.Investigate);
                return; // Keluar dari fungsi
            }
        }

        // 2. === SIMPLE FOLLOW (TUGAS 4) & COOP MOVEMENT (TUGAS 2) ===
        Vector3 targetPosition = lastKnownPlayerPosition + (player.right * flankOffset);
        
        // === NAVMESH (TUGAS 5) ===
        agent.SetDestination(targetPosition);
    }

    // ==========================================================
    // --- FUNGSI UTAMA DETEKSI (UPDATE TERBARU) ---
    // ==========================================================
    bool CanSeePlayer()
    {
        if (player == null) return false;

        float distanceToPlayer = Vector3.Distance(eyePosition.position, player.position);
        Vector3 directionToPlayer = (player.position - eyePosition.position).normalized;

        // ----------------------------------------------------------
        // CEK 1: PROXIMITY (JARAK DEKAT 360 DERAJAT)
        // ----------------------------------------------------------
        // Jika player sangat dekat (kurang dari proximityRadius),
        // Abaikan sudut pandang (Angle). Musuh bisa "merasakan" di belakangnya.
        if (distanceToPlayer < proximityRadius)
        {
            // Tetap cek Raycast agar tidak tembus tembok
            if (!Physics.Raycast(eyePosition.position, directionToPlayer, distanceToPlayer, obstacleMask))
            {
                return true; // KETAHUAN (Karena terlalu dekat!)
            }
        }

        // ----------------------------------------------------------
        // CEK 2: PENGLIHATAN SENTER (JARAK JAUH)
        // ----------------------------------------------------------
        if (distanceToPlayer < viewDistance)
        {
            float angle = Vector3.Angle(eyePosition.forward, directionToPlayer);
            
            // Harus masuk dalam kerucut senter
            if (angle < viewAngle / 2)
            {
                // Cek Raycast (Tembok)
                if (!Physics.Raycast(eyePosition.position, directionToPlayer, distanceToPlayer, obstacleMask))
                {
                    return true; // KETAHUAN (Karena terlihat senter!)
                }
            }
        }

        return false;
    }

    // --- FUNGSI HELPER LAINNYA ---

    void TransitionToState(State newState)
    {
        // Logika "Teriakan" Kooperatif
        if (newState == State.Chase && currentState != State.Chase)
        {
            OnPlayerSpotted?.Invoke(player.position);
            Debug.Log(gameObject.name + " MELIHAT PLAYER! MEMBERI TAHU TEMAN!");
        }
        
        currentState = newState;
        
        switch (currentState)
        {
            case State.Patrol:
                agent.speed = 3.5f;
                GotoNextPatrolPoint(); // Ambil titik patroli random
                break;
                
            case State.Investigate:
                agent.speed = 4.5f; 
                agent.SetDestination(lastKnownPlayerPosition); // Pergi ke lokasi terakhir dulu
                investigateTimer = timeToInvestigate; // Reset timer investigasi
                break;

            case State.Chase:
                agent.speed = 6f; 
                graceTimer = chaseGraceTime; // BARU: Set grace timer saat pertama kali mengejar
                lastKnownPlayerPosition = player.position; // BARU: Catat lokasi
                break;
        }
    }
    
    // --- FUNGSI "TELINGA" AI ---
    void HandlePlayerSpotted(Vector3 playerLocation)
    {
        if (currentState == State.Patrol)
        {
            Debug.Log(gameObject.name + " mendengar teriakan, ikut investigasi!");
            lastKnownPlayerPosition = playerLocation;
            TransitionToState(State.Investigate);
        }
    }

    // Logika Patroli di-upgrade
    void GotoNextPatrolPoint()
    {
        if (patrolPoints.Length == 0) return;

        // Hanya 1 titik? Langsung tuju
        if (patrolPoints.Length == 1)
        {
            agent.SetDestination(patrolPoints[0].position);
            return;
        }

        // Pilih index baru, pastikan BEDA dari index sekarang
        int newPatrolIndex = currentPatrolIndex;
        while (newPatrolIndex == currentPatrolIndex)
        {
            newPatrolIndex = UnityEngine.Random.Range(0, patrolPoints.Length);
        }
        
        currentPatrolIndex = newPatrolIndex;
        agent.SetDestination(patrolPoints[currentPatrolIndex].position);
    }

    // Helper untuk "Search Around"
    Vector3 GetRandomNavMeshPoint(Vector3 center, float radius)
    {
        // Dapatkan titik acak di dalam bola dengan radius 'radius'
        Vector3 randomDirection = UnityEngine.Random.insideUnitSphere * radius;
        randomDirection += center;
        
        NavMeshHit hit;
        // Temukan titik terdekat di NavMesh dari titik acak tadi
        if (NavMesh.SamplePosition(randomDirection, out hit, radius, NavMesh.AllAreas))
        {
            return hit.position;
        }
        
        // Gagal? Kembali ke tengah (lokasi terakhir player)
        return center;
    }

    // --- DEBUGGING (SANGAT BERGUNA) ---
    void OnDrawGizmosSelected()
    {
        if (eyePosition == null) return;

        // 1. Visualisasi kerucut pandang (Kuning)
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(eyePosition.position, viewDistance);

        Vector3 fovLine1 = Quaternion.AngleAxis(viewAngle / 2, eyePosition.up) * eyePosition.forward * viewDistance;
        Vector3 fovLine2 = Quaternion.AngleAxis(-viewAngle / 2, eyePosition.up) * eyePosition.forward * viewDistance;

        Gizmos.color = Color.blue;
        Gizmos.DrawRay(eyePosition.position, fovLine1);
        Gizmos.DrawRay(eyePosition.position, fovLine2);

        // 2. Visualisasi Proximity / Jarak Dekat (Oranye)
        Gizmos.color = new Color(1f, 0.5f, 0f); // Warna Oranye
        Gizmos.DrawWireSphere(eyePosition.position, proximityRadius);

        // Visualisasi jika melihat player
        if (CanSeePlayer())
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(eyePosition.position, player.position);
        }
        
        // Visualisasi status investigasi
        if (currentState == State.Investigate)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(lastKnownPlayerPosition, searchRadius);
        }
    }
}