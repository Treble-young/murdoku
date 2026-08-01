using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuManager : MonoBehaviour
{
    [Header("拖拽绑定")]
    public GameObject instructionPanel;
    public GameObject maskBlack;    // 把遮罩面板拖进这个槽

    void Start()
    {
        instructionPanel.SetActive(false);
        maskBlack.SetActive(false);
    }

    // 打开教程：同时显示遮罩+弹窗
    public void OpenInstruction()
    {
        maskBlack.SetActive(true);
        instructionPanel.SetActive(true);
    }

    // 关闭教程：同时隐藏遮罩+弹窗
    public void CloseInstruction()
    {
        maskBlack.SetActive(false);
        instructionPanel.SetActive(false);
    }

    public void OnStartGame()
    {
        Debug.Log("点击开始游戏，准备进入对局");
        SceneManager.LoadScene("GameScene");
    }

    public void OnQuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}