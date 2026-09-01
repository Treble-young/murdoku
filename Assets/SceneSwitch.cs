using Murdoku;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneSwitch : MonoBehaviour
{
    private const string MainMenuSceneName = "MainMenuScene";
    private const string LevelSelectSceneName = "LevelSelectScene";

    //固定写死跳转目标：你同学的场景名
    public void StartGame()
    {
        SceneManager.LoadScene("PuzzleScene");
    }

    // 出题/编辑模式返回主菜单；游玩模式返回谜题列表。
    public void BackToMenu()
    {
        string targetScene = ResolveBackTargetScene(
            PuzzleSession.SelectedPuzzleId,
            PuzzleSession.EditMode);

        PuzzleSession.SelectedPuzzleId = null;
        PuzzleSession.EditMode = false;
        SceneManager.LoadScene(targetScene);
    }

    private static string ResolveBackTargetScene(string selectedPuzzleId, bool editMode)
    {
        bool playMode = !string.IsNullOrEmpty(selectedPuzzleId) && !editMode;
        return playMode ? LevelSelectSceneName : MainMenuSceneName;
    }
}
