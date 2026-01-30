using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class MainMenuHandler : MonoBehaviour
{
    [Header("Panel References")]
    [Tooltip("Assign the LevelSelect GameObject here (must have RectTransform)")]
    public RectTransform levelSelectPanel;

    [Tooltip("Assign the AboutTab GameObject here (must have RectTransform)")]
    public RectTransform aboutTabPanel;

    [Header("Animation Settings")]
    public float animationDuration = 0.5f;
    public float offScreenYOffset = -1000f;

    private RectTransform activePanel = null;
    private bool isAnimating = false;

    public void OnPlayClicked()
    {
        SceneManager.LoadScene("Level1");
    }

    public void OnSandboxClicked()
    {
        SceneManager.LoadScene("SandboxLevel");
    }

    public void OnQuitClicked()
    {
        Debug.Log("Quitting Game...");
        Application.Quit();
    }

    public void OnLevelsClicked()
    {
        HandlePanelToggle(levelSelectPanel);
    }

    public void OnAboutClicked()
    {
        HandlePanelToggle(aboutTabPanel);
    }

    public void OnLoadLevel(string sceneName)
    {
        SceneManager.LoadScene(sceneName);
    }

    private void HandlePanelToggle(RectTransform targetPanel)
    {
        if (isAnimating) return; // Prevent spam clicking breaking animations

        // The clicked panel is already open
        if (activePanel == targetPanel)
        {
            StartCoroutine(AnimatePanelOut(targetPanel));
            activePanel = null;
        }
        // Another panel is open -> close active, then open new
        else if (activePanel != null)
        {
            StartCoroutine(SwitchPanels(activePanel, targetPanel));
            activePanel = targetPanel;
        }
        // No panel is open -> open the new one
        else
        {
            StartCoroutine(AnimatePanelIn(targetPanel));
            activePanel = targetPanel;
        }
    }

    // Coroutine to swap panels (wait for one to leave before entering the next)
    private IEnumerator SwitchPanels(RectTransform outgoing, RectTransform incoming)
    {
        yield return StartCoroutine(AnimatePanelOut(outgoing));
        yield return StartCoroutine(AnimatePanelIn(incoming));
    }

    private IEnumerator AnimatePanelIn(RectTransform panel)
    {
        isAnimating = true;

        // Enable and Setup position
        panel.gameObject.SetActive(true);
        Vector2 startPos = new Vector2(0, offScreenYOffset);
        Vector2 endPos = Vector2.zero; // Center of screen

        panel.anchoredPosition = startPos;

        // Animate
        float elapsed = 0f;
        while (elapsed < animationDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / animationDuration;
            t = Mathf.SmoothStep(0f, 1f, t);

            panel.anchoredPosition = Vector2.Lerp(startPos, endPos, t);
            yield return null;
        }

        panel.anchoredPosition = endPos; // Ensure exact finish
        isAnimating = false;
    }

    private IEnumerator AnimatePanelOut(RectTransform panel)
    {
        isAnimating = true;

        Vector2 startPos = Vector2.zero;
        Vector2 endPos = new Vector2(0, offScreenYOffset);

        float elapsed = 0f;
        while (elapsed < animationDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / animationDuration;
            t = Mathf.SmoothStep(0f, 1f, t);

            panel.anchoredPosition = Vector2.Lerp(startPos, endPos, t);
            yield return null;
        }

        panel.anchoredPosition = endPos;
        panel.gameObject.SetActive(false); // Disable after animation finishes
        isAnimating = false;
    }
}