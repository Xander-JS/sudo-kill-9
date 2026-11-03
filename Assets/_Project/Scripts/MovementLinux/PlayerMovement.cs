using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    private CharacterController characterController;
    private Animator animator;
    private PlayerCursorAim cursorAim;
    private IsometricCameraController isoCam;

    public new Transform camera;

    public float speed = 4f;
    public float runSpeed = 7f;
    public float gravity = 9.8f;

    private float movementSpeed;
    private float verticalVelocity;

    // Start is called before the first frame update
    void Start()
    {
        characterController = GetComponent<CharacterController>();
        animator = GetComponent<Animator>();
        cursorAim = GetComponent<PlayerCursorAim>();
        isoCam = Object.FindFirstObjectByType<IsometricCameraController>(FindObjectsInactive.Include);
    }

    // Update is called once per frame
    void Update()
    {
        float hor = Input.GetAxis("Horizontal");
        float ver = Input.GetAxis("Vertical");
        Vector3 movement = Vector3.zero;

        bool isMoving = hor != 0 || ver != 0;

        if (isMoving)
        {
            // Movimiento relativo a la dirección del cursor: W=adelante del personaje,
            // D=derecha, A=izquierda, S=atrás. La cámara sigue al cursor, así que el
            // movimiento siempre se siente consistente respecto a la pantalla.
            Vector3 forward, right;
            float aimYaw = cursorAim != null ? cursorAim.Yaw
                         : isoCam    != null ? isoCam.FixedYaw
                         : 45f;
            Quaternion aimRot = Quaternion.Euler(0f, aimYaw, 0f);
            forward = aimRot * Vector3.forward;
            right   = aimRot * Vector3.right;

            Vector3 direction = forward * ver + right * hor;
            movementSpeed = Mathf.Clamp01(direction.magnitude);
            direction.Normalize();

            bool isRunning = Input.GetKey(KeyCode.LeftShift);
            float currentSpeed = isRunning ? runSpeed : speed;

            movement = direction * currentSpeed * movementSpeed * Time.deltaTime;

            // La rotación la maneja PlayerCursorAim (apunta al cursor).
            // No sobreescribir transform.rotation aquí para que no haya conflicto.

            animator.SetBool("IsRunning", isRunning);

            // Blend Tree: 0.6=Walk (Start Walking), 1=Run (Run Forward)
            float animSpeed = isRunning ? 1f : 0.6f;
            animator.SetFloat("Speed", animSpeed);
        }
        else
        {
            movementSpeed = 0f;
            animator.SetBool("IsRunning", false);
            // Sin input → quieto (Speed=0 → Idle en el Blend Tree)
            animator.SetFloat("Speed", 0f);
        }

        // Gravity
        if (characterController.isGrounded)
        {
            verticalVelocity = -0.5f;
        }
        else
        {
            verticalVelocity -= gravity * Time.deltaTime;
        }

        movement.y = verticalVelocity * Time.deltaTime;

        characterController.Move(movement);

        // Rotar Linux para que siempre mire hacia la mira del cursor.
        if (cursorAim != null)
        {
            Vector3 aimFlat = cursorAim.AimPoint - transform.position;
            aimFlat.y = 0f;
            if (aimFlat.sqrMagnitude > 0.01f)
            {
                Quaternion targetRot = Quaternion.LookRotation(aimFlat);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, 18f * Time.deltaTime);
            }
        }
    }
}
