using UnityEngine;

public class PlayerPresentationFade : MonoBehaviour // fades a player's idle panel out
{
    private enum Phase { Shown, Fading, Hidden }

    private CanvasGroup _group;
    private Phase _phase = Phase.Shown;
    private float _duration;
    private float _elapsed;

    public GameObject JoinPrompt { get; private set; }

    public void Init(float duration) // grabs the prompt and a canvas group
    {
        _duration = duration;
        JoinPrompt = transform.Find("PressToPlay")?.gameObject;
        if (!TryGetComponent(out _group)) _group = gameObject.AddComponent<CanvasGroup>();
    }

    public void Restore() // back to fully visible
    {
        _phase = Phase.Shown;
        _group.alpha = 1f;
        SetPrompt(true);
    }

    public void HideInstant() // skips the fade
    {
        _phase = Phase.Hidden;
        Hide();
    }

    public void FadeOut() // ignored if already fading or hidden
    {
        if (_phase != Phase.Shown) return;
        if (_duration <= 0f)
        {
            HideInstant();
            return;
        }

        _phase = Phase.Fading;
        _elapsed = 0f;
        CheckActive();
    }

    public void Tick(float deltaTime) // driven by the minigame each LateUpdate
    {
        if (_phase == Phase.Hidden) Hide();
        if (_phase != Phase.Fading) return;

        CheckActive();
        _elapsed += deltaTime;
        _group.alpha = 1f - Mathf.Clamp01(_elapsed / _duration);
        if (_elapsed >= _duration) HideInstant();
    }

    private void Hide() // re-applied each tick so nothing else can show it
    {
        _group.alpha = 0f;
        SetPrompt(false);
    }

    private void CheckActive() // undoes anything that disables the panel mid-fade
    {
        gameObject.SetActive(true);
        SetPrompt(true);
    }

    private void SetPrompt(bool active)
    {
        if (JoinPrompt != null) JoinPrompt.SetActive(active);
    }
}
