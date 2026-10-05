using UnityEngine;

public class SpiderWebBuilder : MonoBehaviour
{
    public int spokeCount = 12;        
    public int ringCount = 6;          
    public float radius = 1f;          
    public float thickness = 0.012f;   
    [Range(0f, 0.3f)] public float jitter = 0.08f; 
    [Range(0f, 0.2f)] public float sag = 0.06f;    
    public Material webMaterial;
    public int seed = 1;

    [ContextMenu("Build Web")]
    public void Build()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
            DestroyImmediate(transform.GetChild(i).gameObject);

        Random.InitState(seed);

        
        float[] ang = new float[spokeCount];
        float[] len = new float[spokeCount];
        for (int i = 0; i < spokeCount; i++)
        {
            ang[i] = 360f / spokeCount * (i + Random.Range(-jitter, jitter));
            len[i] = radius * Random.Range(1f - jitter, 1f + jitter * 0.5f);
            MakeStrand(Vector3.zero, Dir(ang[i]) * len[i], "Spoke_" + i);
        }

        
        for (int r = 0; r < ringCount; r++)
        {
            float t = Mathf.Lerp(0.2f, 0.95f, (float)r / (ringCount - 1));
            Vector3[] p = new Vector3[spokeCount];
            for (int i = 0; i < spokeCount; i++)
                p[i] = Dir(ang[i]) * len[i] * t * Random.Range(1f - jitter * 0.3f, 1f + jitter * 0.3f);

            for (int i = 0; i < spokeCount; i++)
            {
                Vector3 a = p[i], b = p[(i + 1) % spokeCount];
                Vector3 mid = (a + b) * 0.5f * (1f - sag); // 中点向圆心收，形成下垂弧度
                MakeStrand(a, mid, $"Ring{r}_{i}a");
                MakeStrand(mid, b, $"Ring{r}_{i}b");
            }
        }

        
        var hub = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        DestroyImmediate(hub.GetComponent<Collider>());
        hub.name = "Hub";
        hub.transform.SetParent(transform, false);
        hub.transform.localScale = Vector3.one * thickness * 4f;
        if (webMaterial) hub.GetComponent<Renderer>().sharedMaterial = webMaterial;

#if UNITY_EDITOR
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
#endif
    }


    Vector3 Dir(float deg)
    {
        float rad = deg * Mathf.Deg2Rad;
        return new Vector3(0f, Mathf.Sin(rad), Mathf.Cos(rad));
    }

    void MakeStrand(Vector3 a, Vector3 b, string name)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        DestroyImmediate(go.GetComponent<Collider>()); 
        go.name = name;
        go.transform.SetParent(transform, false);
        Vector3 d = b - a;
        go.transform.localPosition = (a + b) * 0.5f;
        go.transform.localRotation = Quaternion.FromToRotation(Vector3.up, d);
        go.transform.localScale = new Vector3(thickness, d.magnitude, thickness);
        var rend = go.GetComponent<Renderer>();
        rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        if (webMaterial) rend.sharedMaterial = webMaterial;
    }
}