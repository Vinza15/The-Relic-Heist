using UnityEngine;

public class KeyPickup : MonoBehaviour
{
    [Header("Animation")]
    public float rotateSpeed = 100f; // Agar kunci berputar

    void Update()
    {
        // Efek visual: Memutar kunci agar terlihat melayang
        transform.Rotate(Vector3.up * rotateSpeed * Time.deltaTime);
    }

    void OnTriggerEnter(Collider other)
    {
        // Cek apakah yang menabrak adalah Player
        if (other.CompareTag("Player"))
        {
            // Panggil GameManager untuk tambah skor
            GameManager.instance.AddKey();

            // Efek suara (Opsional, jika ada AudioSource)
            // AudioSource.PlayClipAtPoint(pickupSound, transform.position);

            // Hancurkan objek kunci ini
            Destroy(gameObject);
        }
    }
}