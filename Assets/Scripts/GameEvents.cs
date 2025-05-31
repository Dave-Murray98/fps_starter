using System;
using UnityEngine;

public static class GameEvents
{

    #region Player Events

    public static event Action<float, float> OnPlayerHealthChanged;
    public static event Action OnPlayerDeath;

    #endregion


    #region Game State Events
    public static event Action OnGamePaused;
    public static event Action OnGameResumed;

    #endregion


    #region Trigger Methods
    public static void TriggerPlayerHealthChanged(float currentHealth, float maxHealth) =>
        OnPlayerHealthChanged?.Invoke(currentHealth, maxHealth);

    public static void TriggerPlayerDeath() => OnPlayerDeath?.Invoke();

    public static void TriggerGamePaused() => OnGamePaused?.Invoke();

    public static void TriggerGameResumed() => OnGameResumed?.Invoke();

    #endregion

}
