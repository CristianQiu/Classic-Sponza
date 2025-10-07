//MJQStudioWorks

using UnityEditor;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor.PackageManager;


namespace RealToon.Editor.Welcome
{

    [InitializeOnLoad]
    public class RTWelcome : EditorWindow
    {

        #region Variables

        static float WinHig = 300;
        static EditorWindow EdiWin;
        static string CurrRP;
        static string CurrRPDisp;
        static string CurrRPVer;
        static string CurrRPFullVer;
        static string UniVer;
        static string UniVerFull = null;
        static string VRC_LV_Stat;
        static bool VRC_LV_CHK;
        static bool SRP_CHK_AVAI;
        static Object RT_Pack;
        static string RT_pac_name;
        static Object RP_ReMe;
        static Object RP_Qui_ReMe;
        static Vector2 scroll;
        static Vector2 scroll2;
        static Vector2 scroll3;
        static string rt_welcome_settings = "Assets/RealToon/Editor/RTW.sett";
        static string[] RP_Info = { "Built-In Render Pipeline (BiRP)",
                            "Universal Render Pipeline (URP)", 
                            "High Definition Render Pipeline (HDRP)" };

        #endregion

        #region RT_Welcome
            static RTWelcome()
            {
                if (File.Exists(rt_welcome_settings))
                {
                    if (File.ReadAllText(rt_welcome_settings) == "0")
                    {
                        if (File.Exists(rt_welcome_settings))
                        {
                            EditorApplication.delayCall += Ini;
                        }
                    }
                }
            }
        #endregion

        #region ini

        [MenuItem("Window/RealToon/Welcome Screen")]
        static void Ini()
        {
            EdiWin = GetWindow<RTWelcome>(true);
            EdiWin.titleContent = new GUIContent("Welcome RealToon Shader User");
            WinHig = 682;
            EdiWin.minSize = new Vector2(658, WinHig);
            EdiWin.maxSize = new Vector2(658, WinHig);
            
        }
        #endregion

        #region check_srp_ver
        static void check_srp_ver()
        {
            var request = Client.List();

            while (!request.IsCompleted) { }

            if (request.Status == StatusCode.Success)
            {
                foreach (var package in request.Result)
                {
                    if (package.name == "com.unity.render-pipelines.universal")
                    {
                        CurrRPVer = package.version.Substring(0, 2);
                        CurrRPFullVer = package.version;
                        CurrRP = package.displayName;
                        SRP_CHK_AVAI = true;
                        return;
                    }
                    else if (package.name == "com.unity.render-pipelines.high-definition")
                    {
                        CurrRPVer = package.version.Substring(0, 2);
                        CurrRPFullVer = package.version;
                        CurrRP = package.displayName;
                        SRP_CHK_AVAI = true;
                        return;
                    }
                }
            }
            else if (request.Status >= StatusCode.Failure)
            {
                UnityEngine.Debug.LogWarning("URP and HDRP Packages are not available or present in your project.");
                SRP_CHK_AVAI = false;
            }
        }
        #endregion

        #region check_vrc_lv
        static void check_vrc_lv()
        {
            if (GraphicsSettings.currentRenderPipeline == null)
            {
                if (Directory.Exists("Assets/VRCLightVolumes"))
                {
                    VRC_LV_CHK = true;
                    VRC_LV_Stat = "Is present";
                }
                else
                {
                    VRC_LV_CHK = false;
                    VRC_LV_Stat = "Not Present";
                }
            }
            else
            {
                VRC_LV_CHK = false;
                VRC_LV_Stat = "BiRP Only";
            }
        }
        #endregion

        #region rt_pac_set
        static void rt_pac_set()
        {
            if (GraphicsSettings.currentRenderPipeline != null)
            {
                if (CurrRP == "Universal Render Pipeline" || CurrRP == "Universal RP")
                {
                    if (int.Parse(CurrRPVer) == 12 || int.Parse(CurrRPVer) == 13)
                    {
                        RT_pac_name = "RealToon URP (URP 15 and 16)";
                    }
                    if (int.Parse(CurrRPVer) == 14)
                    {
                        RT_pac_name = "RealToon URP (URP 14)";
                    }
                    if (int.Parse(CurrRPVer) == 15 || int.Parse(CurrRPVer) == 16)
                    {
                        RT_pac_name = "RealToon URP (URP 15 and 16)";
                    }

                    if (UniVerFull.Substring(0, 6) == "6000.0")
                    {
                        RT_pac_name = "RealToon URP (Unity 6.0)";
                    }
                    else if (UniVerFull.Substring(0, 6) == "6000.1" || (int.Parse(UniVerFull.Substring(0, 1)) == 6 && int.Parse(UniVerFull.Substring(5, 1)) >= 1))
                    {
                        RT_pac_name = "RealToon URP (Unity 6.1 To Later)";
                    }

                    var ids_reme = AssetDatabase.FindAssets("Please read before you unpack or import", new[] { "Assets/RealToon/RealToon Shader Packages/SRP (LWRP - URP - HDRP)/URP" });
                    if (ids_reme.Length == 1)
                    {
                        RP_ReMe = AssetDatabase.LoadMainAssetAtPath(AssetDatabase.GUIDToAssetPath(ids_reme[0]));
                    }
                    else
                    {
                        Debug.Log("Couldn't find ReadMe's");
                    }

                }
                else if (CurrRP == "High Definition Render Pipeline" || CurrRP == "High Definition RP")
                {

                    if (int.Parse(CurrRPVer) == 12 || int.Parse(CurrRPVer) == 13)
                    {
                        RT_pac_name = "RealToon HDRP (HDRP 12 and 13)";
                    }
                    else if (int.Parse(CurrRPVer) == 16)
                    {
                        RT_pac_name = "RealToon HDRP (HDRP 16)";
                    }
                    else if (int.Parse(CurrRPVer) == 14 || int.Parse(CurrRPVer) == 15)
                    {
                        RT_pac_name = "RealToon HDRP (HDRP 14 and 15)";
                    }

                    if (UniVerFull.Substring(0, 6) == "6000.0")
                    {
                        RT_pac_name = "RealToon HDRP (Unity 6.0)";
                    }
                    else if (UniVerFull.Substring(0, 6) == "6000.1" || (int.Parse(UniVerFull.Substring(0, 1)) == 6 && int.Parse(UniVerFull.Substring(5, 1)) >= 1))
                    {
                        RT_pac_name = "RealToon HDRP (Unity 6.1 To Later)";
                    }

                    var ids_reme = AssetDatabase.FindAssets("Please read before you unpack or import", new[] { "Assets/RealToon/RealToon Shader Packages/SRP (LWRP - URP - HDRP)/HDRP" });
                    if (ids_reme.Length == 1)
                    {
                        RP_ReMe = AssetDatabase.LoadMainAssetAtPath(AssetDatabase.GUIDToAssetPath(ids_reme[0]));
                    }
                    else
                    {
                        Debug.Log("Couldn't find ReadMe's");
                    }

                    var ids_qui_reme = AssetDatabase.FindAssets("RealToon HDRP (Quick Guide)", new[] { "Assets/RealToon/RealToon Shader Packages/SRP (LWRP - URP - HDRP)/HDRP" });
                    if (ids_qui_reme.Length == 1)
                    {
                        RP_Qui_ReMe = AssetDatabase.LoadMainAssetAtPath(AssetDatabase.GUIDToAssetPath(ids_qui_reme[0]));
                    }
                    else
                    {
                        Debug.Log("Couldn't find Quick ReadMe's");
                    }
                }
            }
            else
            {
                if (int.Parse(Application.unityVersion.Substring(0, 4)) >= 2019)
                {
                    RT_pac_name = "RealToon Built-In RP [3D] (Unity 2019 and Later)";
                }
                else if (int.Parse(Application.unityVersion.Substring(0, 4)) < 2019)
                {
                    RT_pac_name = "RealToon Built-In RP [3D] (Unity 2018 and below) (FV)";
                }
            }

            var ids = AssetDatabase.FindAssets(RT_pac_name);

            if (ids.Length == 1)
            {
                RT_Pack = AssetDatabase.LoadMainAssetAtPath(AssetDatabase.GUIDToAssetPath(ids[0]));
            }
            else
            {
                RT_pac_name = "Package Not Found In Your Project";
                Debug.Log("Couldn't find packages");
            }
        }
        #endregion

        #region Process & UI
        void OnGUI()
        {
            Texture2D t = EditorGUIUtility.Load("Assets/RealToon/Editor/RT_GUI_Img.png") as Texture2D;
            Rect rect = new Rect(233, 0, t.width + 70, t.height + 70);
            GUI.DrawTexture(rect, t, ScaleMode.ScaleToFit);

            EditorGUILayout.Space(76);

            EditorGUILayout.BeginVertical();

                EditorGUILayout.BeginVertical();

                if (GraphicsSettings.currentRenderPipeline != null)
                {
                    if (CurrRP == "Universal Render Pipeline" || CurrRP == "Universal RP")
                    {
                        CurrRPDisp = RP_Info[1] + " v" + CurrRPFullVer;
                    }
                    else if (CurrRP == "High Definition Render Pipeline" || CurrRP == "High Definition RP")
                    {
                        CurrRPDisp = RP_Info[2] + " v" + CurrRPFullVer;
                    }
                }
                else
                {
                    CurrRPDisp = RP_Info[0] + CurrRPFullVer;
                }


                GUIStyle centeredtext = GUI.skin.GetStyle("Label");
                centeredtext.alignment = TextAnchor.MiddleCenter;
                centeredtext.fontSize = 18;
                centeredtext.fontStyle = FontStyle.Bold;
                EditorGUILayout.LabelField("A PRO Anime/Toon Shader", centeredtext);

                EditorGUILayout.EndVertical();

                EditorGUILayout.Space(19);

                EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

                EditorGUILayout.BeginHorizontal();

                EditorGUILayout.Space(22);

                    EditorGUILayout.BeginVertical("TextArea", GUILayout.Height(90));

                    EditorGUILayout.LabelField("Introduction:");

                    scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.Height(280));

                    EditorGUILayout.Space(4);

                    GUILayout.TextArea(
                            "*Thank you for purchasing RealToon Shader, before you start using RealToon, please read first the 'ReadMe - Important - Guide.txt' text file for setups and infos.\n\n" +

                           "*All shaders packages are in the folder 'RealToon Shader Packages', just unpack the 'RealToon Shader' that correspond to your projects render pipeline.\n" +
                           "You can also just click the package on the Welcome Screen, To access the Welcome Screen just go to 'Window > RealToon > Welcome Screen'.\n\n" +

                           "*If you are a VRoid user, read the 'For VRoid-VRM users.txt' text file.\n\n" +

                           "*For video tutorials and user guide, see the bottom part of RealToon Inspector panel.\n\n" +

                           "*If you need some help/support, just send an email including the invoice number.\n" +
                           "See the 'User Guide.pdf' file for the links and email support.\n\n" +

                           "*PlayStation support is currently for URP and HDRP only.\n\n" +

                           "Note:\nDon't move the 'RealToon' folder to other folder, it should stay in the root folder 'Asset'.");

                        EditorGUILayout.EndScrollView();

                    EditorGUILayout.EndVertical();

                EditorGUILayout.Space(22);

                EditorGUILayout.EndHorizontal();

                EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

                EditorGUILayout.BeginHorizontal();

                EditorGUILayout.Space(20);

                    EditorGUILayout.BeginVertical("TextArea", GUILayout.Height(90));

                        EditorGUILayout.LabelField("Project Info:");

                        scroll2 = EditorGUILayout.BeginScrollView(scroll2, GUILayout.Height(140));

                            EditorGUILayout.Space(4);

                            GUIStyle Pro_info = GUI.skin.GetStyle("Label");
                            Pro_info.alignment = TextAnchor.MiddleLeft;
                            Pro_info.fontSize = 12;

                            GUILayout.Label("Rendering Pipeline: \n" + CurrRPDisp, Pro_info);

                            EditorGUILayout.Space(10);

                            GUILayout.Label("Unity Version: \n" + UniVerFull, Pro_info);
                            
                            EditorGUILayout.Space(10);

                            if (GraphicsSettings.currentRenderPipeline == null)
                            {
                                GUILayout.Label("VRC Light Volumes (VRC Users Only): \n" + VRC_LV_Stat, Pro_info);
                            }

                        EditorGUILayout.EndScrollView();

                    EditorGUILayout.EndVertical();

                EditorGUILayout.Space(30);

                EditorGUILayout.BeginVertical("TextArea", GUILayout.Height(90));

                        EditorGUILayout.LabelField("Suitable Packages & ReadMe:");

                        scroll3 = EditorGUILayout.BeginScrollView(scroll3, GUILayout.Height(140));
                            
                            GUIStyle Pro_info2 = GUI.skin.GetStyle("Label");
                            Pro_info2.fontSize = 12;
                            Pro_info2.fontStyle = FontStyle.Bold;
            
                            EditorGUILayout.LabelField("Packages: ", Pro_info2);

                            if(GUILayout.Button(RT_pac_name))
                            {
                                if(RT_pac_name != "Package Not Found In Your Project")
                                {
                                    Debug.LogWarning("Now opening RealToon Shader package: " + RT_Pack.name);
                                    AssetDatabase.OpenAsset(RT_Pack);
                                }
                            }

                            EditorGUILayout.Space(10);

                            EditorGUILayout.LabelField("ReadMe: ", Pro_info2);

                            if (GraphicsSettings.currentRenderPipeline != null)
                            {
                                if (CurrRP == "Universal Render Pipeline" || CurrRP == "Universal RP")
                                {
                                    if(GUILayout.Button("Read Me First (URP)"))
                                    {
                                        AssetDatabase.OpenAsset(RP_ReMe);
                                    }
                                }
                                else if (CurrRP == "High Definition Render Pipeline" || CurrRP == "High Definition RP")
                                {
                                    if(GUILayout.Button("Read Me First (HDRP)"))
                                    {
                                        AssetDatabase.OpenAsset(RP_ReMe);
                                    }

                                    if(GUILayout.Button("Quick Guide (HDRP)"))
                                    {
                                        AssetDatabase.OpenAsset(RP_Qui_ReMe);
                                    }
                                }
                            }

                            if(GUILayout.Button("ReadMe for Manual Unpacking & Info"))
                            {
                                Application.OpenURL(Application.dataPath + "/RealToon/ReadMe - Important - Guide.txt");
                            }

                            if(GUILayout.Button("VRoid-VRM users Read Me"))
                            {
                                Application.OpenURL(Application.dataPath + "/RealToon/For VRoid-VRM users.txt");
                            }

                            if(GUILayout.Button("What's New"))
                            {
                                Application.OpenURL(Application.dataPath + "/RealToon/What's New.txt");
                            }

                        EditorGUILayout.EndScrollView();

                    EditorGUILayout.EndVertical();

                EditorGUILayout.Space(20);

                EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("User Guide"))
            {
                Application.OpenURL(Application.dataPath + "/RealToon/RealToon (User Guide).pdf");
            }
            if (GUILayout.Button("Video Tutorials"))
            {
                Application.OpenURL("www.youtube.com/playlist?list=PL0M1m9smMVPJ4qEkJnZObqJE5mU9uz6SY");
            }
            if (GUILayout.Button("Contact/Support"))
            {
                Application.OpenURL("www.mjqstudioworks.weebly.com/contact.html");
            }
            if (GUILayout.Button("Website"))
            {
                Application.OpenURL("www.mjqstudioworks.weebly.com");
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();

        }
        private void OnEnable()
        {
            check_srp_ver();
            check_vrc_lv();
            UniVer = Application.unityVersion.Substring(0, 4);
            UniVerFull = Application.unityVersion;
            rt_pac_set();
        }

        private void OnDestroy()
        {
            if (File.Exists(rt_welcome_settings) && File.ReadAllText(rt_welcome_settings) == "0")
            {
                File.WriteAllText(rt_welcome_settings, "1");
                AssetDatabase.Refresh();
            }
        }
        #endregion
    }

}