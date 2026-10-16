using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MyWhaleMinigame : MinigameBase
{
    private enum FireState { Ember, FireState1, FireState2, FireState3, FireState4 }

    [System.Serializable]
    public class FireAudioTable
    {
        public AudioClip fireBurning;
        public AudioClip wind;
        public AudioClip placingLogs;
        public AudioClip throwingLogs;
        public AudioClip sparks;
    }

    [Header("Fire")]
    [SerializeField] private Transform playersContainer;
    [SerializeField] private Animator[] fireAnimators = new Animator[4];
    [SerializeField] private int chargesPerPlayer = 12;
    [SerializeField] private float secondsPerCharge = 12f;
    [SerializeField] private float emberTimeoutSeconds = 35f;
    [SerializeField] private float[] fireStateScales = { 0.2f, 0.4f, 0.6f, 0.8f, 1f };
    [SerializeField] private FireState currentState = FireState.Ember;
    [SerializeField] private int maxCharges;
    [SerializeField] private float currentCharges;
    [SerializeField] private float survivalTime;

    [Header("Audio")]
    [SerializeField] private FireAudioTable audioTable = new FireAudioTable();
    [SerializeField] private AudioSource fireLoopSource;

    private Vector3[] originalFireScales;
    private int activePlayerCount;
    private float timeInEmber;
    private bool gameEnded;
    private bool noActivePlayers;

    public float SurvivalTime => survivalTime;

    private void Awake()
    {
        InitializeFireAnimators();

        if (fireLoopSource == null)
        {
            fireLoopSource = GetComponent<AudioSource>();
        }
        if (fireLoopSource != null)
        {
            fireLoopSource.playOnAwake = false;
            fireLoopSource.Stop();
        }

        originalFireScales = new Vector3[fireAnimators.Length];
        for (int i = 0; i < fireAnimators.Length; i++)
        {
            if (fireAnimators[i] != null)
            {
                originalFireScales[i] = fireAnimators[i].transform.localScale;
                fireAnimators[i].enabled = false;
            }
        }

        SetFireState(FireState.Ember, true);
    }

    public override void PlayTutorial(System.Action callback)
    {
        callback?.Invoke();
    }

    public override void LoadMiniGame()
    {
        base.LoadMiniGame();
        ResetFire();
    }

    public override void StartMinigame()
    {
        ResetFire();
        base.StartMinigame();
        noActivePlayers = activePlayerCount == 0;
        SetFireAnimations(!noActivePlayers);
        if (!noActivePlayers)
        {
            //StartFireAudio();
        }
    }

    protected override void OnUpdate()
    {
        if (!m_IsMinigameActive || gameEnded)
        {
            return;
        }

        UpdatePlayerCapacity();
        if (activePlayerCount == 0)
        {
            if (!noActivePlayers)
            {
                ResetFire();
                noActivePlayers = true;
            }
            return;
        }

        if (noActivePlayers)
        {
            noActivePlayers = false;
            SetFireAnimations(true);
            StartFireAudio();
        }

        if (currentCharges > 0f)
        {
            survivalTime += Time.deltaTime;
            float decayRate = activePlayerCount / Mathf.Max(0.1f, secondsPerCharge);
            currentCharges = Mathf.Max(0f, currentCharges - decayRate * Time.deltaTime);
        }

        SetFireState(GetStateFromCharges(currentCharges));

        if (currentState == FireState.Ember)
        {
            timeInEmber += Time.deltaTime;
            if (timeInEmber >= emberTimeoutSeconds)
            {
                CompleteMinigame(false);
            }
        }
    }

    public override void OnDirectionalInput(int playerIndex, Vector2 direction)
    {
    }

    public override void OnPrimaryFire(int playerIndex)
    {
    }

    public override void OnSecondaryFire(int playerIndex)
    {
        if (!m_IsMinigameActive || gameEnded || playerIndex < 0 || playerIndex >= 4)
        {
            return;
        }

        UpdatePlayerCapacity();
        if (PlayerUtilities.GetPlayerState(playerIndex) != Player.PlayerState.ACTIVE || currentCharges >= maxCharges)
        {
            return;
        }

        FireState previousState = currentState;
        currentCharges = Mathf.Min(maxCharges, currentCharges + 1f);
        // PlayPlayerAudio(playerIndex, audioTable.throwingLogs != null ? audioTable.throwingLogs : audioTable.placingLogs);
        SetFireState(GetStateFromCharges(currentCharges));

        if (currentState != previousState && audioTable.placingLogs != null)
        {
            // PlayGlobalAudio(audioTable.placingLogs);
        }
    }

    public override void TimeUp()
    {
        CompleteMinigame(currentState != FireState.Ember);
    }

    protected override void OnResetGame()
    {
        ResetFire();
    }

    public override GameScoreData GetScoreData()
    {
        GameScoreData scoreData = new GameScoreData();
        int survivedSeconds = Mathf.FloorToInt(survivalTime);

        for (int i = 0; i < 4; i++)
        {
            if (PlayerUtilities.GetPlayerState(i) == Player.PlayerState.ACTIVE)
            {
                scoreData.PlayerScores[i] = survivedSeconds;
            }
        }

        scoreData.ScoreSuffix = "s survived";
        return scoreData;
    }

    private void UpdatePlayerCapacity()
    {
        int newPlayerCount = PlayerUtilities.GetActivePlayerCount();
        if (newPlayerCount == activePlayerCount)
        {
            return;
        }

        activePlayerCount = newPlayerCount;
        maxCharges = chargesPerPlayer * activePlayerCount;
        currentCharges = Mathf.Min(currentCharges, maxCharges);
        SetFireState(GetStateFromCharges(currentCharges));
    }

    private FireState GetStateFromCharges(float charges)
    {
        if (charges <= 0f)
        {
            return FireState.Ember;
        }
        if (charges < chargesPerPlayer)
        {
            return FireState.FireState1;
        }
        if (charges < chargesPerPlayer * 2f)
        {
            return FireState.FireState2;
        }
        if (charges < chargesPerPlayer * 3f)
        {
            return FireState.FireState3;
        }
        return FireState.FireState4;
    }


    private void SetFireState(FireState newState, bool forceUpdate = false)
    {
        if (currentState == newState && !forceUpdate)
        {
            return;
        }

        FireState previousState = currentState;
        currentState = newState;
        Debug.Log(previousState + "  -->  " + newState);
        if (newState == FireState.Ember && previousState != FireState.Ember)
        {
            timeInEmber = 0f;
        }
        else if (newState != FireState.Ember)
        {
            timeInEmber = 0f;
        }

        float scale = GetStateScale(newState);
        for (int i = 0; i < fireAnimators.Length; i++)
        {
            if (fireAnimators[i] != null)
            {
                fireAnimators[i].transform.localScale = originalFireScales[i] * scale;
            }
        }

        if (previousState == FireState.Ember && newState != FireState.Ember && m_IsMinigameActive)
        {
            PlayGlobalAudio(audioTable.sparks);
        }
    }

    private float GetStateScale(FireState state)
    {
        int index = (int)state;
        if (fireStateScales != null && index >= 0 && index < fireStateScales.Length)
        {
            return fireStateScales[index];
        }

        return 1f;
    }

    private void SetFireAnimations(bool isPlaying)
    {
        for (int i = 0; i < fireAnimators.Length; i++)
        {
            Animator animator = fireAnimators[i];
            if (animator == null)
            {
                continue;
            }

            animator.enabled = isPlaying;
            if (isPlaying)
            {
                animator.Rebind();
                animator.Play(0, 0, 0f);
                animator.Update(0f);
            }
        }
    }

    private void InitializeFireAnimators()
    {
        if (playersContainer == null)
        {
            Transform[] children = GetComponentsInChildren<Transform>(true);
            foreach (Transform child in children)
            {
                if (child.name == "Players")
                {
                    playersContainer = child;
                    break;
                }
            }
        }

        if (fireAnimators == null || fireAnimators.Length != 4)
        {
            fireAnimators = new Animator[4];
        }

        if (playersContainer == null)
        {
            Debug.LogWarning("MyWhaleMinigame could not find the Players container. Assign it in the Inspector.", this);
            return;
        }

        for (int i = 0; i < fireAnimators.Length; i++)
        {
            Transform player = playersContainer.Find("Player" + (i + 1));
            if (player == null)
            {
                Debug.LogWarning("MyWhaleMinigame could not find Player" + (i + 1) + " under Players.", this);
                continue;
            }

            Animator animator = player.GetComponentInChildren<Animator>(true);
            if (animator == null)
            {
                Debug.LogWarning("MyWhaleMinigame could not find an Animator under Player" + (i + 1) + ".", this);
                continue;
            }

            fireAnimators[i] = animator;
        }
    }

    private void StartFireAudio()
    {
        if (fireLoopSource == null || audioTable.fireBurning == null)
        {
            return;
        }

        fireLoopSource.clip = audioTable.fireBurning;
        fireLoopSource.loop = true;
        if (!fireLoopSource.isPlaying)
        {
            fireLoopSource.Play();
        }
    }

    private void StopFireAudio()
    {
        if (fireLoopSource != null)
        {
            fireLoopSource.Stop();
        }
    }

    private void PlayPlayerAudio(int playerIndex, AudioClip clip)
    {
        if (clip == null || FindAnyObjectByType<PlayerAudioManager>() == null)
        {
            return;
        }

        WhalesongAudio.PlayOneShot(playerIndex, clip);
    }

    private void PlayGlobalAudio(AudioClip clip)
    {
        if (clip == null || FindAnyObjectByType<PlayerAudioManager>() == null)
        {
            return;
        }

        WhalesongAudio.PlayGlobalOneShot(clip);
    }

    private void CompleteMinigame(bool didWin)
    {
        if (gameEnded)
        {
            return;
        }

        gameEnded = true;
        StopFireAudio();
        SetFireAnimations(false);
        OnGameComplete?.Invoke(didWin);
    }

    private void ResetFire()
    {
        currentCharges = 0f;
        timeInEmber = 0f;
        survivalTime = 0f;
        gameEnded = false;
        activePlayerCount = PlayerUtilities.GetActivePlayerCount();
        maxCharges = chargesPerPlayer * activePlayerCount;
        SetFireState(FireState.Ember, true);
        StopFireAudio();
        SetFireAnimations(false);
    }
}

