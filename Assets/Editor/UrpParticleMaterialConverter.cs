using UnityEditor;
using UnityEngine;

// 메뉴 [Tools > TankAttack > Convert Explosion Materials to Built-in]
// URP 전용으로 만들어진 폭발 에셋(Cinematic Explosions FREE)의 머티리얼을
// 이 프로젝트의 Built-in 렌더러용 파티클 셰이더로 일괄 변환한다.
// - 원래 텍스처(_BaseMap 또는 _MainTex)를 그대로 옮김
// - 불꽃/스파크: Legacy Particles/Additive (밝게 빛나는 합성)
// - 연기: Legacy Particles/Alpha Blended (반투명)
// - 데모용 불투명 머티리얼: Standard
public static class UrpParticleMaterialConverter
{
    const string TargetFolder = "Assets/Mirza Beig";

    [MenuItem("Tools/TankAttack/Convert Explosion Materials to Built-in")]
    public static void Convert()
    {
        Shader additive = Shader.Find("Legacy Shaders/Particles/Additive");
        Shader alphaBlended = Shader.Find("Legacy Shaders/Particles/Alpha Blended");
        Shader standard = Shader.Find("Standard");
        if (additive == null || alphaBlended == null || standard == null)
        {
            Debug.LogError("Built-in 파티클 셰이더를 찾지 못했습니다.");
            return;
        }

        string[] guids = AssetDatabase.FindAssets("t:Material", new[] { TargetFolder });
        int count = 0;
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) continue;

            // 셰이더가 없어도 머티리얼 파일에 저장된 원래 값은 남아 있으므로 거기서 읽음
            Texture tex = ReadSavedTexture(mat, "_BaseMap") ?? ReadSavedTexture(mat, "_MainTex");
            Color baseColor = ReadSavedColor(mat, "_BaseColor", Color.white);
            bool transparent = ReadSavedFloat(mat, "_Surface", 0.0f) > 0.5f;
            string name = mat.name.ToLower();

            if (!transparent)
            {
                mat.shader = standard;
                mat.SetColor("_Color", baseColor);
                if (tex != null) mat.SetTexture("_MainTex", tex);
            }
            else
            {
                mat.shader = name.Contains("smoke") ? alphaBlended : additive;
                if (tex != null) mat.SetTexture("_MainTex", tex);
                // Legacy 파티클 셰이더는 _TintColor 0.5가 원래 밝기(셰이더 안에서 2배)
                mat.SetColor("_TintColor", new Color(0.5f, 0.5f, 0.5f, 0.5f));
            }

            EditorUtility.SetDirty(mat);
            count++;
            Debug.Log($"변환: {mat.name} → {mat.shader.name}");
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"폭발 머티리얼 {count}개를 Built-in 셰이더로 변환했습니다.");
    }

    static Texture ReadSavedTexture(Material mat, string prop)
    {
        SerializedProperty envs = new SerializedObject(mat).FindProperty("m_SavedProperties.m_TexEnvs");
        for (int i = 0; i < envs.arraySize; i++)
        {
            SerializedProperty e = envs.GetArrayElementAtIndex(i);
            if (e.FindPropertyRelative("first").stringValue == prop)
            {
                return e.FindPropertyRelative("second.m_Texture").objectReferenceValue as Texture;
            }
        }
        return null;
    }

    static Color ReadSavedColor(Material mat, string prop, Color fallback)
    {
        SerializedProperty colors = new SerializedObject(mat).FindProperty("m_SavedProperties.m_Colors");
        for (int i = 0; i < colors.arraySize; i++)
        {
            SerializedProperty e = colors.GetArrayElementAtIndex(i);
            if (e.FindPropertyRelative("first").stringValue == prop)
            {
                return e.FindPropertyRelative("second").colorValue;
            }
        }
        return fallback;
    }

    static float ReadSavedFloat(Material mat, string prop, float fallback)
    {
        SerializedProperty floats = new SerializedObject(mat).FindProperty("m_SavedProperties.m_Floats");
        for (int i = 0; i < floats.arraySize; i++)
        {
            SerializedProperty e = floats.GetArrayElementAtIndex(i);
            if (e.FindPropertyRelative("first").stringValue == prop)
            {
                return e.FindPropertyRelative("second").floatValue;
            }
        }
        return fallback;
    }
}
