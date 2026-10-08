using UnityEngine;

[DisallowMultipleComponent]
//同一个对象不能重复挂两个 ScorePickup，避免配置重复
public class ScorePickup: MonoBehaviour {
    public bool TryCollect() {
        if (!isActiveAndEnabled || isCollected || Time.timeScale <= 0f
            || (GameController.I != null
                && GameController.I.State != GameState.Playing)) {
            return false;
        }//组件没启用、已经领过、游戏暂停或结束，都会拒绝领取
        ScoreManager manager = ScoreManager.I;
        if (manager != null && !manager.CanScore) {
            return false;
        }

        isCollected = true;
        if (manager != null) {
            manager.TryAddReward();
        } else {
        
            Debug.LogWarning("ScorePickup: no ScoreManager; collected without points.", this);
        }
        gameObject.SetActive(false);
        return true;
    }

    private bool isCollected;

    private void OnEnable() {
        isCollected = false;
    }
}
