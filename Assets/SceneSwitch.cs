using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneSwitch : MonoBehaviour
{
    //固定写死跳转目标：你同学的场景名
    public void StartGame()
    {
        SceneManager.LoadScene("PuzzleScene");
    }

    // 游玩模式返回：跳转到谜题列表（选关界面），而非直接回主菜单。
    public void BackToMenu()
    {
        SceneManager.LoadScene("LevelSelectScene");
    }
}
