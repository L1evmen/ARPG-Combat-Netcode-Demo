using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 批量将 Built-in 材质升级为 URP 兼容材质。
/// 菜单：Tools → Upgrade Materials to URP
/// </summary>
public class MaterialUpgradeToURP : EditorWindow
{
    // Built-in Shader 名 → URP Shader 名的映射
    static readonly Dictionary<string, string> ShaderMap = new Dictionary<string, string>
    {
        { "Standard",            "Universal Render Pipeline/Lit" },
        { "Standard (Specular)", "Universal Render Pipeline/Lit" },
        { "Unlit/Color",         "Universal Render Pipeline/Unlit" },
        { "Unlit/Texture",       "Universal Render Pipeline/Unlit" },
        { "Unlit/Transparent",   "Universal Render Pipeline/Unlit" },
        { "Particles/Standard Surface", "Universal Render Pipeline/Particles/Simple Lit" },
        { "Mobile/Diffuse",      "Universal Render Pipeline/Simple Lit" },
        { "Mobile/Bumped Specular", "Universal Render Pipeline/Simple Lit" },
        { "Skybox/6 Sided",      "Skybox/6 Sided" },  // Skybox 通常兼容
        { "Skybox/Cubemap",      "Skybox/Cubemap" },
    };

    Vector2 _scrollPos;
    List<Material> _foundMaterials = new List<Material>();
    bool _scanned;

    [MenuItem("Tools/Upgrade Materials to URP")]
    static void ShowWindow()
    {
        GetWindow<MaterialUpgradeToURP>("材质升级 URP");
    }

    void OnGUI()
    {
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Built-in → URP 材质批量升级", EditorStyles.boldLabel);
        EditorGUILayout.Space(5);
        EditorGUILayout.HelpBox(
            "扫描项目中所有使用 Built-in Shader 的材质，并转换为 URP 兼容 Shader。\n" +
            "建议先备份项目！转换后可能需要微调参数。",
            MessageType.Info);
        EditorGUILayout.Space(10);

        if (GUILayout.Button("扫描项目材质", GUILayout.Height(30)))
            ScanMaterials();

        if (_scanned)
        {
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField($"找到 {_foundMaterials.Count} 个需要升级的材质", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);
            foreach (var mat in _foundMaterials)
            {
                if (mat == null) continue;
                string shaderName = mat.shader.name;
                string target = ShaderMap.ContainsKey(shaderName) ? ShaderMap[shaderName] : "未知";
                EditorGUILayout.LabelField($"  {mat.name}  |  {shaderName}  →  {target}");
            }
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(10);
            GUI.enabled = _foundMaterials.Count > 0;
            if (GUILayout.Button("升级全部材质", GUILayout.Height(35)))
                UpgradeMaterials();
            GUI.enabled = true;
        }
    }

    void ScanMaterials()
    {
        _foundMaterials.Clear();

        // 搜索项目中所有 .mat 文件
        string[] guids = AssetDatabase.FindAssets("t:Material");
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null || mat.shader == null) continue;

            string shaderName = mat.shader.name;

            // 跳过已经是 URP 的
            if (shaderName.StartsWith("Universal Render Pipeline") ||
                shaderName.StartsWith("URP") ||
                shaderName.StartsWith("Shader Graphs") ||
                shaderName.StartsWith("Hidden"))
                continue;

            // 跳过 Unity 内置资源
            if (path.StartsWith("Packages/") || path.StartsWith("Assets/Plugins/"))
                continue;

            // 只处理有映射关系的 Shader
            if (ShaderMap.ContainsKey(shaderName))
                _foundMaterials.Add(mat);
        }

        _scanned = true;
        Debug.Log($"[MaterialUpgrade] 扫描完成，找到 {_foundMaterials.Count} 个需要升级的材质");
    }

    void UpgradeMaterials()
    {
        int success = 0, fail = 0;

        foreach (var mat in _foundMaterials)
        {
            if (mat == null) continue;

            string oldShaderName = mat.shader.name;
            if (!ShaderMap.ContainsKey(oldShaderName)) { fail++; continue; }

            string targetShaderName = ShaderMap[oldShaderName];
            var newShader = Shader.Find(targetShaderName);
            if (newShader == null)
            {
                Debug.LogWarning($"[MaterialUpgrade] 找不到 Shader: {targetShaderName}，跳过 {mat.name}");
                fail++;
                continue;
            }

            // 保存原始属性
            Color baseColor = mat.HasProperty("_Color") ? mat.GetColor("_Color") : Color.white;
            Color baseColor2 = mat.HasProperty("_BaseColor") ? mat.GetColor("_BaseColor") : Color.white;
            Texture mainTex = mat.HasProperty("_MainTex") ? mat.GetTexture("_MainTex") : null;
            Texture baseMap = mat.HasProperty("_BaseMap") ? mat.GetTexture("_BaseMap") : null;
            float metallic = mat.HasProperty("_Metallic") ? mat.GetFloat("_Metallic") : 0f;
            float smoothness = mat.HasProperty("_Glossiness") ? mat.GetFloat("_Glossiness") : 0.5f;
            float bumpScale = mat.HasProperty("_BumpScale") ? mat.GetFloat("_BumpScale") : 1f;
            Texture bumpMap = mat.HasProperty("_BumpMap") ? mat.GetTexture("_BumpMap") : null;
            Texture emissionTex = mat.HasProperty("_EmissionMap") ? mat.GetTexture("_EmissionMap") : null;
            Color emissionColor = mat.HasProperty("_EmissionColor") ? mat.GetColor("_EmissionColor") : Color.black;
            float cutoff = mat.HasProperty("_Cutoff") ? mat.GetFloat("_Cutoff") : 0.5f;

            // 透明模式检测
            bool isTransparent = mat.HasProperty("_Mode") && mat.GetFloat("_Mode") > 0;
            bool isCutout = mat.HasProperty("_Mode") && mat.GetFloat("_Mode") == 1;

            // 切换 Shader
            mat.shader = newShader;

            // 迁移属性到 URP
            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", mainTex != null ? baseColor : baseColor2);

            if (mat.HasProperty("_BaseMap") && mainTex != null)
                mat.SetTexture("_BaseMap", mainTex);

            if (mat.HasProperty("_Metallic"))
                mat.SetFloat("_Metallic", metallic);

            if (mat.HasProperty("_Smoothness"))
                mat.SetFloat("_Smoothness", smoothness);

            if (mat.HasProperty("_BumpMap") && bumpMap != null)
            {
                mat.SetTexture("_BumpMap", bumpMap);
                mat.SetFloat("_BumpScale", bumpScale);
                mat.EnableKeyword("_NORMALMAP");
            }

            if (mat.HasProperty("_EmissionColor"))
            {
                mat.SetColor("_EmissionColor", emissionColor);
                if (emissionTex != null && mat.HasProperty("_EmissionMap"))
                    mat.SetTexture("_EmissionMap", emissionTex);
                if (emissionColor != Color.black)
                    mat.EnableKeyword("_EMISSION");
            }

            // 透明模式
            if (isCutout)
            {
                mat.SetFloat("_Surface", 0); // Opaque
                mat.SetFloat("_AlphaClip", 1);
                mat.SetFloat("_Cutoff", cutoff);
                mat.EnableKeyword("_ALPHATEST_ON");
                mat.renderQueue = (int)RenderQueue.AlphaTest;
            }
            else if (isTransparent)
            {
                mat.SetFloat("_Surface", 1); // Transparent
                mat.renderQueue = (int)RenderQueue.Transparent;
            }

            EditorUtility.SetDirty(mat);
            success++;
            Debug.Log($"[MaterialUpgrade] ✓ {mat.name}: {oldShaderName} → {targetShaderName}");
        }

        AssetDatabase.SaveAssets();
        EditorUtility.DisplayDialog("升级完成",
            $"成功: {success}\n失败: {fail}\n\n建议检查材质外观并微调参数。",
            "确定");

        _scanned = false;
        _foundMaterials.Clear();
    }
}
