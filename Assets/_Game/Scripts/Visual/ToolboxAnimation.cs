using UnityEngine;

public sealed class ToolboxAnimation : MonoBehaviour
{
    private SpriteRenderer target;
    private Sprite[] frames;
    private float fps;
    public void Setup(SpriteRenderer renderer, Sprite[] sprites, float rate) { target = renderer; frames = sprites; fps = Mathf.Max(1, rate); }
    private void Update()
    {
        if (target != null && frames != null && frames.Length > 0 && !GameMenu.IsOpen)
        {
            var frame = frames[(int)(Time.time * fps) % frames.Length];
            if (frame != null) target.sprite = frame;
        }
    }
}
