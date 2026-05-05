using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.XR.Interaction.Toolkit.Samples.StarterAssets;
using UnityEngine.XR.Interaction.Toolkit.Samples.ARStarterAssets;

/// <summary>
/// AR Battle Manager - the Cancel Button is permanently repurposed as START BATTLE.
/// Shows "START BATTLE" immediately when the Object Menu opens.
/// Player places a robot, then taps START BATTLE to begin the automated sequence.
/// </summary>
public class ARBattleManager : MonoBehaviour
{
    [Header("Core References")]
    public ObjectSpawner spawner;
    public GameObject arDrawerUI;
    public GameObject arSession;      // AR Session to disable during battle
    public Camera arCamera;          // AR Camera to disable during battle

    [Header("UI Elements")]
    public CanvasGroup fadeScreen;
    public TextMeshProUGUI opponentText;
    public TextMeshProUGUI battleLogText;
    public Button cancelButton;  // The "Cancel Button" is repurposed as START BATTLE trigger

    [Header("Battle Settings")]
    public float rouletteDuration = 2.5f;
    public float lineDelay = 0.8f;

    // Battle state
    private int playerRobotIndex = 0;  // 0 = SCRAP-O-TRON, 1 = IRON-GEAR PRIME
    private bool battleStarted = false;
    private bool buttonActivated = false;
    private bool spawnLockActive = false;        // True after first robot placed
    private List<GameObject> spawnedRobots = new List<GameObject>();

    // Spawn-lock components discovered at runtime
    private List<Button> robotMenuButtons = new List<Button>();   // The robot selection buttons
    private ARInteractorSpawnTrigger spawnTrigger;                // Hardware spawn trigger

    private readonly string[] robotNames = { "SCRAP-O-TRON", "IRON-GEAR PRIME" };
    private readonly string[][] robotSkills = {
        new string[] { "Punch", "Slam", "Charge Attack" },
        new string[] { "Laser Beam", "Rocket Fist", "Plasma Strike" }
    };
    private readonly string[] robotColors = { "#FF4444", "#5599FF" };

    // ── Smart rolling dialogue buffer ────────────────────────────────────
    private readonly List<string> _lines = new List<string>();
    private const int MAX_LINES = 12;  // Max visible lines before oldest scroll off

    // ── Cached native-UI refs (found once in Start) ───────────────────────
    private GameObject _createBtnGO;
    private GameObject _deleteBtnGO;

    // ── Pending reward (sent after WebView is shown) ──────────────────────
    private UnityBridge.BattleRewardPayload _pendingReward;

    private void Start()
    {
        // Hide battle UI elements initially
        if (opponentText != null) opponentText.gameObject.SetActive(false);
        if (battleLogText != null) battleLogText.gameObject.SetActive(false);

        // Auto-find AR Session and Camera if not assigned
        if (arSession == null) arSession = GameObject.Find("AR Session");
        if (arCamera == null)
        {
            var camGO = GameObject.Find("AR Camera");
            if (camGO != null) arCamera = camGO.GetComponent<Camera>();
        }

        // Configure battle log text layout: full-width, lower 70% of screen
        SetupBattleLogLayout();

        // Wire Cancel Button as START BATTLE and style it
        WireCancelButton();
        SetupStartBattleButtonStyle();

        ShowStatus("AR Battle Ready");

        // Cache native UI refs once (avoid Find() during battle)
        _createBtnGO = GameObject.Find("UI/Create Button");
        _deleteBtnGO = GameObject.Find("UI/Delete Button");

        // Track spawned robots via event
        WireSpawnerEvent();

        // Discover and cache spawn-lock components procedurally
        FindSpawnLockComponents();
    }

    /// <summary>Configures battleLogText: centered on screen, wider, bigger font.</summary>
    private void SetupBattleLogLayout()
    {
        if (battleLogText == null) return;
        var rt = battleLogText.rectTransform;
        // Anchored to center vertical band (30%–85% of screen height)
        rt.anchorMin     = new Vector2(0.04f, 0.22f);
        rt.anchorMax     = new Vector2(0.96f, 0.82f);
        rt.offsetMin     = Vector2.zero;
        rt.offsetMax     = Vector2.zero;
        battleLogText.alignment          = TextAlignmentOptions.TopLeft;
        battleLogText.enableWordWrapping = true;
        battleLogText.fontSize           = 26f;
        battleLogText.lineSpacing        = 4f;
        battleLogText.enableAutoSizing   = false;
        battleLogText.overflowMode       = TextOverflowModes.Overflow;
    }

    /// <summary>Styles the Cancel/START BATTLE button as a glowing pill.</summary>
    private void SetupStartBattleButtonStyle()
    {
        if (cancelButton == null) return;

        // Pill dimensions: wide enough for text, tall enough for visual weight
        var rt = cancelButton.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.sizeDelta = new Vector2(300f, 58f);
        }

        // Vivid red-orange battle color
        var colors = cancelButton.colors;
        colors.normalColor      = new Color(0.90f, 0.25f, 0.15f, 1f);  // battle red
        colors.highlightedColor = new Color(1.00f, 0.35f, 0.10f, 1f);
        colors.pressedColor     = new Color(0.70f, 0.15f, 0.05f, 1f);
        cancelButton.colors = colors;

        var img = cancelButton.GetComponent<Image>();
        if (img != null) img.color = new Color(0.90f, 0.25f, 0.15f, 1f);

        // Add a glow Image sibling rendered behind this button
        AddButtonGlow();

        // Kick off subtle pulse coroutine
        StartCoroutine(PulseStartButton());
    }

    private void AddButtonGlow()
    {
        if (cancelButton == null) return;
        var parent = cancelButton.transform.parent;
        if (parent == null) return;

        // Avoid duplicates
        if (parent.Find("BTN_Glow") != null) return;

        var glowGO  = new GameObject("BTN_Glow");
        glowGO.transform.SetParent(parent, false);
        glowGO.transform.SetSiblingIndex(cancelButton.transform.GetSiblingIndex()); // behind button
        glowGO.layer = cancelButton.gameObject.layer;

        var glowRT          = glowGO.AddComponent<RectTransform>();
        var btnRT           = cancelButton.GetComponent<RectTransform>();
        glowRT.anchorMin    = btnRT.anchorMin;
        glowRT.anchorMax    = btnRT.anchorMax;
        glowRT.anchoredPosition = btnRT.anchoredPosition;
        glowRT.sizeDelta    = new Vector2(btnRT.sizeDelta.x + 40f, btnRT.sizeDelta.y + 30f);
        glowRT.pivot        = btnRT.pivot;

        var glowImg         = glowGO.AddComponent<Image>();
        glowImg.color       = new Color(1f, 0.25f, 0.05f, 0.45f);
        glowImg.raycastTarget = false;
    }

    private IEnumerator PulseStartButton()
    {
        if (cancelButton == null) yield break;
        var glowImg = cancelButton.transform.parent?.Find("BTN_Glow")?.GetComponent<Image>();
        float t = 0f;
        while (cancelButton != null && cancelButton.gameObject != null)
        {
            t += Time.deltaTime * 1.5f;
            float alpha = Mathf.Lerp(0.20f, 0.55f, (Mathf.Sin(t) + 1f) * 0.5f);
            if (glowImg != null)
                glowImg.color = new Color(1f, 0.25f, 0.05f, alpha);
            yield return null;
        }
    }
    
    private void Update()
    {
        // BACKUP: Poll for robot spawn if the objectSpawned event fails (Android safety net).
        // NOTE: spawnAsChildren=false, so spawner.childCount is always 0.
        // Instead we poll via the spawner's objectSpawned event — see WireSpawnerEvent().
        // No polling needed here; the event is the authoritative detection path.
    }

    // ─── Spawn-Lock System ──────────────────────────────────────────────────

    /// <summary>
    /// Procedurally discovers the robot selection buttons inside the Object Menu's
    /// Scroll View Content, and caches the ARInteractorSpawnTrigger.
    /// Call once on Start() — works even if buttons are added/removed from the prefab.
    /// </summary>
    private void FindSpawnLockComponents()
    {
        // 1. Find the ARInteractorSpawnTrigger that gates actual tap-to-spawn
        if (spawnTrigger == null)
            spawnTrigger = FindObjectOfType<ARInteractorSpawnTrigger>();

        // 2. Find robot selection buttons procedurally via Scroll View Content
        robotMenuButtons.Clear();
        var contentGO = GameObject.Find(
            "UI/Object Menu Animator/Object Menu/Scroll View/Viewport/Content");
        if (contentGO != null)
        {
            // Every direct child of Content is a robot selection button
            foreach (Transform child in contentGO.transform)
            {
                var btn = child.GetComponent<Button>();
                if (btn != null)
                {
                    robotMenuButtons.Add(btn);
                    Debug.Log($"[ARBattleManager] Registered robot button: {child.name}");
                }
            }
        }
        else
        {
            Debug.LogWarning("[ARBattleManager] Could not find robot button Content container.");
        }

        Debug.Log($"[ARBattleManager] SpawnLock ready. {robotMenuButtons.Count} robot buttons, " +
                  $"spawnTrigger={(spawnTrigger != null ? "OK" : "NULL")}");
    }

    /// <summary>
    /// Locks spawning after the first robot is placed.
    /// Disables the ObjectSpawner + ARInteractorSpawnTrigger (hard block)
    /// and greys out all robot selection buttons (visual feedback).
    /// </summary>
    private void LockSpawning()
    {
        if (spawnLockActive) return;   // Idempotent guard
        spawnLockActive = true;

        // Hard block 1: Disable the ObjectSpawner component
        if (spawner != null)
        {
            spawner.enabled = false;
            Debug.Log("[ARBattleManager] ObjectSpawner DISABLED — no more robots allowed.");
        }

        // Hard block 2: Disable the ARInteractorSpawnTrigger
        if (spawnTrigger != null)
        {
            spawnTrigger.enabled = false;
            Debug.Log("[ARBattleManager] ARInteractorSpawnTrigger DISABLED.");
        }

        // Visual: Grey out every robot selection button in the menu
        foreach (var btn in robotMenuButtons)
        {
            if (btn != null)
            {
                btn.interactable = false;
                Debug.Log($"[ARBattleManager] Greyed out button: {btn.name}");
            }
        }

        ShowStatus("Robot placed! Tap START BATTLE!");
    }

    /// <summary>
    /// Unlocks spawning for the next battle round.
    /// Re-enables ObjectSpawner, ARInteractorSpawnTrigger, and all robot buttons.
    /// </summary>
    private void UnlockSpawning()
    {
        spawnLockActive = false;

        // Re-enable hard blocks
        if (spawner != null) spawner.enabled = true;
        if (spawnTrigger != null) spawnTrigger.enabled = true;

        // Re-enable and restore robot selection buttons
        foreach (var btn in robotMenuButtons)
        {
            if (btn != null)
                btn.interactable = true;
        }

        Debug.Log("[ARBattleManager] Spawning UNLOCKED — ready for next battle.");
    }

    private void WireCancelButton()
    {
        // Try to find Cancel Button by path (child of Object Menu)
        if (cancelButton == null)
        {
            var cancelGO = GameObject.Find("UI/Object Menu Animator/Object Menu/Cancel Button");
            if (cancelGO != null)
            {
                cancelButton = cancelGO.GetComponent<Button>();
                ShowStatus("Cancel Button Found");
            }
            else
            {
                ShowStatus("ERROR: Cancel Button Not Found!");
            }
        }

        if (cancelButton != null)
        {
            // Clear any existing listeners and wire up the battle trigger
            cancelButton.onClick.RemoveAllListeners();
            cancelButton.onClick.AddListener(OnCancelButtonClicked);
            
            // IMMEDIATELY set text to "START BATTLE" — this button is permanently repurposed
            // Expand the text RectTransform to fill the button so the full text is never clipped
            SetButtonText("START BATTLE");
            
            ShowStatus("START BATTLE Ready!");
        }
        else
        {
            ShowStatus("ERROR: Button Not Wired!");
        }
    }

    /// <summary>
    /// Finds the first TextMeshProUGUI child on the cancelButton and sets its text.
    /// Also expands the child RectTransform to fill the button so nothing is clipped.
    /// </summary>
    private void SetButtonText(string newText)
    {
        if (cancelButton == null) return;

        // Iterate ALL children, not just ones named "Text" — robust against rename
        foreach (Transform child in cancelButton.transform)
        {
            var textComp = child.GetComponent<TextMeshProUGUI>();
            if (textComp != null)
            {
                textComp.text = newText;

                // Stretch the text rect to fill the button so no clipping occurs
                var rt = child.GetComponent<RectTransform>();
                if (rt != null)
                {
                    rt.anchorMin = Vector2.zero;
                    rt.anchorMax = Vector2.one;
                    rt.offsetMin = Vector2.zero;  // left/bottom margin = 0
                    rt.offsetMax = Vector2.zero;  // right/top margin = 0
                }

                // Enable auto-sizing so the font scales to fit if needed
                textComp.enableAutoSizing = true;
                textComp.fontSizeMin = 14f;
                textComp.fontSizeMax = 32f;
                textComp.enableWordWrapping = false;
                textComp.overflowMode = TMPro.TextOverflowModes.Overflow;

                textComp.SetAllDirty();
                break;
            }
        }
    }

    private void WireSpawnerEvent()
    {
        if (spawner == null) return;

        // objectSpawned is a C# event Action<GameObject> — subscribe directly (NOT via reflection/UnityEvent)
        spawner.objectSpawned += OnRobotSpawned;
        Debug.Log("[ARBattleManager] Subscribed to ObjectSpawner.objectSpawned.");
    }

    private void OnDestroy()
    {
        if (cancelButton != null)
            cancelButton.onClick.RemoveListener(OnCancelButtonClicked);

        // Unsubscribe from the C# event
        if (spawner != null)
            spawner.objectSpawned -= OnRobotSpawned;
    }

    // objectSpawned fires with the spawned GameObject — signature must match Action<GameObject>
    private void OnRobotSpawned(GameObject spawnedGO)
    {
        if (buttonActivated) return;   // Already fired — ignore duplicates

        // Track the spawned robot (required for the battle trigger guard)
        if (spawnedGO != null)
            spawnedRobots.Add(spawnedGO);

        // Record which robot the player chose
        if (spawner != null)
            playerRobotIndex = spawner.spawnOptionIndex;

        buttonActivated = true;

        // Lock spawning immediately — one robot maximum
        LockSpawning();

        Debug.Log($"[ARBattleManager] Robot placed (index={playerRobotIndex}). spawnedRobots.Count={spawnedRobots.Count}. Spawn locked.");
    }
    
    private void ShowStatus(string message)
    {
        Debug.Log($"[ARBattleManager] {message}");
    }
    
    private void BringBattleUIToFront()
    {
        // Move battle UI elements to front by setting their Canvas sort order high
        if (fadeScreen != null)
        {
            var canvas = fadeScreen.GetComponentInParent<Canvas>();
            if (canvas != null)
                canvas.sortingOrder = 9999;
        }
        
        if (opponentText != null)
        {
            var canvas = opponentText.GetComponentInParent<Canvas>();
            if (canvas != null)
                canvas.sortingOrder = 10000;
        }
        
        if (battleLogText != null)
        {
            var canvas = battleLogText.GetComponentInParent<Canvas>();
            if (canvas != null)
                canvas.sortingOrder = 10001;
        }
    }

    // Called when Cancel Button is clicked - THIS STARTS THE BATTLE
    public void OnCancelButtonClicked()
    {
        if (battleStarted) return;
        if (spawnedRobots.Count == 0) return;

        battleStarted = true;

        // ── Hide ALL native AR UI immediately ────────────────────────────
        if (arDrawerUI != null) arDrawerUI.SetActive(false);
        if (cancelButton != null) cancelButton.gameObject.SetActive(false);
        if (_createBtnGO != null) _createBtnGO.SetActive(false);
        if (_deleteBtnGO != null) _deleteBtnGO.SetActive(false);
        cancelButton?.transform.parent?.Find("BTN_Glow")?.gameObject.SetActive(false);

        StartCoroutine(ExecuteBattleSequence());
    }

    private IEnumerator ExecuteBattleSequence()
    {
        // DISABLE AR Session and AR Camera silently - battle is UI overlay only
        if (arSession != null)
            arSession.SetActive(false);
        if (arCamera != null)
            arCamera.gameObject.SetActive(false);
        
        // Ensure battle UI is on top of everything
        BringBattleUIToFront();
        
        // ── PHASE 1: Fade to black (2s) ─────────────────────────────────
        if (fadeScreen != null)
        {
            float elapsed = 0f;
            while (elapsed < 2f)
            {
                elapsed += Time.deltaTime;
                fadeScreen.alpha = Mathf.Lerp(0f, 1f, elapsed / 2f);
                yield return null;
            }
            fadeScreen.alpha = 1f;
        }

        yield return new WaitForSeconds(0.4f);

        // ── PHASE 2: Roulette opponent selection ─────────────────────────
        int enemyIndex = Random.Range(0, robotNames.Length);

        // Stat-based win probability:
        // IRON-GEAR PRIME (idx 1) is stronger than SCRAP-O-TRON (idx 0)
        float winChance = 0.5f;
        if (playerRobotIndex == 0 && enemyIndex == 1) winChance = 0.28f; // weak vs strong
        else if (playerRobotIndex == 1 && enemyIndex == 0) winChance = 0.72f; // strong vs weak

        bool isDraw     = (Random.value < 0.08f);        // 8% draw
        bool playerWins = !isDraw && (Random.value < winChance);
        
        if (opponentText != null)
        {
            opponentText.gameObject.SetActive(true);
            
            float elapsed = 0f;
            int current = 0;
            while (elapsed < rouletteDuration)
            {
                current = (current + 1) % robotNames.Length;
                opponentText.text = $"Opponent: <color={robotColors[current]}>{robotNames[current]}</color>";
                elapsed += 0.1f;
                yield return new WaitForSeconds(0.1f);
            }
            
            opponentText.text = $"Opponent: <color={robotColors[enemyIndex]}>{robotNames[enemyIndex]}</color>";
            yield return new WaitForSeconds(1f);
            opponentText.gameObject.SetActive(false);
        }

        yield return new WaitForSeconds(0.3f);

        // ── PHASE 3: Battle dialogue ─────────────────────────────────────
        if (battleLogText != null)
        {
            battleLogText.text = "";
            battleLogText.gameObject.SetActive(true);
            yield return StartCoroutine(PlayBattleDialogue(playerRobotIndex, enemyIndex, playerWins, isDraw));
        }

        // ── PHASE 4: Show reward card (4 s pause inside) ─────────────────
        yield return StartCoroutine(ShowRewardCard(playerWins, isDraw));

        // ── PHASE 5: Brief hold then hide battle UI ───────────────────────
        yield return new WaitForSeconds(0.8f);
        if (battleLogText != null) battleLogText.gameObject.SetActive(false);
        if (opponentText  != null) opponentText.gameObject.SetActive(false);

        // ── PHASE 6: Fade back to white ───────────────────────────────────
        if (fadeScreen != null)
        {
            float elapsed = 0f;
            while (elapsed < 1.2f)
            {
                elapsed += Time.deltaTime;
                fadeScreen.alpha = Mathf.Lerp(1f, 0f, elapsed / 1.2f);
                yield return null;
            }
            fadeScreen.alpha = 0f;
        }

        // ── PHASE 7: Show WebView, THEN flush reward
        if (WebViewManager.Instance != null)
        {
            WebViewManager.Instance.ShowWebView();
            WebViewManager.Instance.NavigateToBattleLanding();
        }
        yield return new WaitForSeconds(0.4f);
        FlushRewardToReact();

        // ── PHASE 8: Reset local state ────────────────────────────────────
        ResetBattle();
    }

    private IEnumerator PlayBattleDialogue(int playerIndex, int enemyIndex, bool playerWins, bool isDraw)
    {
        string pc    = robotColors[playerIndex];
        string ec    = robotColors[enemyIndex];
        string pName = $"<color={pc}>{robotNames[playerIndex]}</color>";
        string eName = $"<color={ec}>{robotNames[enemyIndex]}</color>";

        // Stat-adjusted damage: IRON-GEAR PRIME hits harder
        int pBase = (playerIndex == 1) ? 22 : 16;  // IRON-GEAR PRIME base
        int eBase = (enemyIndex  == 1) ? 22 : 16;
        int p1dmg = pBase + Random.Range(0, 12);
        int e1dmg = eBase + Random.Range(0, 12);
        int p2dmg = pBase + Random.Range(0, 10);
        int e2dmg = eBase + Random.Range(0, 14);
        int p3dmg = playerWins ? pBase + Random.Range(15, 28) : Random.Range(4, 12);

        string pSkill1 = robotSkills[playerIndex][Random.Range(0, robotSkills[playerIndex].Length)];
        string eSkill1 = robotSkills[enemyIndex] [Random.Range(0, robotSkills[enemyIndex].Length)];
        string pSkill2 = robotSkills[playerIndex][Random.Range(0, robotSkills[playerIndex].Length)];
        string eSkill2 = robotSkills[enemyIndex] [Random.Range(0, robotSkills[enemyIndex].Length)];
        string pSkill3 = robotSkills[playerIndex][Random.Range(0, robotSkills[playerIndex].Length)];

        _lines.Clear();

        // --- Entrance ---
        yield return StartCoroutine(AppendLine($">> {pName} <color=white>enters the arena!</color>"));
        yield return StartCoroutine(AppendLine($">> {eName} <color=white>materializes!</color>"));
        yield return new WaitForSeconds(0.7f);

        // --- Round 1 ---
        yield return StartCoroutine(AppendLine(""));
        yield return StartCoroutine(AppendLine("<color=white><size=118%><b>--- ROUND 1 ---</b></size></color>"));
        yield return StartCoroutine(AppendLine($"> {pName} uses <color={pc}><b>{pSkill1}</b></color>!"));
        yield return StartCoroutine(AppendLine($"  <color=#FFA500>Direct hit!  -{p1dmg} HP</color>"));
        yield return new WaitForSeconds(0.5f);
        yield return StartCoroutine(AppendLine($"< {eName} fires back with <color={ec}><b>{eSkill1}</b></color>!"));
        yield return StartCoroutine(AppendLine($"  <color=#FF6666>Critical hit!  -{e1dmg} HP</color>"));
        yield return new WaitForSeconds(0.6f);

        // --- Round 2 ---
        yield return StartCoroutine(AppendLine(""));
        yield return StartCoroutine(AppendLine("<color=white><size=118%><b>--- ROUND 2 ---</b></size></color>"));
        yield return StartCoroutine(AppendLine($"> {pName} counters with <color={pc}><b>{pSkill2}</b></color>!"));
        yield return StartCoroutine(AppendLine($"  <color=#FFA500>Glancing blow!  -{p2dmg} HP</color>"));
        yield return new WaitForSeconds(0.5f);
        yield return StartCoroutine(AppendLine($"< {eName} retaliates — <color={ec}><b>{eSkill2}</b></color>!"));
        yield return StartCoroutine(AppendLine($"  <color=#FF6666>Heavy hit!  -{e2dmg} HP</color>"));
        yield return new WaitForSeconds(0.7f);

        // --- Round 3 ---
        yield return StartCoroutine(AppendLine(""));
        yield return StartCoroutine(AppendLine("<color=white><size=118%><b>--- ROUND 3 ---</b></size></color>"));
        yield return StartCoroutine(AppendLine($"> {pName} charges up <color={pc}><b>{pSkill3}</b></color>!"));

        // --- Outcome ---
        if (playerWins && !isDraw)
        {
            yield return StartCoroutine(AppendLine($"  <color=#FFD700>** FINISHING BLOW!  -{p3dmg} HP **</color>"));
            yield return new WaitForSeconds(1.0f);
            yield return StartCoroutine(AppendLine(""));
            yield return StartCoroutine(AppendLine($"<color=#FFD700><size=125%><b>{pName} WINS!</b></size></color>"));
            yield return new WaitForSeconds(0.5f);
            yield return StartCoroutine(AppendLine("<color=#00FF88><size=145%><b>** VICTORY! **</b></size></color>"));
        }
        else if (isDraw)
        {
            yield return new WaitForSeconds(0.8f);
            yield return StartCoroutine(AppendLine(""));
            yield return StartCoroutine(AppendLine("<color=white>Both fighters are exhausted!</color>"));
            yield return new WaitForSeconds(0.4f);
            yield return StartCoroutine(AppendLine("<color=#FFD700><size=150%><b>⚖ DRAW! ⚖</b></size></color>"));
        }
        else
        {
            yield return StartCoroutine(AppendLine($"  <color=#888888>Not enough power... <b>-{p2dmg} HP</b></color>"));
            yield return new WaitForSeconds(0.5f);
            yield return StartCoroutine(AppendLine($"◀ {eName} delivers the <b>final blow</b>!"));
            yield return new WaitForSeconds(0.4f);
            yield return StartCoroutine(AppendLine(""));
            yield return StartCoroutine(AppendLine("<color=#FF4444><size=150%><b>💀 DEFEAT! 💀</b></size></color>"));
        }
    }

    /// <summary>
    /// Appends one line to the battle log text with the configured delay.
    /// Empty string appends just a newline with a shorter pause.
    /// </summary>
    /// <summary>
    /// Smart rolling line appender.
    /// Adds a line to the rolling buffer (max MAX_LINES), rebuilds TMP text,
    /// and auto-scrolls by trimming the oldest lines when the buffer is full.
    /// </summary>
    private IEnumerator AppendLine(string line)
    {
        if (battleLogText == null) yield break;

        _lines.Add(string.IsNullOrEmpty(line) ? "" : line);

        // Trim oldest lines when over limit (auto-scroll effect)
        while (_lines.Count > MAX_LINES)
            _lines.RemoveAt(0);

        // Rebuild text from buffer
        battleLogText.text = string.Join("\n", _lines);

        float pause = string.IsNullOrEmpty(line) ? 0.15f : lineDelay;
        yield return new WaitForSeconds(pause);
    }

    /// <summary>
    /// Shows the material reward card in the dialogue and stores the payload.
    /// Does NOT send to React yet — FlushRewardToReact() does that after WebView is shown.
    /// </summary>
    private IEnumerator ShowRewardCard(bool playerWon, bool isDraw)
    {
        int plastic, metal, paper;
        string resultLabel, sign, matColor;

        if (isDraw)
        {
            plastic = Random.Range(1, 4); metal = Random.Range(1, 4); paper = Random.Range(1, 4);
            resultLabel = "DRAW"; sign = "+"; matColor = "#FFD700";
        }
        else if (playerWon)
        {
            plastic = Random.Range(5, 11); metal = Random.Range(5, 11); paper = Random.Range(5, 11);
            resultLabel = "VICTORY"; sign = "+"; matColor = "#00FF88";
        }
        else
        {
            plastic = Random.Range(1, 6); metal = Random.Range(1, 6); paper = Random.Range(1, 6);
            resultLabel = "DEFEAT"; sign = "-"; matColor = "#FF4444";
        }

        int pAmt = playerWon || isDraw ? plastic : -plastic;
        int mAmt = playerWon || isDraw ? metal   : -metal;
        int paAmt= playerWon || isDraw ? paper   : -paper;

        // Store for sending once WebView is visible
        _pendingReward = new UnityBridge.BattleRewardPayload
        {
            won     = playerWon,
            plastic = pAmt, metal = mAmt, paper = paAmt,
            message = $"{resultLabel}! {sign}{plastic} Plastic, {sign}{metal} Metal, {sign}{paper} Paper"
        };

        // Brief pause then show reward card
        yield return new WaitForSeconds(1.0f);
        yield return StartCoroutine(AppendLine(""));
        yield return StartCoroutine(AppendLine("<color=white>--------------------------</color>"));
        yield return StartCoroutine(AppendLine($"<color={matColor}><size=112%><b>MATERIALS {(playerWon || isDraw ? "EARNED" : "LOST")}:</b></size></color>"));
        yield return new WaitForSeconds(0.4f);
        yield return StartCoroutine(AppendLine($"  <color=#00BFFF>Plastic   <b>{sign}{plastic}</b></color>"));
        yield return StartCoroutine(AppendLine($"  <color=#C0C0C0>Metal     <b>{sign}{metal}</b></color>"));
        yield return StartCoroutine(AppendLine($"  <color=#90EE90>Paper     <b>{sign}{paper}</b></color>"));
        yield return StartCoroutine(AppendLine("<color=white>--------------------------</color>"));

        // Hold so player can read the reward — 4 seconds
        yield return new WaitForSeconds(4.0f);
    }

    /// <summary>Sends the pending reward to React (call AFTER WebView is shown).</summary>
    private void FlushRewardToReact()
    {
        if (_pendingReward == null) return;
        if (WebViewManager.Instance == null) { _pendingReward = null; return; }
        WebViewManager.Instance.SendToReact(UnityBridge.BATTLE_REWARD, _pendingReward);
        _pendingReward = null;
        Debug.Log("[ARBattleManager] Battle reward flushed to React.");
    }

    public void ResetBattle()
    {
        battleStarted    = false;
        buttonActivated  = false;
        _lines.Clear();
        spawnedRobots.Clear();
        playerRobotIndex = 0;

        if (opponentText != null) opponentText.gameObject.SetActive(false);
        if (battleLogText != null) { battleLogText.text = ""; battleLogText.gameObject.SetActive(false); }
        if (fadeScreen != null) fadeScreen.alpha = 0f;

        // Re-enable AR
        if (arSession != null) arSession.SetActive(true);
        if (arCamera != null)  arCamera.gameObject.SetActive(true);

        // Restore all native AR UI
        if (arDrawerUI != null) arDrawerUI.SetActive(true);
        if (cancelButton != null) { cancelButton.gameObject.SetActive(true); }
        var createBtn = GameObject.Find("UI/Create Button");
        if (createBtn != null) createBtn.SetActive(true);
        var deleteBtn = GameObject.Find("UI/Delete Button");
        if (deleteBtn != null) deleteBtn.SetActive(true);
        cancelButton?.transform.parent?.Find("BTN_Glow")?.gameObject.SetActive(true);

        UnlockSpawning();
        SetButtonText("START BATTLE");
    }

    /// <summary>
    /// Called by BackButtonHandler when leaving AR mid-session.
    /// Destroys any placed robots and fully resets state so the next session starts clean.
    /// </summary>
    public void CleanupOnExit()
    {
        // Stop any running battle coroutines
        StopAllCoroutines();

        // Destroy all placed robot GameObjects
        foreach (var robot in spawnedRobots)
            if (robot != null) Destroy(robot);

        // Full state reset (re-enables spawner, UI, etc.)
        ResetBattle();

        Debug.Log("[ARBattleManager] CleanupOnExit — robots destroyed, state reset.");
    }
}
