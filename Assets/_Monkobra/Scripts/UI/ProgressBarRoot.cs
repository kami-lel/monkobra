using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Scrollbar))]
public class ProgressBarRoot : MonoBehaviour
{
    [Header("Cobra Marker")]
    [SerializeField] private Transform cobraHead;
    [SerializeField] private RectTransform cobraMarker;

    private const string PLAYER_TAG = "Player";

    private Scrollbar progressBar;
    private Transform player;

    private bool hasWarnedNoTreeManager;
    private bool hasWarnedNoPlayer;
    private int lastLoggedPercent = -1;

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
        }
    }

    private void Update()
    {
        if (TreeManager.I == null)
        {
            if (!hasWarnedNoTreeManager)
            {
                Debug.LogWarning(
                    "ProgressBarRoot: no TreeManager in scene.",
                    this
                );

                hasWarnedNoTreeManager = true;
            }

            return;
        }

        UpdatePlayerProgress();
        UpdateCobraProgress();
    }

    private void UpdatePlayerProgress()
    {
        if (player == null)
        {
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

                return;
            }

            player = playerObject.transform;
        }

        float playerProgress = Mathf.InverseLerp(
            TreeManager.I.MinY,
            TreeManager.I.MaxY,
            player.position.y
        );

        progressBar.SetValueWithoutNotify(playerProgress);

        int percent = Mathf.RoundToInt(playerProgress * 100f);

        if (percent != lastLoggedPercent)
        {
            lastLoggedPercent = percent;
        }
    }

    private void UpdateCobraProgress()
{
    if (cobraHead == null || cobraMarker == null)
    {
        return;
    }

    float cobraProgress = Mathf.InverseLerp(
        TreeManager.I.MinY,
        TreeManager.I.MaxY,
        cobraHead.position.y
    );

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