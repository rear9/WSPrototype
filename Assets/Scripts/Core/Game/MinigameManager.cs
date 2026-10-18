using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static GameManager;

[System.Serializable]
public class GameTimer
{
    public float m_TimerMax = 10.0f;
    private float m_CurrentTime;
    public float TotalTime{
        get;
        private set;
    }

    public bool IsRunning { get; private set; } = true;

    public GameTimer(float TimerMax)
    {
        m_CurrentTime = m_TimerMax = TimerMax;
        TotalTime = 0.0f;
    }

    public void ResetClock()
    {
        m_CurrentTime = m_TimerMax = TimerMax;
        TotalTime = 0.0f;
    }

    public float Tick(float dt)
    {
        if (IsRunning)
        {
            m_CurrentTime = Mathf.Clamp(m_CurrentTime - dt, 0, m_TimerMax);
            TotalTime += dt;
        }
        return m_CurrentTime;
    }
    public void AddTime(float secondsToAdd)
    {
        m_CurrentTime += secondsToAdd;
    }

    public void PauseTimer()
    {
        IsRunning = false;
    }
    public void ResumeTimer()
    {
        IsRunning = true;
    }

    public float CurrentTime
    {
        get { return m_CurrentTime; }
    }
    public float TimerMax
    {
        get { return m_TimerMax; }
    }
}

public class MinigameManager : MonoBehaviour
{
    //Events
    public System.Action OnMinigameStart;
    public System.Action OnMinigameQuit;

    public System.Action<float> OnIncreaseTimer; //TODO: Move these somewhere else?
    public System.Action<bool> OnTimerChange;

    public System.Action PauseTimer;
    public System.Action UnpauseTimer;


    public Animator[] goPromptAnimators;
    public AudioClip goSFXClip;

    private List<MinigameBase>  minigames = new List<MinigameBase>();
    private List<MinigameBase>  minigameQueue = new List<MinigameBase>();
    private MinigameBase        currentMinigame;
    private int                 minigameIndex = -1; //Starts at -1 so we advance to 0
    private GameTimer           m_Timer;
    private bool                minigamesPopulated;
    [SerializeField] WinScreen m_WinScreen;

    public int MinigamesCompleted{
        get; private set;
    }


    //Public
    public void IncreaseWins()
    {
        MinigamesCompleted++;
    }

    //Properties
    public string CurrentGameName
    {
        get
        {
            if (currentMinigame != null)
            {
                return currentMinigame.name;
            }
            else
            {
                return "null";
            }
        }
    }
    public bool MinigameLoaded
    {
        get { return currentMinigame != null; }
    }
    public bool MinigameActive
    {
        get
        {
            if (currentMinigame != null)
            {
                return currentMinigame.m_IsMinigameActive;
            }
            return false;
        }
    }

    public bool CurrentMinigameSupportsLateJoining => currentMinigame != null && currentMinigame.SupportsLateJoining;
    public bool CurrentMinigameOwnsJoinPrompt(GameObject prompt) => currentMinigame != null && currentMinigame.OwnsJoinPrompt(prompt);
    public bool UsesCustomJoinFlow
    {
        get
        {
            if (currentMinigame != null)
            {
                return currentMinigame.UsesCustomJoinFlow;
            }
            if (!minigamesPopulated)
            {
                PopulateMinigames();
            }
            return minigames.Count > 0 && minigames.All(minigame => minigame.UsesCustomJoinFlow);
        }
    }

    void Start()
    {
        PopulateMinigames();
        for (int i = 0; i < minigames.Count; i++)
        {
            //minigames[i].gameManager = this;
            minigames[i].OnGameComplete = HandleMinigameComplete;
            //minigames[i].OnGameClose = HandleMinigameClose;
            //minigames[i].OnMinigameFinished = HandleMinigameComplete;
            minigames[i].OnIncreaseTimer += HandleIncreaseTimer;
            minigames[i].OnTimerChange += HandleTimerChange;
        }
    }
    private void PopulateMinigames()
    {
        if (minigamesPopulated)
        {
            return;
        }
        minigamesPopulated = true;
        for(int i=0; i < transform.childCount; i++)
        {
            MinigameBase game = transform.GetChild(i).GetComponent<MinigameBase>();
            if (game!=null)
            {
                minigames.Add(game);
            }
        }
    }

    private void LateUpdate()
    {
        if (currentMinigame != null)
        {
            float tRemaining = 0.0f;
            if (currentMinigame.m_IsMinigameActive && currentMinigame.UsesGlobalRoundTimer)
            {
                tRemaining = m_Timer.Tick(Time.deltaTime);
                UIManager.SetTimer(m_Timer.CurrentTime, m_Timer.TimerMax);
                if (Mathf.Approximately(0.0f, tRemaining))
                {
                    TimeUp();
                }
            }            
        }
    }

    /// <summary>
    /// The global or local timer is up and the minigame is over
    /// </summary>
    private void TimeUp()
    {
        Debug.Log("Time is up");
        currentMinigame.TimeUp();
    }

    public void PassDirectionalInput(int playerIndex, Vector2 direction)
    {
        currentMinigame?.OnDirectionalInput(playerIndex, direction);
    }
    public void PassButtonInput(int playerIndex, int button)
    {
        if(button == 0)
        {
            currentMinigame.OnPrimaryFire(playerIndex);
        }
        else
        {
            currentMinigame.OnSecondaryFire(playerIndex);
        }
    }

    /// <summary>
    /// Plays the tutorial
    /// </summary>
    public void PlayTutorial()
    {
        currentMinigame.PlayTutorial(MinigameStart);
    }

    /// <summary>
    /// Actually starts the minigame
    /// </summary>
    public void MinigameStart()
    {
        bool customJoinFlow = currentMinigame != null && currentMinigame.UsesCustomJoinFlow;
        if (!customJoinFlow)
        {
            UnpauseTimer();
            UIManager.SetTimerVisible(true);
            UIManager.SetWaitBannerVisible(false);
        }
        else
        {
            UIManager.SetTimerVisible(false);
            UIManager.SetAllBannersVisible(false);
        }
        currentMinigame.StartMinigame();
        if (!customJoinFlow)
        {
            for (int i = 0; i < goPromptAnimators.Length; i++)
            {
                goPromptAnimators[i].SetTrigger("PlayPrompt");
            }
            PlayerAudioManager.PlayGlobalOneShot(goSFXClip);
        }
        OnMinigameStart?.Invoke();
    }

    public void Initialise() //TODO: I think this should be rolled into ResetProgress or vice versa
    {
        minigameQueue = new List<MinigameBase>(minigames);
    }

    public void ResetProgress()
    {
        MinigamesCompleted = 0;
        foreach (MinigameBase minigame in minigames)
        {
            minigame.ResetMinigame();
        }
    }

    public void ForceShutdown()
    {
        if (currentMinigame != null)
        {
            if (currentMinigame.UsesCustomJoinFlow && currentMinigame.m_IsMinigameActive)
            {
                currentMinigame.EndGame();
            }
            currentMinigame.Shutdown();
            currentMinigame = null;
        }
        ResetProgress();
    }

    //TODO: This bubbling of events could be neater
    private void HandleMinigameComplete(bool didWin)
    {
        if (didWin)
        {
            MinigamesCompleted++;
        }
        PauseTimer();
        currentMinigame.EndGame();
        GameScoreData gsd = currentMinigame.GetScoreData();
        gsd.ScoreIcon = currentMinigame.m_ScoreSprite;
        m_WinScreen.ShowWinScreen(currentMinigame, gsd, HandleMinigameClose);
    }
    private void HandleMinigameClose()
    {
        currentMinigame.Shutdown();
        OnMinigameQuit();
    }
    private void HandleIncreaseTimer(float t)
    {
        if (OnIncreaseTimer != null)
        {
            OnIncreaseTimer(t);
        }
    }
    private void HandleTimerChange(bool b)
    {
        if (OnTimerChange != null)
        {
            OnTimerChange(b);
        }
    }

    public void GetAndLoadNextMinigame()
    {
        minigameIndex++;
        if (minigameIndex > minigameQueue.Count() - 1)
        {
            GenerateNewMinigameQueue();
            minigameIndex = 0;
        }
        currentMinigame = minigameQueue[minigameIndex];
        currentMinigame.LoadMiniGame();

        m_Timer = new GameTimer(currentMinigame.GetGameDuration());
    }
    private void GenerateNewMinigameQueue()
    {
        // Shuffle the minigame list to generate a new order for the next round
        // Check that the first minigame in the new order isn't the same as the last we just played
        MinigameBase prevLastMinigame = null;
        if (minigameQueue.Count > 1)
        {
            prevLastMinigame = minigameQueue[minigameQueue.Count - 1];
        }

        if (minigames.Count > 1)
        {
            // Max iteration to break out of loop just in case
            int shuffleIteration = 0;
            do
            {
                List<MinigameBase> shuffledMinigames = minigames.OrderBy(x => UnityEngine.Random.value).ToList();
                minigameQueue = shuffledMinigames;
                shuffleIteration++;

            } while (minigameQueue[0] == prevLastMinigame && shuffleIteration >= 50);
        }
    }
}
