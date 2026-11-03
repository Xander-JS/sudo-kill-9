using UnityEngine;
using SudoKill9.Combat;

/// <summary>
/// Controla el comportamiento físico y visual de una bala / proyectil:
/// - Desplazamiento balístico a alta velocidad en la dirección frontal (transform.forward).
/// - Detección dual y continua de colisiones: Raycast continuo para evitar efecto túnel a alta velocidad
///   más OnTriggerEnter con Rigidbody cinemático y SphereCollider para respuesta física garantizada.
/// - Aplica daño a cualquier entidad que implemente IDamageable (enemigos, jefes, objetivos).
/// - Filtra y excluye únicamente al propio personaje disparador (tiradorRaiz), permitiendo colisionar
///   con TODOS los objetos del escenario, paredes y enemigos (incluida la Capa 0 / Default).
/// - Genera trazador luminoso (TrailRenderer), cuerpo tridimensional de bala dorada y destello de luz.
/// - Compatible para ser reemplazado por cualquier prefab o modelo 3D asignado en el Inspector.
/// </summary>
[RequireComponent(typeof(SphereCollider))]
[RequireComponent(typeof(Rigidbody))]
public class BalaProyectil : MonoBehaviour
{
    [Header("Parámetros Físicos y de Combate")]
    [Tooltip("Velocidad de avance de la bala en metros por segundo.")]
    public float velocidad = 65f;

    [Tooltip("Tiempo de vida máximo en segundos antes de autodestruirse.")]
    public float tiempoDeVida = 3.5f;

    [Tooltip("Cantidad de daño aplicada al impactar sobre un objeto con IDamageable.")]
    public float dano = 25f;

    [Tooltip("Capas con las que puede colisionar e impactar la bala.")]
    public LayerMask capasImpacto = ~0;

    [Header("Efectos Opcionales")]
    [Tooltip("Efecto de partículas o destello que se instancia en el punto de impacto.")]
    public GameObject efectoImpactoPrefab;

    [Tooltip("Color del trazador de la bala.")]
    public Color colorTrazador = new Color(1f, 0.85f, 0.3f, 1f);

    [Header("Disparador")]
    [Tooltip("Transform raíz del personaje que disparó la bala para evitar auto-impactos.")]
    public Transform tiradorRaiz;

    private Vector3 posicionAnterior;
    private bool haImpactado = false;
    private float tiempoRestante;
    private SphereCollider esferaCol;
    private Rigidbody rb;

    private void Awake()
    {
        tiempoRestante = tiempoDeVida;
        posicionAnterior = transform.position;

        GarantizarComponentesFisicosYVisuales();
    }

    private void Start()
    {
        posicionAnterior = transform.position;
        IgnorarColisionesConTirador();
    }

    private void Update()
    {
        if (haImpactado) return;

        tiempoRestante -= Time.deltaTime;
        if (tiempoRestante <= 0f)
        {
            DestruirBala();
            return;
        }

        float distanciaPaso = velocidad * Time.deltaTime;
        Vector3 direccion = transform.forward;
        Vector3 posicionActual = transform.position;
        float radioDeteccion = (esferaCol != null) ? Mathf.Max(0.04f, esferaCol.radius) : 0.06f;

        // 0. Detección inmediata de solapamiento en el origen (disparo a quemarropa / contacto físico estrecho)
        // Evita que balas disparadas dentro del collider del objetivo atraviesen sin registrar colisión
        Collider[] solapados = Physics.OverlapSphere(posicionActual, radioDeteccion, capasImpacto, QueryTriggerInteraction.Collide);
        if (solapados != null && solapados.Length > 0)
        {
            for (int i = 0; i < solapados.Length; i++)
            {
                Collider col = solapados[i];
                if (EsColliderValido(col))
                {
                    Vector3 punto = col.ClosestPoint(posicionActual);
                    Vector3 normal = (posicionActual - punto).normalized;
                    if (normal.sqrMagnitude < 0.001f) normal = -direccion;
                    ProcesarImpacto(col, punto, normal);
                    return;
                }
            }
        }

        // 1. Barrido volumétrico continuo (SphereCastAll) para evitar túnel a cualquier velocidad
        RaycastHit[] hits = Physics.SphereCastAll(posicionActual, radioDeteccion, direccion, distanciaPaso, capasImpacto, QueryTriggerInteraction.Collide);
        if (hits == null || hits.Length == 0)
        {
            hits = Physics.RaycastAll(posicionActual, direccion, distanciaPaso, capasImpacto, QueryTriggerInteraction.Collide);
        }

        if (hits != null && hits.Length > 0)
        {
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            for (int i = 0; i < hits.Length; i++)
            {
                RaycastHit h = hits[i];
                if (EsColliderValido(h.collider))
                {
                    Vector3 puntoImpacto = h.point;
                    if (puntoImpacto == Vector3.zero && h.collider != null)
                    {
                        puntoImpacto = h.collider.ClosestPoint(posicionActual);
                    }
                    Vector3 normalImpacto = (h.normal.sqrMagnitude > 0.001f) ? h.normal : -direccion;
                    ProcesarImpacto(h.collider, puntoImpacto, normalImpacto);
                    return;
                }
            }
        }

        transform.position += direccion * distanciaPaso;
        posicionAnterior = transform.position;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (haImpactado) return;
        if (!EsColliderValido(other)) return;

        Vector3 puntoImpacto = transform.position;
        if (other is BoxCollider || other is SphereCollider || other is CapsuleCollider || (other is MeshCollider mc && mc.convex))
        {
            puntoImpacto = other.ClosestPoint(transform.position);
        }
        else
        {
            puntoImpacto = other.bounds.ClosestPoint(transform.position);
        }

        Vector3 normal = (transform.position - puntoImpacto).normalized;
        if (normal.sqrMagnitude < 0.001f) normal = -transform.forward;

        ProcesarImpacto(other, puntoImpacto, normal);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (haImpactado) return;
        if (collision == null || !EsColliderValido(collision.collider)) return;

        ContactPoint contact = collision.GetContact(0);
        Vector3 punto = contact.point;
        Vector3 normal = contact.normal;
        if (normal.sqrMagnitude < 0.001f) normal = -transform.forward;

        ProcesarImpacto(collision.collider, punto, normal);
    }

    private bool EsColliderValido(Collider col)
    {
        if (col == null) return false;
        if (col.gameObject == gameObject) return false;
        if (col.transform.IsChildOf(transform)) return false;

        // Evitar auto-colisión con el jugador que disparó
        if (tiradorRaiz != null)
        {
            if (col.transform == tiradorRaiz || col.transform.IsChildOf(tiradorRaiz) || col.transform.root == tiradorRaiz)
            {
                return false;
            }
        }

        // Verificar máscara de capas
        if ((capasImpacto.value & (1 << col.gameObject.layer)) == 0)
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Maneja el impacto de la bala con un collider del entorno o enemigo.
    /// Detiene y destruye la bala inmediatamente para que no traspase el objetivo.
    /// </summary>
    public void ProcesarImpacto(Collider colliderImpactado, Vector3 punto, Vector3 normal)
    {
        if (haImpactado) return;
        haImpactado = true;
        transform.position = punto;

        if (colliderImpactado != null)
        {
            // 1. Buscar prioritariamente a WindowsBossAI en el collider o su jerarquía
            WindowsBossAI boss = colliderImpactado.GetComponentInParent<WindowsBossAI>();
            if (boss == null) boss = colliderImpactado.GetComponentInChildren<WindowsBossAI>();
            if (boss == null && colliderImpactado.transform.root != null)
            {
                boss = colliderImpactado.transform.root.GetComponentInChildren<WindowsBossAI>();
            }

            if (boss != null)
            {
                boss.RecibirDisparo(dano, punto, transform.forward);
            }
            else
            {
                // 2. Aplicar daño a cualquier entidad con IDamageable
                IDamageable objetivoDanable = colliderImpactado.GetComponentInParent<IDamageable>();
                if (objetivoDanable == null) objetivoDanable = colliderImpactado.GetComponentInChildren<IDamageable>();
                if (objetivoDanable == null && colliderImpactado.transform.root != null)
                {
                    objetivoDanable = colliderImpactado.transform.root.GetComponentInChildren<IDamageable>();
                }

                if (objetivoDanable != null)
                {
                    objetivoDanable.TakeDamage(dano);
                }
            }
        }

        // Desactivar inmediatamente collider y renderers de la bala para impedir avance visual o colisiones residuales
        if (esferaCol != null) esferaCol.enabled = false;
        Collider rootCol = GetComponent<Collider>();
        if (rootCol != null) rootCol.enabled = false;
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null) renderers[i].enabled = false;
        }

        Vector3 normalSegura = normal;
        if (normalSegura.sqrMagnitude < 0.001f) normalSegura = -transform.forward;
        Vector3 upwardSeguro = Mathf.Abs(Vector3.Dot(normalSegura.normalized, Vector3.up)) > 0.95f ? Vector3.forward : Vector3.up;
        Quaternion rotacionImpacto = Quaternion.LookRotation(normalSegura, upwardSeguro);

        // Instanciar efecto de impacto si está configurado o generar destello genérico
        if (efectoImpactoPrefab != null)
        {
            Instantiate(efectoImpactoPrefab, punto, rotacionImpacto);
        }
        else
        {
            GenerarEfectoImpactoGenerico(punto, normalSegura, upwardSeguro);
        }

#if UNITY_EDITOR
        Debug.DrawRay(punto, normalSegura * 0.35f, Color.yellow, 1f);
#endif

        DestruirBala();
    }

    private void GenerarEfectoImpactoGenerico(Vector3 punto, Vector3 normal, Vector3 upward)
    {
        GameObject fx = new GameObject("Impacto_Bala_FX");
        fx.transform.position = punto;
        fx.transform.rotation = Quaternion.LookRotation(normal, upward);

        GameObject luzGO = new GameObject("LuzImpacto");
        luzGO.transform.SetParent(fx.transform, false);
        Light luz = luzGO.AddComponent<Light>();
        luz.type = LightType.Point;
        luz.color = new Color(1f, 0.85f, 0.35f);
        luz.range = 2.2f;
        luz.intensity = 3.0f;

        ParticleSystem ps = fx.AddComponent<ParticleSystem>();
        ParticleSystemRenderer psr = fx.GetComponent<ParticleSystemRenderer>();
        if (psr != null)
        {
            Shader particleShader = Shader.Find("Universal Render Pipeline/Particles/Unlit") 
                                 ?? Shader.Find("Particles/Standard Unlit") 
                                 ?? Shader.Find("Sprites/Default");
            if (particleShader != null)
            {
                psr.material = new Material(particleShader);
            }
        }

        var main = ps.main;
        main.duration = 0.2f;
        main.loop = false;
        main.startLifetime = 0.18f;
        main.startSpeed = 4.5f;
        main.startSize = 0.07f;
        main.startColor = new Color(1f, 0.88f, 0.3f);
        main.stopAction = ParticleSystemStopAction.Destroy;

        var emission = ps.emission;
        emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 10) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 40f;
        shape.radius = 0.04f;

        if (Application.isPlaying) Destroy(fx, 0.35f);
        else DestroyImmediate(fx);
    }

    private void DestruirBala()
    {
        if (Application.isPlaying)
        {
            Destroy(gameObject);
        }
        else
        {
            DestroyImmediate(gameObject);
        }
    }

    /// <summary>
    /// Configura y garantiza los componentes físicos (Rigidbody, SphereCollider)
    /// y visuales (Modelo 3D dorado, TrailRenderer luminoso).
    /// </summary>
    public void GarantizarComponentesFisicosYVisuales()
    {
        // 1. Configurar Rigidbody
        rb = GetComponent<Rigidbody>();
        if (rb == null) rb = gameObject.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

        // 2. Configurar SphereCollider como Trigger para interacciones físicas
        esferaCol = GetComponent<SphereCollider>();
        if (esferaCol == null) esferaCol = gameObject.AddComponent<SphereCollider>();
        esferaCol.isTrigger = true;
        esferaCol.radius = 0.05f;

        // 3. Añadir TrailRenderer luminoso si no existe
        TrailRenderer trail = GetComponent<TrailRenderer>();
        if (trail == null)
        {
            trail = gameObject.AddComponent<TrailRenderer>();
            trail.time = 0.12f;
            trail.startWidth = 0.05f;
            trail.endWidth = 0.005f;
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trail.receiveShadows = false;

            Shader unlit = Shader.Find("Sprites/Default");
            if (unlit == null) unlit = Shader.Find("Universal Render Pipeline/Unlit");
            if (unlit == null) unlit = Shader.Find("Unlit/Color");

            if (unlit != null)
            {
                Material mat = new Material(unlit);
                mat.color = colorTrazador;
                trail.material = mat;
            }

            Gradient grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[] { new GradientColorKey(colorTrazador, 0f), new GradientColorKey(new Color(1f, 0.4f, 0.1f), 1f) },
                new GradientAlphaKey[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) }
            );
            trail.colorGradient = grad;
        }

        // 4. Si no tiene MeshRenderer ni hijos con MeshRenderer, crear cuerpo 3D de bala
        MeshRenderer mr = GetComponentInChildren<MeshRenderer>();
        if (mr == null)
        {
            GameObject cuerpoBala = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            cuerpoBala.name = "ModeloBala_3D";
            cuerpoBala.transform.SetParent(transform, false);
            // La bala se orienta hacia el eje Z (frente)
            cuerpoBala.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            cuerpoBala.transform.localScale = new Vector3(0.04f, 0.08f, 0.04f);

            // Eliminar collider del hijo para que el esferaCol del padre gestione las colisiones
            Collider c = cuerpoBala.GetComponent<Collider>();
            if (c != null)
            {
                c.enabled = false;
                if (Application.isPlaying) Destroy(c);
                else DestroyImmediate(c);
            }

            MeshRenderer mrHijo = cuerpoBala.GetComponent<MeshRenderer>();
            if (mrHijo != null)
            {
                Shader litShader = Shader.Find("Universal Render Pipeline/Lit");
                if (litShader == null) litShader = Shader.Find("Standard");
                if (litShader == null) litShader = Shader.Find("Sprites/Default");

                if (litShader != null)
                {
                    Material matBala = new Material(litShader);
                    matBala.color = new Color(1f, 0.8f, 0.2f); // Dorado cobrizo
                    if (matBala.HasProperty("_Metallic")) matBala.SetFloat("_Metallic", 0.9f);
                    if (matBala.HasProperty("_Smoothness")) matBala.SetFloat("_Smoothness", 0.85f);
                    if (matBala.HasProperty("_EmissionColor"))
                    {
                        matBala.EnableKeyword("_EMISSION");
                        matBala.SetColor("_EmissionColor", new Color(1f, 0.7f, 0.1f) * 0.8f);
                    }
                    mrHijo.material = matBala;
                }
            }
        }
    }

    /// <summary>
    /// Ignora físicamente colisiones con todos los colliders del tirador.
    /// </summary>
    public void IgnorarColisionesConTirador()
    {
        if (tiradorRaiz == null) return;
        Collider miCollider = GetComponent<Collider>();
        if (miCollider == null) return;

        Collider[] collidersTirador = tiradorRaiz.GetComponentsInChildren<Collider>(true);
        foreach (Collider c in collidersTirador)
        {
            if (c != null && c != miCollider)
            {
                Physics.IgnoreCollision(miCollider, c, true);
            }
        }
    }

    /// <summary>
    /// Método de fábrica para instanciar y configurar una bala en una posición y dirección dadas.
    /// </summary>
    public static BalaProyectil Disparar(
        GameObject prefabOpcional,
        Vector3 posicion,
        Vector3 direccion,
        float velocidad,
        float dano,
        float tiempoVida,
        LayerMask capas,
        GameObject efectoImpacto = null,
        Transform tirador = null)
    {
        GameObject balaGO;
        if (prefabOpcional != null)
        {
            balaGO = Instantiate(prefabOpcional, posicion, Quaternion.LookRotation(direccion));
        }
        else
        {
            balaGO = new GameObject("Bala_Proyectil");
            balaGO.transform.position = posicion;
            balaGO.transform.rotation = Quaternion.LookRotation(direccion);
        }

        BalaProyectil proyectil = balaGO.GetComponent<BalaProyectil>();
        if (proyectil == null)
        {
            proyectil = balaGO.AddComponent<BalaProyectil>();
        }

        proyectil.velocidad = velocidad;
        proyectil.dano = dano;
        proyectil.tiempoDeVida = tiempoVida;
        proyectil.capasImpacto = capas;
        proyectil.efectoImpactoPrefab = efectoImpacto;
        proyectil.tiradorRaiz = tirador;

        proyectil.GarantizarComponentesFisicosYVisuales();
        proyectil.IgnorarColisionesConTirador();

        return proyectil;
    }
}
