

using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public class GameViewSizeHelper
{
    const int miscSize = 1; // Used when no main GameView exists (ex: batchmode)
#if UNITY_2019_3_OR_NEWER
        static Type s_GameViewType = Type.GetType("UnityEditor.PlayModeView,UnityEditor");
        static string s_GetGameViewFuncName = "GetMainPlayModeView";
#else
    static Type s_GameViewType = Type.GetType("UnityEditor.GameView,UnityEditor");
    static string s_GetGameViewFuncName = "GetMainGameView";
#endif

    private static EditorWindow GetMainGameView()
    {
        var getMainGameView = s_GameViewType.GetMethod(s_GetGameViewFuncName, BindingFlags.NonPublic | BindingFlags.Static);
        if (getMainGameView == null)
        {
            Debug.LogError(string.Format("Can't find the main Game View : {0} function was not found in {1} type ! Did API change ?",
                s_GetGameViewFuncName, s_GameViewType));
            return null;
        }
        var res = getMainGameView.Invoke(null, null);
        return (EditorWindow)res;
    }

    public static void GetGameRenderSize(out int width, out int height)
    {
        var gameView = GetMainGameView();

        if (gameView == null)
        {
            width = height = miscSize;
            return;
        }

        var prop = gameView.GetType().GetProperty("targetSize", BindingFlags.NonPublic | BindingFlags.Instance);
        var size = (Vector2)prop.GetValue(gameView, new object[] { });
        width = (int)size.x;
        height = (int)size.y;
    }
}