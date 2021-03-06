﻿using System.IO;
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

// ------------------------------------------------------------------------------------------------
// 現状と仕様
// ------------------------------------------------------------------------------------------------
// 
// 現在のところ無効なディレクトリまたはファイル名であることを確認しません。
// 任意のディレクトリ、ファイル名を指定するときは、
// 実行者自身が安全な名前であることを保証する必要があります。

#if UNITY_EDITOR

/// <summary>
/// Unity の画面から画像をキャプチャするための機能を提供します。
/// </summary>
public class ImageCaptureTool : EditorWindow
{
    #region Define
    /// <summary>
    /// カメラデータクラス
    /// </summary>
    [System.Serializable]
    public class CameraData
    {
        public bool enable = true;      // キャプチャするか否かのフラグ
        public Camera camera = null;    // キャプチャするカメラオブジェクト
        public string suffix = "";      // ファイル名の末尾につける文字列
        public int width = 0;           // キャプチャする解像度の幅
        public int height = 0;          // キャプチャする解像度の高さ

        public CameraData(string suf)
        {
            suffix = suf;
        }
    }
    #endregion

    #region Filed

    /// <summary>
    /// キャプチャ画像を保存するためのキー。
    /// </summary>
    public KeyCode imageCaptureKey = KeyCode.Return;

    /// <summary>
    /// キー入力によるキャプチャ画像の保存の可否。
    /// true のとき、キー入力によるキャプチャ画像の保存を有効にします。
    /// </summary>
    public bool enableImageCaptureKey = false;

    /// <summary>
    /// キャプチャ画像を出力するディレクトリのパス。
    /// 空白文字列などの無効な文字列のとき、実行ファイルと同じディレクトリに出力します。
    /// </summary>
    public string outputDirectory = null;

    /// <summary>
    /// キャプチャ画像の既定のファイル名。
    /// </summary>
    public static string BaseOutputFileName = "image";

    /// <summary>
    /// キャプチャ画像の基底のファイル名。
    /// 空白文字列などの無効な文字列のとき、基底のファイル名は image となります。
    /// </summary>
    public string outputFileName = ImageCaptureTool.BaseOutputFileName;

    /// <summary>
    /// キャプチャ画像のインデックス番号。
    /// </summary>
    public int currentFileNameIndex = 0;

    /// <summary>
    /// カメラデータリスト。
    /// </summary>
    public List<CameraData> cameraList = new List<CameraData>();

    /// <summary>
    /// キャプチャ画像の解像度の倍率。
    /// 2 を設定するとき指定された解像度の 2 倍の解像度でキャプチャ画像を生成します。
    /// </summary>
    public int imageScale = 1;

    /// <summary>
    /// 背景の透過を有効にする判定。初期値は無効です。
    /// </summary>
    public bool enableBackgroundAlpha = false;

    /// <summary>
    /// ScrollView の現在の位置。
    /// </summary>
    private Vector2 scrollPosition = Vector2.zero;

    #endregion Field

    #region Method

    /// <summary>
    /// Window を初期化(表示)するときに実行されます。
    /// </summary>
    [MenuItem("Custom/ImageCaptureTool")]
    static void Init()
    {
        EditorWindow.GetWindow<ImageCaptureTool>("ImageCapture");
    }

    /// <summary>
    /// Window が有効になったときに実行されます。
    /// </summary>
    void OnEnable()
    {
        EditorApplication.update += ForceOnGUI;
    }

    /// <summary>
    /// Window が無効になったときに実行されます。
    /// </summary>
    void OnDisable()
    {
        EditorApplication.update -= ForceOnGUI;
    }

    /// <summary>
    /// GUI の出力時に実行されます。
    /// </summary>
    void OnGUI()
    {
        #region Style

        GUIStyle marginStyle = GUI.skin.label;
        marginStyle.wordWrap = true;
        marginStyle.margin = new RectOffset(5, 5, 5, 5);

        #endregion Style

        this.scrollPosition = EditorGUILayout.BeginScrollView(this.scrollPosition, GUI.skin.box);

        EditorGUILayout.LabelField("現在の設定で画像をキャプチャします。",
                                    marginStyle);

        if (GUILayout.Button("Click to Save"))
        {
            CaptureImage();
        }

        // 指定したキー入力が実行されたとき、画像を保存します。

        if (Event.current != null
            && Event.current.type == EventType.keyDown
            && Event.current.keyCode == this.imageCaptureKey
            && this.enableImageCaptureKey)
        {
            CaptureImage();
        }

        EditorGUILayout.LabelField("有効なとき、指定したキー入力によって画像をキャプチャします。",
                                    marginStyle);

        EditorGUILayout.BeginHorizontal(GUI.skin.label);
        {
            this.enableImageCaptureKey = EditorGUILayout.Toggle(this.enableImageCaptureKey);
            this.imageCaptureKey = (KeyCode)EditorGUILayout.EnumPopup(this.imageCaptureKey);
        }
        EditorGUILayout.EndHorizontal();

        #region OutputDirectory

        EditorGUILayout.LabelField("キャプチャ画像の出力ディレクトリを設定します。",
                                    marginStyle);

        EditorGUILayout.BeginHorizontal(GUI.skin.label);
        {
            if (GUILayout.Button("Open"))
            {
                string tempPath = EditorUtility.SaveFolderPanel("Open", this.outputDirectory, "");

                // Cancel された場合を考慮します。

                if (!tempPath.Equals(""))
                {
                    this.outputDirectory = EditorGUILayout.TextField(tempPath);
                    this.Repaint();
                }
            }
            else
            {
                this.outputDirectory = EditorGUILayout.TextField(this.outputDirectory);
            }
        }
        EditorGUILayout.EndHorizontal();

        if (this.outputDirectory == null || this.outputDirectory.Equals(""))
        {
            this.outputDirectory = Application.dataPath + "/";
        }

        #endregion Output Directory

        EditorGUILayout.LabelField
            ("キャプチャ画像の基底のファイル名を設定します。",
             marginStyle);

        this.outputFileName = EditorGUILayout.TextField(this.outputFileName);

        EditorGUILayout.LabelField
            ("キャプチャ画像のファイル名に与えられるインデックスです。",
             marginStyle);

        this.currentFileNameIndex = EditorGUILayout.IntField(this.currentFileNameIndex);


        int[] gameViewResolution = GetGameViewResolution();

        EditorGUILayout.Separator();
        for (int i = 0; i < cameraList.Count; i++)
        {
            CameraData data = cameraList[i];
            GUILayout.Box("", GUILayout.Width(this.position.width), GUILayout.Height(1));
            data.enable = EditorGUILayout.Toggle("Camera[" + i + "]キャプチャ", data.enable);

            EditorGUILayout.LabelField
                ("キャプチャ画像のファイル名の末尾につける文字列を指定します。",
                 marginStyle);
            data.suffix = EditorGUILayout.TextField("Suffix", data.suffix);

            EditorGUILayout.LabelField
                ("画像をキャプチャするカメラを指定します。指定しないとき MainCamera の画像をキャプチャします。",
                 marginStyle);

            data.camera = EditorGUILayout.ObjectField("Camera", data.camera, typeof(Camera), true) as Camera;

            EditorGUILayout.LabelField
            ("キャプチャ画像の水平方向の解像度。0 のとき、GameView の解像度(" + gameViewResolution[0] + ")になります。",
             marginStyle);

            data.width = EditorGUILayout.IntSlider(data.width, 0, 9999);

            EditorGUILayout.LabelField
                ("出力する画像の垂直方向の解像度。0 のとき、GameView の解像度(" + gameViewResolution[1] + ")になります。",
                 marginStyle);

            data.height = EditorGUILayout.IntSlider(data.height, 0, 9999);
        }
        
        EditorGUILayout.BeginHorizontal(GUI.skin.label);
        {
            if (GUILayout.Button("+"))
            {
                cameraList.Add(new CameraData("Camera_" + cameraList.Count.ToString()));
            }
            if ((GUILayout.Button("-"))&&(cameraList.Count>0))
            {
                cameraList.RemoveAt(cameraList.Count - 1);
            }
        }
        EditorGUILayout.EndHorizontal();
        GUILayout.Box("", GUILayout.Width(this.position.width), GUILayout.Height(1));
        EditorGUILayout.Separator();

        EditorGUILayout.LabelField
            ("キャプチャ画像の解像度の倍率。2 を設定するとき、指定した解像度の 2 倍の解像度になります。",
             marginStyle);

        this.imageScale = EditorGUILayout.IntSlider(this.imageScale, 1, 10);

        EditorGUILayout.LabelField
            ("カメラの設定にかかわらず背景を透過するとき有効にします。",
             marginStyle);

        this.enableBackgroundAlpha = EditorGUILayout.Toggle(this.enableBackgroundAlpha);

        EditorGUILayout.EndScrollView();
    }

    /// <summary>
    /// GameView の解像度情報を更新するために再描画します。
    /// </summary>
    private void ForceOnGUI()
    {
        // 毎フレーム更新・再描画すると処理不可が大きいので、更新レートを落とします。

        if (System.DateTime.Now.Millisecond % 5 == 0)
        {
            Repaint();
        }
    }

    /// <summary>
    /// GameView の大きさを取得します。
    /// </summary>
    /// </summary>
    /// <returns>
    /// GameView の横幅と高さの配列。
    /// </returns>
    private int[] GetGameViewResolution()
    {
        string[] gameViewResolution = UnityStats.screenRes.Split('x');
        return new int[]
        {
            int.Parse(gameViewResolution[0]),
            int.Parse(gameViewResolution[1])
        };
    }

    /// <summary>
    /// 画像の保存を実行します。
    /// </summary>
    private void CaptureImage()
    {
        // エディタ以外のときは実行しません。

        if (Application.platform != RuntimePlatform.OSXEditor
            && Application.platform != RuntimePlatform.WindowsEditor)
        {
            return;
        }

        for (int i = 0; i < cameraList.Count; i++)
        {
            if (!cameraList[i].enable) continue;

            try
            {
                Texture2D texture = GenerateCaptureImage(cameraList[i]);
                byte[] bytes = texture.EncodeToPNG();

                string outputFileName = GetOutputPath() + GetOutputFileName() + cameraList[i].suffix + ".png";

                File.WriteAllBytes(outputFileName, bytes);

                this.currentFileNameIndex += 1;

                DestroyImmediate(texture);

                ShowCaptureResult(true, "Success : " + outputFileName);
            }
            catch
            {
                ShowCaptureResult(false, "Error : " + outputFileName);
            }
        }
    }

    /// <summary>
    /// キャプチャしたイメージを Texture2D として取得します。
    /// </summary>
    /// <returns>
    /// キャプチャされたイメージが与えられた Texture2D 。
    /// </returns>
    private Texture2D GenerateCaptureImage(CameraData data)
    {
        Camera fixedCamera;
        
        if (data.camera == null)
        {
            fixedCamera = GameObject.FindGameObjectWithTag("MainCamera").GetComponent<Camera>();
        }
        else
        {
            fixedCamera = data.camera;
        }

        int fixedWidth = data.width;
        int fixedHiehgt = data.height;
        int bit = 32;

        int[] gameViewResolution = GetGameViewResolution();

        if (fixedWidth == 0)
        {
            fixedWidth = gameViewResolution[0];
        }

        if (fixedHiehgt == 0)
        {
            fixedHiehgt = gameViewResolution[1];
        }

        fixedWidth *= this.imageScale;
        fixedHiehgt *= this.imageScale;

        Color presetBackgroundColor = fixedCamera.backgroundColor;
        CameraClearFlags presetClearFlags = fixedCamera.clearFlags;

        if (this.enableBackgroundAlpha)
        {
            fixedCamera.backgroundColor = Color.clear;
            fixedCamera.clearFlags = CameraClearFlags.SolidColor;
        }

        RenderTexture presetRenderTexture = fixedCamera.targetTexture;

        // カメラに出力用の RenderTexture を設定してレンダリングを実行し、
        // その情報を Texture2D に保存して返す。

        RenderTexture outputRenderTexture = new RenderTexture(fixedWidth,
                                                              fixedHiehgt,
                                                              bit);
        fixedCamera.targetTexture = outputRenderTexture;

        Texture2D captureImage = new Texture2D(fixedWidth,
                                        fixedHiehgt,
                                        TextureFormat.ARGB32,
                                        false);

        fixedCamera.Render();

        RenderTexture.active = outputRenderTexture;

        captureImage.ReadPixels
            (new Rect(0, 0, fixedWidth, fixedHiehgt), 0, 0);

        // 設定を元に戻します。

        fixedCamera.backgroundColor = presetBackgroundColor;
        fixedCamera.clearFlags = presetClearFlags;
        fixedCamera.targetTexture = presetRenderTexture;

        // 解放してから終了します。

        RenderTexture.active = null;

        outputRenderTexture.Release();

        DestroyImmediate(outputRenderTexture);

        return captureImage;
    }

    /// <summary>
    /// 最終的に出力するパスを取得します。
    /// </summary>
    /// <returns>
    /// 出力するパス
    /// </returns>
    private string GetOutputPath()
    {
        string fixedDirectory = this.outputDirectory;
        
        if (fixedDirectory == null || fixedDirectory.Equals(""))
        {
            fixedDirectory = Application.dataPath + "/";
        }
        else
        {
            fixedDirectory = fixedDirectory + "/";
        }
        
        return fixedDirectory;
    }

    /// <summary>
    /// 最終的に出力するファイル名を取得します。
    /// </summary>
    /// <returns>
    /// 出力するファイル名。
    /// </returns>
    private string GetOutputFileName()
    {
        string fixedFileName = this.outputFileName;
        
        if (fixedFileName.Equals(""))
        {
            fixedFileName = ImageCaptureTool.BaseOutputFileName;
        }

        return fixedFileName + this.currentFileNameIndex;
    }

    /// <summary>
    /// キャプチャーの成否を出力します。
    /// </summary>
    /// <param name="success">
    /// キャプチャーの成否。
    /// </param>
    /// <param name="message">
    /// 通知するメッセージ。
    /// </param>
    private void ShowCaptureResult(bool success, string message)
    {
        GUIContent notification = new GUIContent(message);
        this.ShowNotification(notification);
    }

    #endregion Method
}

#endif