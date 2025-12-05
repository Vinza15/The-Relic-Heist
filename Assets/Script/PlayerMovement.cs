using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 6.0f;
    public float jumpSpeed = 8.0f;
    public float gravity = 20.0f;

    [Header("Mouse Look")]
    public float mouseSensitivity = 100.0f;
    public float clampAngle = 80.0f; // Batas lihat atas/bawah

    private CharacterController controller;
    private Camera mainCamera;
    private Vector3 moveDirection = Vector3.zero;
    private float rotationX = 0.0f;

    void Start()
    {
        controller = GetComponent<CharacterController>();
        mainCamera = Camera.main;

        // Sembunyikan dan kunci kursor di tengah layar
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        // --- 1. MOUSE LOOK (Lihat Kiri/Kanan & Atas/Bawah) ---
        
        // Lihat Kiri/Kanan (memutar badan Player)
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity * Time.deltaTime;
        transform.Rotate(Vector3.up * mouseX);

        // Lihat Atas/Bawah (memutar Kamera)
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity * Time.deltaTime;
        rotationX -= mouseY;
        rotationX = Mathf.Clamp(rotationX, -clampAngle, clampAngle); // Batasi sudut
        mainCamera.transform.localRotation = Quaternion.Euler(rotationX, 0, 0);

        // --- 2. MOVEMENT (WASD) ---

        // Cek jika di darat
        if (controller.isGrounded)
        {
            float moveForward = Input.GetAxis("Vertical"); // W & S
            float moveStrafe = Input.GetAxis("Horizontal"); // A & D

            // Ubah input dari lokal ke world space
            moveDirection = transform.TransformDirection(new Vector3(moveStrafe, 0, moveForward));
            moveDirection *= moveSpeed;

            if (Input.GetButton("Jump"))
            {
                moveDirection.y = jumpSpeed;
            }
        }

        // Terapkan gravitasi
        moveDirection.y -= gravity * Time.deltaTime;

        // Eksekusi gerakan
        controller.Move(moveDirection * Time.deltaTime);
    }
}