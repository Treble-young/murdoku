using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneSwitch : MonoBehaviour
{
    //固定写死跳转目标：你同学的场景名
    public void StartGame()
    {
        SceneManager.LoadScene("CharacterPanelTest");
    }

    // 返回主菜单（SampleScene 的菜单面板）
    public void BackToMenu()
    {
        SceneManager.LoadScene("SampleScene");
    }
}
