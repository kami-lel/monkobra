using System.Text;
using TMPro;
using UnityEngine;

/// <summary>
/// Fills the TMP text it sits on with one line per hack key, ie
/// <c>F8: download the analytics session report</c>, so a tester can see what
/// each dev key does. Outside a development build the keys are off, so it
/// switches its own object off instead.
/// <para>
/// Put it on the TMP text object itself. The text is written once, in
/// <c>Awake</c>.
/// </para>
/// </summary>
[RequireComponent(typeof(TMP_Text))]
public class HackShortcutPanel: MonoBehaviour {
    // Inspector Fields  #######################################################
    [SerializeField]
    [Tooltip("dev/hack keys listed, in the order of its entries")]
    private HackShortcutConfig hackShortcuts;

    // MonoBehaviour Lifecycle  ################################################
    private void Awake() {
        if (!HackShortcutConfig.IsActive) {
            gameObject.SetActive(false);
            return;
        }

        // Inspector Assignment Guard  -----------------------------------------
        if (hackShortcuts == null) {
            Debug.LogWarning(
                "HackShortcutPanel:\tmust assign Inspector Field: Hack "
                    + "Shortcuts",
                this
            );
            enabled = false;
            return;
        }

        TMP_Text label = GetComponent<TMP_Text>();
        if (label == null) {
            Debug.LogError(
                "HackShortcutPanel:\tfail to get Component: TMP_Text",
                this
            );
            enabled = false;
            return;
        }

        label.text = BuildHelpText();
        if (Debug.isDebugBuild) {
            Debug.Log("HackShortcutPanel:\thelp text ready");
        }
    }

    // Private Helpers  ########################################################
    // one "key: description" line per entry
    private string BuildHelpText() {
        StringBuilder sb = new StringBuilder();
        foreach (HackShortcutConfig.Entry entry in hackShortcuts.Entries) {
            if (sb.Length > 0) {
                sb.Append('\n');
            }
            sb.Append(entry.Key).Append(": ").Append(entry.Description);
        }
        return sb.ToString();
    }
}
