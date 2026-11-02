using UnityEngine;
using UnityEngine.InputSystem;
using SudoKill9.Combat;

/// <summary>
/// Sistema de disparo del personaje Linux (clic derecho).
/// Requiere PlayerCursorAim en el mismo GameObject para obtener el punto de mira.
///
/// Configuración mínima en el Inspector:
///   1. Asignar MuzzlePoint (Transform hijo del arma, en la punta del cañón).
///   2. Asignar HitMask excluyendo la capa del propio jugador y del arma.
///   3. (Opcional) Asignar MuzzleLight para el destello del disparo.
/// </summary>
[RequireComponent(typeof(PlayerCursorAim))]
public class PlayerShooter : MonoBehaviour
{
    [Header("Disparo")]
    [Tooltip("Transform en la punta del cañón (MuzzlePoint, hijo del arma).")]
    [SerializeField] private Transform muzzlePoint;
    [Tooltip("Cadencia de disparo (disparos por segundo).")]
    [SerializeField] private float fireRate = 5f;
    [Tooltip("Distancia máxima del Raycast.")]
    [SerializeField] private float maxRange = 80f;
    [Tooltip("Daño aplicado al objetivo por disparo.")]
    [SerializeField] private float damage = 25f;
    [Tooltip("Capas que puede alcanzar el Raycast. EXCLUIR la capa del jugador y del arma para evitar auto-impactos.")]
    [SerializeField] private LayerMask hitMask = ~0;

    [Header("Efectos visuales")]
    [Tooltip("Duración del trazador de bala (segundos).")]
    [SerializeField] private float tracerDuration = 0.06f;
    [Tooltip("Color del trazador.")]
    [SerializeField] private Color tracerColor = new Color(1f, 0.95f, 0.4f);
    [Tooltip("(Opcional) Luz para el destello del cañón. Añadir como hijo de MuzzlePoint.")]
    [SerializeField] private Light muzzleLight;
    [Tooltip("Duración del destello del cañón (segundos).")]
    [SerializeField] private float muzzleFlashDuration = 0.04f;
    [Tooltip("Radio de la marca de impacto (debug gizmo, metros).")]
    [SerializeField] private float impactGizmoRadius = 0.05f;

    // ── Referencias internas ──────────────────────────────────────────────
    private PlayerCursorAim cursorAim;
    private LineRenderer tracer;
    private float nextFireTime;
    private float muzzleLightOffTime;

    // ── Gizmo de impacto (solo para depuración en Scene view) ─────────────
    private Vector3 lastImpactPoint;
    private bool hadImpact;

    // ─────────────────────────────────────────────────────────────────────
    private void Awake()
    {
        cursorAim = GetComponent<PlayerCursorAim>();

        SetupTracer();
    }

    private void SetupTracer()
    {
        tracer = gameObject.AddComponent<LineRenderer>();
        tracer.positionCount = 2;
        tracer.startWidth = 0.025f;
        tracer.endWidth   = 0.008f;
        tracer.useWorldSpace = true;
        tracer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        tracer.receiveShadows = false;
        tracer.generateLightingData = false;

        // Material mínimo para que el color sea visible sin texturas externas.
        Shader unlit = Shader.Find("Sprites/Default");
        if (unlit == null) unlit = Shader.Find("Unlit/Color");
        tracer.material = new Material(unlit);

        tracer.startColor = tracerColor;
        tracer.endColor   = new Color(tracerColor.r, tracerColor.g, tracerColor.b, 0f);
        tracer.enabled = false;
    }

    // ─────────────────────────────────────────────────────────────────────
    private void Update()
    {
        // Apagar luz de destello tras su duración.
        if (muzzleLight != null && Time.time >= muzzleLightOffTime)
            muzzleLight.enabled = false;

        Mouse mouse = Mouse.current;
        if (mouse == null) return;

        if (mouse.rightButton.wasPressedThisFrame && Time.time >= nextFireTime)
        {
            Fire();
            nextFireTime = Time.time + 1f / Mathf.Max(0.01f, fireRate);
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    private void Fire()
    {
        if (muzzlePoint == null)
        {
            Debug.LogWarning("[PlayerShooter] MuzzlePoint no asignado. " +
                             "Crea un Transform hijo del arma en la punta del cañón y asígnalo.");
            return;
        }

        Vector3 origin    = muzzlePoint.position;
        Vector3 aimTarget = cursorAim.AimPoint;
        Vector3 direction = (aimTarget - origin);

        // Si la mira está prácticamente en el origen del disparo, usar el forward del MuzzlePoint.
        if (direction.sqrMagnitude < 0.01f)
            direction = muzzlePoint.forward;
        else
            direction.Normalize();

        Vector3 hitPoint = origin + direction * maxRange;
        hadImpact = false;

        if (Physics.Raycast(origin, direction, out RaycastHit hit, maxRange,
                            hitMask, QueryTriggerInteraction.Ignore))
        {
            hitPoint    = hit.point;
            hadImpact   = true;
            lastImpactPoint = hitPoint;

            // Aplicar daño si el objeto es dañable.
            IDamageable damageable = hit.collider.GetComponentInParent<IDamageable>();
            damageable?.TakeDamage(damage);

#if UNITY_EDITOR
            Debug.DrawLine(origin, hitPoint, Color.red, 0.5f);
            Debug.DrawRay(hitPoint, hit.normal * 0.15f, Color.yellow, 0.5f);
#endif
        }

        ShowMuzzleFlash();
        ShowTracer(origin, hitPoint);
    }

    // ─────────────────────────────────────────────────────────────────────
    private void ShowMuzzleFlash()
    {
        if (muzzleLight == null) return;
        muzzleLight.enabled = true;
        muzzleLightOffTime  = Time.time + muzzleFlashDuration;
    }

    private void ShowTracer(Vector3 from, Vector3 to)
    {
        tracer.SetPosition(0, from);
        tracer.SetPosition(1, to);
        tracer.enabled = true;

        CancelInvoke(nameof(HideTracer));
        Invoke(nameof(HideTracer), tracerDuration);
    }

    private void HideTracer()
    {
        tracer.enabled = false;
    }

    // ─────────────────────────────────────────────────────────────────────
    // Gizmos de depuración (visibles solo en la Scene view del Editor).
    private void OnDrawGizmosSelected()
    {
        if (muzzlePoint != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(muzzlePoint.position, 0.04f);
            Gizmos.DrawRay(muzzlePoint.position, muzzlePoint.forward * 0.5f);
        }

        if (hadImpact)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(lastImpactPoint, impactGizmoRadius);
        }
    }
}
