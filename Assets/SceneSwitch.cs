using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneSwitch : MonoBehaviour
{
    //固定写死跳转目标：你同学的场景名
    public void StartGame()
    {
        SceneManager.LoadScene("CharacterPanelTest");
    }
}