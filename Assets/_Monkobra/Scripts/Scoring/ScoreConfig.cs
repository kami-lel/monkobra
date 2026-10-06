using UnityEngine;

//全局可看的高度和分数规则
[CreateAssetMenu(fileName = "ScoreConfig", menuName = "Monkobra/Scoring/Score Config")]
//[SerializeField]：让 Unity 保存这个字段，并在默认 Inspector 中显示
public class ScoreConfig: ScriptableObject {
    public int PointsPerMeter => Mathf.Max(0, pointsPerMeter);
    public float UnitsPerMeter => Mathf.Max(0.001f, unitsPerMeter);

    [SerializeField, Min(0)]
    private int pointsPerMeter = 1;
    // 其他脚本不能直接读取或修改 pointsPerMeter
    [SerializeField, Min(0.001f)]
    private float unitsPerMeter = 1f;
    //定义规则，unityy轴一米等于多少米
    private void OnValidate() {
        pointsPerMeter = Mathf.Max(0, pointsPerMeter);
        unitsPerMeter = Mathf.Max(0.001f, unitsPerMeter);
    }
}
