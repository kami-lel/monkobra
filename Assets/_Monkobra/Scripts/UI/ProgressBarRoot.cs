using UnityEngine;
using UnityEngine.UI;


[RequireComponent(typeof(Scrollbar))]
public class ProgressBarRoot : MonoBehaviour
{
    [Header("Cobra Marker")]
    [SerializeField] private Transform cobraHead;
    [SerializeField] private RectTransform cobraMarker;

    [Header("Window")]
    // world-y span shown by the full bar, player pinned at top
    [SerializeField] private float windowSizeY = 20f;

    private const string PLAYER_TAG = "Player";

    private Scrollbar progressBar;
    private Transform player;

    private bool hasWarnedNoPlayer;

    private void Awake()
    {
        progressBar = GetComponent<Scrollbar>();

        if (progressBar == null)
        {
            Debug.LogError(
                "ProgressBarRoot: failed to get Scrollbar.",
                this
            );

            enabled = false;
            return;
        }

        if (windowSizeY <= 0f)
        {
            Debug.LogWarning(
                "ProgressBarRoot: windowSizeY must be positive.",
                this
            );

            windowSizeY = 1f;
        }
    }

    private void Update()
    {
        if (!TryFindPlayer())
        {
            return;
        }

        // player stays at the far end of the bar
        progressBar.SetValueWithoutNotify(1f);

        UpdateCobraProgress();
    }

    private bool TryFindPlayer()
    {
        if (player != null)
        {
            return true;
        }

        GameObject playerObject =
            GameObject.FindGameObjectWithTag(PLAYER_TAG);

        if (playerObject == null)
        {
            if (!hasWarnedNoPlayer)
            {
                Debug.LogWarning(
                    "ProgressBarRoot: no Player found.",
                    this
                );

                hasWarnedNoPlayer = true;
            }

            return false;
        }

        player = playerObject.transform;
        return true;
    }

    private void UpdateCobraProgress()
    {
        if (cobraHead == null || cobraMarker == null)
        {
            return;
        }

        // gap below player, clamped so a far cobra sticks at bar bottom
        float gap = Mathf.Max(0f, player.position.y - cobraHead.position.y);
        float cobraProgress = 1f - Mathf.Clamp01(gap / windowSizeY);

        Vector2 anchor = new Vector2(0.5f, 0.5f);

        switch (progressBar.direction)
        {
            case Scrollbar.Direction.LeftToRight:
                anchor.x = cobraProgress;
                break;

            case Scrollbar.Direction.RightToLeft:
                anchor.x = 1f - cobraProgress;
                break;

            case Scrollbar.Direction.BottomToTop:
                anchor.y = cobraProgress;
                break;

            case Scrollbar.Direction.TopToBottom:
                anchor.y = 1f - cobraProgress;
                break;
        }

        cobraMarker.anchorMin = anchor;
        cobraMarker.anchorMax = anchor;
        cobraMarker.anchoredPosition = Vector2.zero;
    }
}
