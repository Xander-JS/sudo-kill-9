
using UnityEngine;
using UnityEngine.InputSystem;

public class DirectionalSprite : MonoBehaviour
{
    [Header("Sprites del personaje")]
    public Sprite front;
    public Sprite back;
    public Sprite left;
    public Sprite right;

    [Header("Movimiento")]
    public float moveSpeed = 3f;

    private SpriteRenderer spriteRenderer;
    private Keyboard keyboard;

    void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        keyboard = Keyboard.current;
    }

    void Update()
    {
        keyboard = Keyboard.current;

        if (keyboard == null || spriteRenderer == null)
            return;

        Vector2 input = Vector2.zero;
        if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) input.y += 1;
        if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) input.y -= 1;
        if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) input.x -= 1;
        if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) input.x += 1;

        if (input == Vector2.zero)
            return;

        // normalized so diagonals aren't faster
        transform.Translate(input.normalized * moveSpeed * Time.deltaTime, Space.World);

        if (input.y > 0) ChangeSprite(back);
        else if (input.y < 0) ChangeSprite(front);
        else if (input.x < 0) ChangeSprite(left);
        else ChangeSprite(right);
    }

    void ChangeSprite(Sprite newSprite)
    {
        if (newSprite != null)
        {
            spriteRenderer.sprite = newSprite;
        }
    }
}
