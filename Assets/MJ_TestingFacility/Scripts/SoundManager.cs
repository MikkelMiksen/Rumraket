using System.Collections;
using UnityEngine;
using FMODUnity;
using FMOD.Studio;
using MJ_TestingFacility.Scripts;

// Which enemy was hit. The names must match the labels of the FMOD
// parameter "Enemy" (Plane = 0, Bird = 1, Asteroid = 2).
[ System.Serializable]
public enum HitEnemy
{
    Plane,
    Bird,
    Asteroid
}

// Plays every sound of the game. It never decides anything about the game:
//   - UI clicks arrive through the UIFunctionalityProvider events (Init)
//   - everything else arrives through OnStateChanged, called by GameManager
//   - hits, explosions and the thruster have public methods, ready for when
//     the game has enemies and thrust input
public class SoundManager : MonoBehaviour
{
    // ---------- FMOD event paths (case-sensitive, spelled as in FMOD) ----------
    private const string EngineEvent = "event:/Rocket_Engine";
    private const string ThrusterEvent = "event:/Rocket_Thurster";
    private const string LaunchEvent = "event:/Rocket_Launch";
    private const string ExplosionEvent = "event:/Rocket_Explotion";
    private const string HitEvent = "event:/Hit";
    private const string UiSelectEvent = "event:/UI_Select";
    private const string UiHoverEvent = "event:/UI_Hover";
    private const string UiBackEvent = "event:/UI_Back";
    private const string UiPlayEvent = "event:/UI_Play";
    private const string UiLaunchEvent = "event:/UI_Launch";
    private const string UiTransitionEvent = "event:/UI_Transition";
    private const string MusicEvent = "event:/Music";
    private const string EndOfRunEvent = "event:/End_of_Run";

    // ---------- Loops: we keep the instance so we can stop them later ----------
    private EventInstance music;
    private EventInstance engine;
    private EventInstance thruster;

    private UIFunctionalityProvider ui;

    // ===================================================================
    // Setup and teardown
    // ===================================================================

    // GameManager calls this once, right after it creates the UI provider.
    // This is how we subscribe to the UI events.
    public void Init(UIFunctionalityProvider provider)
    {
        Debug.Log("SoundManager: Init called, subscribed to the UI events.");
        ui = provider;

        ui.PlayClicked += OnPlayClicked;
        ui.UpgradesClicked += OnUpgradesClicked;
        ui.LaunchClicked += OnLaunchClicked;
        ui.BackClicked += OnBackClicked;
        ui.ContinueClicked += OnContinueClicked;
        ui.UpgradeBuyClicked += OnUpgradeBuyClicked;
        ui.ButtonHovered += OnButtonHovered;
    }

    private void Start()
    {
        StartCoroutine(StartMusicWhenReady());
    }

    private void OnDestroy()
    {
        // Step 1: stop listening to the UI
        if (ui != null)
        {
            ui.PlayClicked -= OnPlayClicked;
            ui.UpgradesClicked -= OnUpgradesClicked;
            ui.LaunchClicked -= OnLaunchClicked;
            ui.BackClicked -= OnBackClicked;
            ui.ContinueClicked -= OnContinueClicked;
            ui.UpgradeBuyClicked -= OnUpgradeBuyClicked;
            ui.ButtonHovered -= OnButtonHovered;
        }

        // Step 2: loops never stop on their own, so stop and release all of them
        StopLoop(ref engine);
        StopLoop(ref thruster);
        StopLoop(ref music);
    }

    // The music starts once and keeps playing until the game is left.
    // It waits for the FMOD banks (Master + Master.strings) to finish loading.
    private IEnumerator StartMusicWhenReady()
    {
        yield return new WaitUntil(() => RuntimeManager.HaveAllBanksLoaded);

        StartLoop(ref music, MusicEvent);
    }

    // ===================================================================
    // UI clicks (subscribed in Init)
    // ===================================================================

    private void OnPlayClicked() { RuntimeManager.PlayOneShot(UiPlayEvent); }
    private void OnUpgradesClicked() { RuntimeManager.PlayOneShot(UiSelectEvent); }
    private void OnLaunchClicked() { RuntimeManager.PlayOneShot(UiLaunchEvent); }
    private void OnBackClicked() { RuntimeManager.PlayOneShot(UiBackEvent); }
    private void OnContinueClicked() { RuntimeManager.PlayOneShot(UiSelectEvent); }
    private void OnUpgradeBuyClicked(UpgradeType type) { RuntimeManager.PlayOneShot(UiSelectEvent); }
    private void OnButtonHovered() { RuntimeManager.PlayOneShot(UiHoverEvent); }

    // ===================================================================
    // Game flow (GameManager calls this from SetState)
    // ===================================================================

    public void OnStateChanged(GameState oldState, GameState newState)
    {
        switch (newState)
        {
            case GameState.LaunchPad:
            case GameState.UpgradeHub:
                // A menu page swaps in
                RuntimeManager.PlayOneShot(UiTransitionEvent);
                break;

            case GameState.Flying:
                // UI_Launch has just played, now the rocket launches
                RuntimeManager.PlayOneShot(LaunchEvent);
                StartLoop(ref engine, EngineEvent);
                break;

            case GameState.RunSummary:
                // Engine off with a fade, then the payout jingle
                StopLoop(ref engine);
                StopLoop(ref thruster);
                RuntimeManager.PlayOneShot(EndOfRunEvent);
                break;
        }
    }

    // ===================================================================
    // Not in the game yet: call these when you add the feature
    // ===================================================================

    // While the player is thrusting. It plays on top of the engine.
    public void StartThruster()
    {
        if (thruster.isValid())
        {
            return;   // already running
        }

        StartLoop(ref thruster, ThrusterEvent);
    }

    public void StopThruster()
    {
        StopLoop(ref thruster);
    }

    // The rocket explodes or is destroyed
    public void PlayExplosion()
    {
        StopLoop(ref engine);
        StopLoop(ref thruster);
        RuntimeManager.PlayOneShot(ExplosionEvent);
    }

    // A collision. The FMOD parameter "Enemy" picks the sound.
    public void PlayHit(HitEnemy enemy)
    {
        EventInstance hit = RuntimeManager.CreateInstance(HitEvent);
        hit.setParameterByNameWithLabel("Enemy", enemy.ToString());
        hit.start();
        hit.release();   // FMOD frees the instance when the one-shot has finished
    }

    // ===================================================================
    // Helpers
    // ===================================================================

    // Creates and starts a looping event. A loop that is already running is stopped first.
    private void StartLoop(ref EventInstance instance, string path)
    {
        StopLoop(ref instance);

        instance = RuntimeManager.CreateInstance(path);
        instance.start();
    }

    // Stops a loop with its fade-out and frees it. Safe to call when nothing is playing.
    private void StopLoop(ref EventInstance instance)
    {
        if (!instance.isValid())
        {
            return;
        }

        instance.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
        instance.release();
        instance.clearHandle();
    }
}