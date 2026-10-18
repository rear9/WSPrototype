using System.Linq;
using UnityEngine;
using UnityEngine.Events;

/*
public class CampfireManager : MinigameBase
{
    private enum FireState { Ember, FireState1, FireState2, FireState3 }

    [System.Serializable]
    public class FireAudioTable
    {
        public AudioClip fireBurning;
        public AudioClip wind;
        public AudioClip placingLogs;
        public AudioClip throwingLogs;
        public AudioClip sparks;
    }

    private const int PlayerCount = 4;
    private const float StateDropBuffer = 0.5f;

    [Header("Fire")]
    [SerializeField] private Animator[] fireAnimators = new Animator[PlayerCount];
    [SerializeField] private string[] fireStateAnimationNames = { "embers", "state1clip", "state2clip", "state3clip" };
    [SerializeField] private FireState currentState = FireState.Ember;
    [SerializeField] private float currentCharges;
    [SerializeField] private float survivalTime;

    [Header("Charges")]
    [SerializeField] private int chargesPerPlayer = 12;
    [SerializeField, Min(0.1f)] private float secondsPerCharge = 12f;
    [SerializeField, Range(0f, 0.5f)] private float decayIncreasePerAdditionalPlayer = 0.05f;

    [Header("Hue")]
    [SerializeField] private Color[] hueColours = // colour-blind safe palette, first is default
    {
        new Color32(230, 159, 0, 255), // orange
        new Color32(86, 180, 233, 255), // sky blue
        new Color32(0, 158, 115, 255), // green
        new Color32(240, 228, 66, 255), // yellow
        new Color32(0, 114, 178, 255), // blue
        new Color32(213, 94, 0, 255), // vermillion
        new Color32(204, 121, 167, 255) // purple
    };
    [SerializeField, Min(0f)] private float hueLerpSpeed = 4f;

    [Header("Player UI")]
    [SerializeField] private GameObject[] playerIdlePanels = new GameObject[PlayerCount];
    [SerializeField, Min(0f)] private float blurFadeDuration = 0.75f;
    [SerializeField] private UnityEvent<int> onPlayerJoined = new();

    [Header("Audio")]
    [SerializeField] private FireAudioTable audioTable = new();
    [SerializeField] private AudioSource fireLoopSource;

    private readonly bool[] _joined = new bool[PlayerCount];
    private readonly PlayerPresentationFade[] _fades = new PlayerPresentationFade[PlayerCount];
    private readonly SpriteRenderer[] _fireSprites = new SpriteRenderer[PlayerCount];
    private int _hueIndex;
    private Color _fireHue;
    private PlayerManager _playerManager;
    private PlayerAudioManager _playerAudio;
    private int _activePlayerCount;
    private float _maxCharges;

    public float SurvivalTime => survivalTime;
    public float CurrentCharges => currentCharges;
    public float MaxCharges => _maxCharges;

    public override bool SupportsLateJoining => true;
    public override bool UsesCustomJoinFlow => true;
    public override bool UsesGlobalRoundTimer => false;

    private void Awake() // cache references and set the starting look
    {
        _playerManager = FindAnyObjectByType<PlayerManager>();
        _playerAudio = FindAnyObjectByType<PlayerAudioManager>();
        if (fireLoopSource == null) fireLoopSource = GetComponent<AudioSource>();
        if (fireLoopSource != null) fireLoopSource.playOnAwake = false;

        FindFireAnims();
        SetupIdlePanels();
        for (int i = 0; i < PlayerCount; i++)
        {
            if (fireAnimators[i] != null)
            {
                _fireSprites[i] = fireAnimators[i].GetComponent<SpriteRenderer>();
                fireAnimators[i].enabled = true;
            }
            SetPlayerActive(i, false, false);
        }

        _fireHue = hueColours[0];
        //SetFireAudio(false);
        SetFireState(FireState.Ember, true);
    }

    protected override void OnUpdate() // decays fuel and updates the fire state
    {
        if (!m_IsMinigameActive) return;

        SyncJoined();
        if (_activePlayerCount == 0)
        {
            if (currentCharges > 0f || survivalTime > 0f || currentState != FireState.Ember) ResetFire(false);
            return;
        }

        if (currentCharges > 0f)
        {
            float decayRate = (1f + (_activePlayerCount - 1) * decayIncreasePerAdditionalPlayer) / secondsPerCharge;
            survivalTime += Time.deltaTime;
            currentCharges = Mathf.Max(0f, currentCharges - decayRate * Time.deltaTime);
        }

        SetFireState(GetStateFromCharges(currentCharges));
        //SetFireAudio(currentState != FireState.Ember);
    }

    private void LateUpdate() // resyncs after Connect hides the idle panel
    {
        if (m_IsMinigameActive) SyncJoined();
        foreach (PlayerPresentationFade fade in _fades)
        {
            if (fade != null) fade.Tick(Time.unscaledDeltaTime);
        }
        UpdateFireHue();
    }

    public override void PlayTutorial(System.Action callback) => callback?.Invoke(); // no tutorial

    public override void LoadMiniGame()
    {
        base.LoadMiniGame();
        ResetFire(false);
    }

    public override void StartMinigame() // keeps players who joined beforehand
    {
        ResetFire(true, true);
        base.StartMinigame();
        for (int i = 0; i < PlayerCount; i++)
        {
            if (_joined[i]) onPlayerJoined?.Invoke(i);
        }
    }

    public override void OnDirectionalInput(int playerIndex, Vector2 direction) { }

    public override void OnPrimaryFire(int playerIndex) // joins, or cycles the shared hue
    {
        if (!CanHandleInput(playerIndex)) return;
        if (!_joined[playerIndex]) JoinPlayer(playerIndex);
        else _hueIndex = (_hueIndex + 1) % hueColours.Length;
    }

    public override void OnSecondaryFire(int playerIndex) // add charge
    {
        if (!CanHandleInput(playerIndex)) return;
        if (!_joined[playerIndex])
        {
            JoinPlayer(playerIndex);
            return;
        }
        if (currentCharges >= _maxCharges) return;

        FireState previous = currentState;
        currentCharges = Mathf.Min(_maxCharges, currentCharges + 1f);
        // PlayPlayerAudio(playerIndex, audioTable.throwingLogs != null ? audioTable.throwingLogs : audioTable.placingLogs);
        SetFireState(GetStateFromCharges(currentCharges));
        //if (currentState != previous) PlayGlobalAudio(audioTable.placingLogs);
    }

    public override void TimeUp() { }

    protected override void OnResetGame() => ResetFire(false);

    public override GameScoreData GetScoreData() => new GameScoreData();

    public override bool OwnsJoinPrompt(GameObject prompt) // stops GameManager toggling our prompts
    {
        return prompt != null && _fades.Any(fade => fade != null && fade.JoinPrompt == prompt);
    }

    private bool CanHandleInput(int playerIndex) => m_IsMinigameActive && playerIndex >= 0 && playerIndex < PlayerCount;

    private void JoinPlayer(int playerIndex) // marks the player in and fades their panel
    {
        if (_joined[playerIndex]) return;

        _joined[playerIndex] = true;
        if (_playerManager != null)
        {
            Player player = _playerManager.players[playerIndex];
            player.idling = false;
            player.idleTimer = 0f;
            _playerManager.SetCountdownScreenVisibility(playerIndex, false);
            _playerManager.SetHoldingScreenVisibility(playerIndex, false);
        }

        SetPlayerActive(playerIndex, true);
        UpdatePlayerCap();
        onPlayerJoined?.Invoke(playerIndex);
    }

    private void SyncJoined() // picks up players joined through Player.Connect
    {
        for (int i = 0; i < PlayerCount; i++)
        {
            if (PlayerUtilities.GetPlayerState(i) == Player.PlayerState.ACTIVE) JoinPlayer(i);
        }
    }

    private void UpdatePlayerCap() // rescales fuel when the player count changes
    {
        int count = _joined.Count(joined => joined);
        if (count == _activePlayerCount) return;

        float fuelRatio = _maxCharges > 0f ? currentCharges / _maxCharges : 0f;
        _activePlayerCount = count;
        _maxCharges = chargesPerPlayer * count;
        currentCharges = _maxCharges * fuelRatio;
        SetFireState(GetStateFromCharges(currentCharges));
    }

    private FireState GetStateFromCharges(float charges) // drop buffer stops state flicker
    {
        if (charges <= 0f || _maxCharges <= 0f) return FireState.Ember;

        float step = _maxCharges / 4f;
        int rising = Mathf.Clamp(Mathf.CeilToInt(charges / step) - 1, 0, 3);
        int falling = Mathf.Clamp(Mathf.FloorToInt((charges + StateDropBuffer) / step), 0, 3);
        return (FireState)(rising > (int)currentState ? rising : Mathf.Min((int)currentState, falling));
    }

    private void SetFireState(FireState newState, bool force = false) // scales and replays each fire
    {
        if (currentState == newState && !force) return;

        FireState previous = currentState;
        currentState = newState;
        for (int i = 0; i < PlayerCount; i++)
        {
            if (fireAnimators[i] == null) continue;
            PlayFireAnim(fireAnimators[i]);
        }

        //if (previous == FireState.Ember && newState != FireState.Ember && m_IsMinigameActive) PlayGlobalAudio(audioTable.sparks);
    }

    private void PlayFireAnim(Animator animator) // restarts the clip for this state
    {
        int hash = Animator.StringToHash(fireStateAnimationNames[(int)currentState]);
        if (animator.HasState(0, hash)) animator.Play(hash, 0, 0f);
        else animator.Rebind();
        animator.Update(0f);
    }

    private void SetPlayerActive(int playerIndex, bool joined, bool animate = true) // idle panel look
    {
        PlayerPresentationFade fade = _fades[playerIndex];
        if (fade == null) return;

        fade.gameObject.SetActive(true);
        if (!joined) fade.Restore();
        else if (animate) fade.FadeOut();
        else fade.HideInstant();
    }

    private void ResetFire(bool keepActivePlayers, bool animateJoined = false) // back to an unlit fire
    {
        currentCharges = 0f;
        survivalTime = 0f;
        _activePlayerCount = 0;
        _maxCharges = 0f;
        _hueIndex = 0;
        _fireHue = hueColours[0];
        for (int i = 0; i < PlayerCount; i++)
        {
            _joined[i] = keepActivePlayers && PlayerUtilities.GetPlayerState(i) == Player.PlayerState.ACTIVE;
        }

        UpdatePlayerCap();
        SetFireState(FireState.Ember, true);
        //SetFireAudio(false);
        for (int i = 0; i < PlayerCount; i++) SetPlayerActive(i, _joined[i], animateJoined);
    }

    private void UpdateFireHue() // eases every fire sprite towards the shared hue
    {
        _fireHue = Color.Lerp(_fireHue, hueColours[_hueIndex], hueLerpSpeed * Time.deltaTime);
        foreach (SpriteRenderer sprite in _fireSprites)
        {
            if (sprite == null) continue;
            Color hue = _fireHue;
            hue.a = sprite.color.a;
            sprite.color = hue;
        }
    }

    // private void SetFireAudio(bool lit) // loops burning sound while lit
    // {
    //     if (fireLoopSource == null) return;
    //     if (!lit)
    //     {
    //         fireLoopSource.Stop();
    //         return;
    //     }
    //     if (audioTable.fireBurning == null || fireLoopSource.isPlaying) return;

    //     fireLoopSource.clip = audioTable.fireBurning;
    //     fireLoopSource.loop = true;
    //     fireLoopSource.Play();
    // }

    // private void PlayPlayerAudio(int playerIndex, AudioClip clip) // through that player's speaker
    // {
    //     if (clip != null && _playerAudio != null) WhalesongAudio.PlayOneShot(playerIndex, clip);
    // }

    // private void PlayGlobalAudio(AudioClip clip) // through every speaker
    // {
    //     if (clip != null && _playerAudio != null) WhalesongAudio.PlayGlobalOneShot(clip);
    // }

    private void FindFireAnims() // try to find each player's animator by name if something breaks
    {
        for (int i = 0; i < PlayerCount; i++)
        {
            if (fireAnimators[i] == null) fireAnimators[i] = FindChild(transform, "Player" + (i + 1))?.GetComponentInChildren<Animator>(true);
        }
    }

    private void SetupIdlePanels() // finds idle panels and adds fade components
    {
        Canvas canvas = System.Array.Find(FindObjectsByType<Canvas>(FindObjectsInactive.Include), c => c.name == "PlayerStateCanvas");
        for (int i = 0; i < PlayerCount; i++)
        {
            if (playerIdlePanels[i] == null && canvas != null) playerIdlePanels[i] = FindChild(canvas.transform, "IdlePanel" + i)?.gameObject;
            if (playerIdlePanels[i] == null)
            {
                Debug.LogWarning("[CampfireManager] could not find idle panel" + i, this);
                continue;
            }

            if (!playerIdlePanels[i].TryGetComponent(out _fades[i])) _fades[i] = playerIdlePanels[i].AddComponent<PlayerPresentationFade>();
            _fades[i].Init(blurFadeDuration);
        }
    }

    private static Transform FindChild(Transform root, string childName) // deep search helper [including inactive]
    {
        return root.GetComponentsInChildren<Transform>(true).FirstOrDefault(child => child.name == childName);
    }
}
*/