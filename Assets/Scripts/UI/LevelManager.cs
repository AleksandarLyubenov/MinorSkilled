using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using System.Collections;

public class LevelManager : MonoBehaviour
{

    public static LevelManager Instance;

    [Header("Game Objects")]
    [Tooltip("All balls in the scene that count towards the win condition.")]
    public Rigidbody[] balls;

    [Header("UI References")]
    public GameObject goalPanel;
    public GameObject controlsPanel;
    public Button playButton;
    public TextMeshProUGUI playButtonText;

    [Header("Menus")]
    public RectTransform pauseMenuPanel;
    public RectTransform levelPassedMenuPanel;
    [Header("Animation")]
    public float animationDuration = 0.4f;
    public float offScreenY = -1000f;

    public bool IsPlaying { get; private set; } = false;
    private bool isPaused = false;
    private bool levelComplete = false;

    private Vector3[] startPositions;
    private Quaternion[] startRotations;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Start()
    {
        // initial positions
        if (balls != null)
        {
            startPositions = new Vector3[balls.Length];
            startRotations = new Quaternion[balls.Length];

            for (int i = 0; i < balls.Length; i++)
            {
                if (balls[i] != null)
                {
                    startPositions[i] = balls[i].transform.position;
                    startRotations[i] = balls[i].transform.rotation;
                }
            }
        }

        FreezeBalls(true);
        if (playButtonText != null) playButtonText.text = "Play";

        // Hide Menus
        SetupMenu(pauseMenuPanel);
        SetupMenu(levelPassedMenuPanel);
    }

    void Update()
    {
        // Block pause toggle if level is finished
        if (Input.GetKeyDown(KeyCode.Escape) && !levelComplete)
        {
            TogglePauseMenu();
        }
    }
    public void OnLevelPassed()
    {
        if (levelComplete) return; // Don't trigger twice

        levelComplete = true;
        IsPlaying = false;

        Debug.Log("Level Passed!");

        FreezeBalls(true);

        // Win UI
        StartCoroutine(AnimateMenu(levelPassedMenuPanel, true));
    }

    public void OnNextLevelClicked()
    {
        Time.timeScale = 1f;
        // Loads the next scene in the Build Settings list
        int nextSceneIndex = SceneManager.GetActiveScene().buildIndex + 1;
        if (nextSceneIndex < SceneManager.sceneCountInBuildSettings)
        {
            SceneManager.LoadScene(nextSceneIndex);
        }
        else
        {
            Debug.Log("No more levels! Loading Main Menu.");
            SceneManager.LoadScene("MainMenu");
        }
    }
    public void OnPlayButtonPressed()
    {
        if (levelComplete) return; // Disable play button if won
        if (IsPlaying) ResetLevel();
        else StartLevel();
    }

    public void OnPauseRestartClicked()
    {
        TogglePauseMenu();
        ResetLevel();
    }

    public void OnPauseMenuClicked()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene("MainMenu");
    }

    void StartLevel()
    {
        IsPlaying = true;
        FreezeBalls(false);
        if (goalPanel != null) goalPanel.SetActive(false);
        if (controlsPanel != null) controlsPanel.SetActive(false);
        if (playButtonText != null) playButtonText.text = "Restart";
    }

    void ResetLevel()
    {
        IsPlaying = false;
        levelComplete = false; // Reset win state

        ResetBalls();
        FreezeBalls(true);

        if (goalPanel != null) goalPanel.SetActive(true);
        if (playButtonText != null) playButtonText.text = "Play";

        // Ensure Win Menu is hidden
        if (levelPassedMenuPanel != null)
        {
            levelPassedMenuPanel.gameObject.SetActive(false);
            levelPassedMenuPanel.anchoredPosition = new Vector2(0, offScreenY);
        }
    }

    void FreezeBalls(bool freeze)
    {
        foreach (var rb in balls)
        {
            if (rb != null)
            {
                rb.isKinematic = freeze;
                if (!freeze) rb.WakeUp();
                else
                {
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                }
            }
        }
    }

    void ResetBalls()
    {
        for (int i = 0; i < balls.Length; i++)
        {
            if (balls[i] != null)
            {
                balls[i].transform.position = startPositions[i];
                balls[i].transform.rotation = startRotations[i];
                balls[i].linearVelocity = Vector3.zero;
                balls[i].angularVelocity = Vector3.zero;
            }
        }
    }
    void SetupMenu(RectTransform menu)
    {
        if (menu != null)
        {
            menu.anchoredPosition = new Vector2(0, offScreenY);
            menu.gameObject.SetActive(false);
        }
    }

    public void TogglePauseMenu()
    {
        isPaused = !isPaused;
        Time.timeScale = isPaused ? 0f : 1f;
        StartCoroutine(AnimateMenu(pauseMenuPanel, isPaused));
    }

    IEnumerator AnimateMenu(RectTransform panel, bool showing)
    {
        if (panel == null) yield break;
        if (showing) panel.gameObject.SetActive(true);

        Vector2 hiddenPos = new Vector2(0, offScreenY);
        Vector2 centerPos = Vector2.zero;
        Vector2 start = showing ? hiddenPos : centerPos;
        Vector2 end = showing ? centerPos : hiddenPos;

        float elapsed = 0f;
        while (elapsed < animationDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / animationDuration;
            t = Mathf.SmoothStep(0f, 1f, t);
            panel.anchoredPosition = Vector2.Lerp(start, end, t);
            yield return null;
        }

        panel.anchoredPosition = end;
        if (!showing) panel.gameObject.SetActive(false);
    }
}