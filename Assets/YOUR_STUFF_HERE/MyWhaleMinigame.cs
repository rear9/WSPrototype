using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MyWhaleMinigame : MinigameBase
{ 
    // define variables [player sprite renderers, score timer,
    // FSM table for fire states, 3 (max 4) states exposed audio reference table (mini audiomanager),
    // int variables (min-max for 10-12)
    // charges, defined time limit per charges
    
    // AudioManager
    // Fire burning (flame/crackling), wind,
    // placing logs, throwing logs, sparks, player joining)
    
    public override GameScoreData GetScoreData()
    {
        //Here's an example of how you might generate scores
        int teamTime = 0;
        GameScoreData gsd = new GameScoreData();
        for (int i = 0; i < 4; i++)
        {
            if (PlayerUtilities.GetPlayerState(i) == Player.PlayerState.ACTIVE)
            {
                gsd.PlayerScores[i] = 1;                        //Each player scored one point
                gsd.PlayerTimes[i] = gsd.PlayerScores[i] * 2;   //Each player gets two seconds per point scored
                teamTime += gsd.PlayerTimes[i];                 //Keep a running total of the total time scored by all players
            }
        }
        gsd.ScoreSuffix = " points";    //This lets you write something after the player's score.
        gsd.TeamTime = teamTime;
        return gsd;
    }

    /// <summary>
    /// How do you want to handle input from the four directional buttons?
    /// </summary>
    /// <param name="playerIndex">Which player (0-3) pressed the button</param>
    /// <param name="direction">Which direction(s) are they pressing</param>
    public override void OnDirectionalInput(int playerIndex, Vector2 direction)
    {
        // do nothing
    }
    /// <summary>
    /// What should happen when the player presses the left hand button?
    /// </summary>
    /// <param name="playerIndex">Which player (0-3) pressed the button</param>
    public override void OnPrimaryFire(int playerIndex)
    {
        
        // fire things
        
        // colour changing (hue system)
        // colours are presets based on colour-blindness palette
    }

    /// <summary>
    /// What should happen when the player presses the right hand button?
    /// </summary>
    /// <param name="playerIndex">Which player (0-3) pressed the button</param>
    public override void OnSecondaryFire(int playerIndex)
    {
        
       // make fire go up
       
       // add a charge (max 12), after 3/4 charges, change state from FSM
    }

    public override void TimeUp()
    {
        
    }

    protected override void OnResetGame()
    {
        
    }
}
